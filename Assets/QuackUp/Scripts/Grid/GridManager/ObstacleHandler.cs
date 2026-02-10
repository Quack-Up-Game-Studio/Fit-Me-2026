using System;
using System.Collections.Generic;
using System.Linq;
using FitMe.Shared;
using QuackUp.Utils;
using R3;
using Redcode.Extensions;
using UnityEngine;
using VContainer;

namespace FitMe.Grid
{
    public class ObstacleHandler : IDisposable
    {
        private readonly GridManager _gridManager;
        private readonly GridManagerConfig _config;
        private readonly BlockManagerConfig _blockManagerConfig;
        private readonly BlockFactory _blockFactory;

        private IDisposable _bindings;

        [Inject]
        public ObstacleHandler(
            GridManager gridManager,
            GridManagerConfig config,
            BlockManagerConfig blockManagerConfig,
            BlockFactory blockFactory)
        {
            _config = config;
            _gridManager = gridManager;
            _blockManagerConfig = blockManagerConfig;
            _blockFactory = blockFactory;
            Bind();
        }

        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            _gridManager.OnCellsCreated
                .Subscribe(_ => CreateObstacles())
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }

        public void Dispose()
        {
            _bindings.Dispose();
        }

        private void CreateObstacles()
        {
            if (_gridManager.CurrentGridPreset.ObstacleData == null || _gridManager.CurrentGridPreset.ObstacleData.Length == 0)
            {
                DebugUtils.Log("No obstacle data found.");
                return;
            }
            //group by shape first
            var flattenObstacleData = _gridManager.CurrentGridPreset.ObstacleData.FlattenWithArrayIndex();
            var groupedByShape = flattenObstacleData
                .Where(x => x.Item1.hasCell && x.Item1.hasObstacle)
                .GroupBy(x => x.Item1.shape).ToList();
            if (!groupedByShape.Any())
            {
                DebugUtils.Log("No obstacles to create.");
                return;
            }
            foreach (var shapes in groupedByShape)
            {
                //then group by id
                var groupedById = shapes.GroupBy(x => x.Item1.id).ToList();
                var blockPreset = _blockManagerConfig.BlockPresetDictionary[shapes.Key];
                blockPreset.GenerateSchema();
                var schemas = blockPreset.BlockSchemas;
                foreach (var ids in groupedById)
                {
                    var matchedSchema = new List<BlockSchema>();
                    foreach (var schema in schemas)
                    {
                        var flattenSchemaWithCell = schema.schema
                            .FlattenWithArrayIndex()
                            .Where(x => x.Item1 == 1)
                            .ToList();
                        if (!ArrayHelper.IsTheSameShape(ids.Select(x => x.Item2).ToList(), 
                                flattenSchemaWithCell.Select(x => x.Item2).ToList()))
                        {
                            continue;
                        }
                        matchedSchema.Add(schema);
                    }
                    if (matchedSchema.Count == 0)
                    {
                        DebugUtils.LogWarning($"No matching schema found for obstacle id {ids.Key} and shape {shapes.Key}");
                        continue;
                    }
                    var randomSchema = matchedSchema.GetRandomElement();
                    var topLeftObstacleCell = ids
                        .OrderBy(x => x.Item2.x)
                        .ThenBy(x => x.Item2.y)
                        .First();
                    var destinationCell = _gridManager.GetCellByArrayIndex(topLeftObstacleCell.Item2);
                    var randomRotationQuaternion = Quaternion.Euler(0f, 0f, randomSchema.Index * 90f);
                    var block = _blockFactory.Create(shapes.Key, Vector3.zero, randomRotationQuaternion, new InstantiateParameters
                    {
                        parent = null,
                        worldSpace = true,
                    });
                    var scale = new Vector3(_config.CellSize.x, _config.CellSize.y, 1f);
                    block.GameObject.transform.localScale = scale;
                    var topLeftBlockAtom = block.Model.Atoms
                        .OrderBy(x => x.GameObject.transform.position.x)
                        .ThenByDescending(x => x.GameObject.transform.position.y)
                        .First();
                    var distance = destinationCell.GameObject.transform.position - topLeftBlockAtom.GameObject.transform.position;
                    block.GameObject.transform.position += distance;
                    var randomColor = EnumUtils.RandomValue<BlockColor>();
                    block.GameObject.name = $"Block_{shapes.Key}";
                    block.Model.ChangeType(randomColor, false);
                    block.ViewModel.ScaleInCommand.Execute(new ScaleInCommandData(new Promise<Unit>(), scale));
                    _gridManager.TryPlaceBlock(block, false, true);
                    block.ViewModel.SetSortingLayerCommand.Execute(_blockManagerConfig.GridSortingLayer);
                }
            }
        }
    }
}