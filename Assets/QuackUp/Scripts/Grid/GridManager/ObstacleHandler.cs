using System;
using System.Collections.Generic;
using System.Linq;
using FitMe.Shared;
using QuackUp.SceneManagement;
using QuackUp.Utils;
using R3;
using Redcode.Extensions;
using UnityEngine;
using VContainer;
using Random = UnityEngine.Random;

namespace FitMe.Grid
{
    public class ObstacleHandler : IDisposable
    {
        private readonly GridManager _gridManager;
        private readonly GridManagerConfig _config;
        private readonly BlockManagerConfig _blockManagerConfig;
        private readonly BlockFactory _blockFactory;
        private readonly ILevelManager _levelManager;

        private IDisposable _bindings;

        [Inject]
        public ObstacleHandler(
            GridManager gridManager,
            GridManagerConfig config,
            BlockManagerConfig blockManagerConfig,
            BlockFactory blockFactory,
            ILevelManager levelManager)
        {
            _config = config;
            _gridManager = gridManager;
            _blockManagerConfig = blockManagerConfig;
            _blockFactory = blockFactory;
            _levelManager = levelManager;
            Bind();
        }

        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            _gridManager.OnCellsCreated
                .Subscribe(_ => CreateObstacles(_gridManager.CurrentGridPreset.ObstacleMode))
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }

        public void Dispose()
        {
            _bindings.Dispose();
        }

        private void CreateObstacles(ObstacleMode obstacleMode)
        {
            if (!_gridManager.IsGameplay) return;
            switch (obstacleMode)
            {
                case ObstacleMode.Generated:
                    GenerateObstacles();
                    break;
                case ObstacleMode.Custom:
                    CreateCustomObstacles();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(obstacleMode), obstacleMode, null);
            }
        }

        private void GenerateObstacles()
        {
            if (_levelManager.CurrentObstacleCount == 0) return;
            var allPresetArraySize = _blockManagerConfig.BlockPresetDictionary.Values
                .Select(x => x.BlockSchema.schema.GetArraySize())
                .ToList();
            var maxCol = allPresetArraySize.Max(size => size.x);
            var maxRow = allPresetArraySize.Max(size => size.y);
            var windowSize = new Vector2Int(maxCol, maxRow);
            for (var i = 0; i < _levelManager.CurrentObstacleCount; i++)
            {
                _gridManager.CreateVacantSchema(out var grid, out _);
                var randomCell = grid
                    .FlattenWithArrayIndex()
                    .Where(x => x.Item == 1)
                    .GetRandomElement();
                var window = ArrayHelper.GenerateWindow(grid, randomCell.ArrayIndex, windowSize);
                var fitSchemaData = new List<(BlockSchema blockSchema, BlockShape shape, BlockPreset preset, int[,] placed)>();
                foreach (var (shape, preset) in _blockManagerConfig.BlockPresetDictionary)
                {
                    preset.GenerateSchema();
                    foreach (var schema in preset.DistinctBlockSchemas)
                    {
                        if (ArrayHelper.CanBFitInA(window, schema.schema, out var placed, true)) 
                            fitSchemaData.Add((schema, shape, preset, placed));
                    }
                }
                var randomSchemaData = fitSchemaData.GetRandomElement();
                ArrayHelper.TryGetFirstDifference(window, randomSchemaData.placed, out var firstDifference);
                var topLeftCellArrayIndex = randomCell.ArrayIndex + firstDifference;
                var allMatchingSchema = randomSchemaData.preset.BlockSchemas
                    .Where(x => ArrayMemberComparer<int>.Default
                        .Equals(x.schema, randomSchemaData.blockSchema.schema))
                    .ToList();
                InstantiateBlock(topLeftCellArrayIndex, allMatchingSchema.GetRandomElement(), randomSchemaData.shape);
            }
        }

        private void CreateCustomObstacles()
        {
            if (_gridManager.CurrentGridPreset.CustomObstacleData == null || _gridManager.CurrentGridPreset.CustomObstacleData.Length == 0)
            {
                DebugUtils.Log("No obstacle data found.");
                return;
            }
            //group by shape first
            var flattenObstacleData = _gridManager.CurrentGridPreset.CustomObstacleData.FlattenWithArrayIndex();
            var groupedByShape = flattenObstacleData
                .Where(x => x.Item.hasCell && x.Item.hasObstacle)
                .GroupBy(x => x.Item.shape).ToList();
            if (!groupedByShape.Any())
            {
                DebugUtils.Log("No obstacles to create.");
                return;
            }
            foreach (var shapes in groupedByShape)
            {
                //then group by id
                var groupedById = shapes.GroupBy(x => x.Item.id).ToList();
                var blockPreset = _blockManagerConfig.BlockPresetDictionary[shapes.Key];
                blockPreset.GenerateSchema();
                var schemas = blockPreset.BlockSchemas;
                foreach (var ids in groupedById)
                {
                    var matchedSchema = 
                    (
                        from schema in schemas
                        let flattenSchemaWithCell = schema.schema.FlattenWithArrayIndex()
                            .Where(x => x.Item == 1)
                            .ToList()
                        where ArrayHelper.IsTheSameShape(ids.Select(x => x.ArrayIndex).ToList(), 
                            flattenSchemaWithCell.Select(x => x.ArrayIndex).ToList())
                        select schema
                    ).ToList();
                    if (matchedSchema.Count == 0)
                    {
                        DebugUtils.LogWarning($"No matching schema found for obstacle id {ids.Key} and shape {shapes.Key}");
                        continue;
                    }
                    var randomSchema = matchedSchema.GetRandomElement();
                    var topLeftObstacleCell = ids
                        .OrderBy(x => x.ArrayIndex.x)
                        .ThenBy(x => x.ArrayIndex.y)
                        .First();
                    var shape = shapes.Key;
                    var topLeftCellArrayIndex = topLeftObstacleCell.ArrayIndex;
                    InstantiateBlock(topLeftCellArrayIndex, randomSchema, shape);
                }
            }
        }

        private void InstantiateBlock(Vector2Int topLeftCellArrayIndex, BlockSchema randomSchema, BlockShape shape)
        {
            var destinationCell = _gridManager.GetCellByArrayIndex(topLeftCellArrayIndex);
            var rotation = Quaternion.Euler(0, 0, randomSchema.Index * 90f);
            var block = _blockFactory.Create(shape, Vector3.zero, rotation, new InstantiateParameters
            {
                parent = null,
                worldSpace = true,
            });
            var scale = new Vector3(_config.CellSize.x, _config.CellSize.y, 1f);
            block.GameObject.transform.localScale = scale;
            var topLeftBlockAtom = block.Model.Atoms
                .Select(x => (instance: x,
                    arrayIndex: ArrayHelper.RotateIndexBySchema(x.Model.ArrayIndex.CurrentValue,
                        randomSchema.Index, randomSchema.schema.GetArraySize())))
                .OrderBy(x => x.arrayIndex.x)
                .ThenBy(x => x.arrayIndex.y)
                .Select(x => x.instance)
                .First();
            var distance = destinationCell.GameObject.transform.position - topLeftBlockAtom.GameObject.transform.position;
            block.GameObject.transform.position += distance;
            var randomColor = EnumUtils.RandomValue<BlockColor>();
            block.GameObject.name = $"Block_{shape}";
            block.Model.ChangeType(randomColor, false);
            block.Model.BlockState.Value = BlockState.Obstacle;
            block.ViewModel.ScaleInCommand.Execute(new ScaleInCommandData(new Promise<Unit>(), scale));
            _gridManager.TryPlaceBlock(block, false);
            block.ViewModel.SetSortingLayerCommand.Execute(_blockManagerConfig.GridSortingLayer);
        }
    }
}