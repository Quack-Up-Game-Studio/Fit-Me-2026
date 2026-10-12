using System;
using System.Collections.Generic;
using System.Reflection;
using FitMe.GameData;
using FitMe.Grid;
using FitMe.Shared;
using NUnit.Framework;
using QuackUp.Utils;
using R3;
using UnityEngine;

namespace FitMe.Grid.Tests
{
    public sealed class ShapeGameplayRuleTests
    {
        private readonly List<UnityEngine.Object> _objects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var instance in _objects)
            {
                if (instance) UnityEngine.Object.DestroyImmediate(instance);
            }
            _objects.Clear();
        }

        [Test]
        public void ShapeLevelDefinition_ValidatesExactCapacityAndProductionSolverConfirmsFill()
        {
            var (board, level, catalog) = CreateMockLevelAndCatalog();

            var validation = ShapeLevelDataValidator.Validate(level);
            Assert.That(validation.IsValid, Is.True, string.Join(", ", validation.Errors));
            Assert.That(catalog.Validate().IsValid, Is.True);
            Assert.That(catalog.TryGetById("test-shape-001", out var selected), Is.True);
            Assert.That(selected, Is.SameAs(level));

            var mask = new int[2, 3];
            for (var row = 0; row < 2; row++)
            for (var column = 0; column < 3; column++)
                mask[row, column] = board.customGrid[row, column] == 1 &&
                                    !board.CustomObstacleData[row, column].hasObstacle ? 1 : 0;
            var blocks = new IReadOnlyList<Vector2Int>[]
            {
                new[] { Vector2Int.zero, new Vector2Int(0, 1) },
                new[] { Vector2Int.zero, new Vector2Int(1, 0) }
            };

            Assert.That(DeterministicTilingSolver.TrySolve(mask, blocks, out var placements), Is.True);
            AssertExactCover(mask, placements);
        }

        [Test]
        public void ShapeObstacleBuilder_PreservesSameColorFourConnectedMultiAtomAdjacency()
        {
            var board = Keep(ScriptableObject.CreateInstance<GridPreset>());
            board.GridSize = new Vector2Int(3, 2);
            board.PresetGridType = GridType.Custom;
            board.customGrid = new[,] { { 1, 1, 1 }, { 1, 1, 1 } };
            typeof(GridPreset).GetProperty(nameof(GridPreset.ObstacleMode))
                .SetValue(board, ObstacleMode.Custom);
            board.CustomObstacleData = new ObstacleData[2, 3];
            for (var row = 0; row < 2; row++)
            for (var column = 0; column < 3; column++)
                board.CustomObstacleData[row, column] = new ObstacleData
                {
                    hasCell = true,
                    hasObstacle = row == 0 && column < 2,
                    color = BlockColor.Blue,
                    petrified = false
                };
            var level = Keep(ScriptableObject.CreateInstance<ShapeLevelDefinition>());
            SetPrivateField(level, "_board", board);
            SetPrivateField(level, "_playerBlocks", new List<ShapePlayerBlockEntry>
            {
                new ShapePlayerBlockEntry(CreatePreset(new[,] { { 1, 1, 1, 1 } }), 1)
            });

            var runtimeData = level.ToRuntimeData();

            Assert.That(runtimeData.Obstacles, Has.Count.EqualTo(1));
            Assert.That(runtimeData.Obstacles[0].Schema.schema, Is.EqualTo(new[,] { { 1, 1 } }));
            Assert.That(runtimeData.Obstacles[0].Color, Is.EqualTo(BlockColor.Blue));
        }

        [Test]
        public void VacantSchema_ExcludesCustomGridHolesFromPossibleFitChecks()
        {
            var gridObject = Keep(new GameObject("Pass 5 test grid"));
            var grid = gridObject.AddComponent<UnityEngine.Grid>();
            var config = Keep(ScriptableObject.CreateInstance<GridManagerConfig>());
            using var manager = new GridManager(grid, config, null, null, new NullMessageHub());
            var preset = Keep(ScriptableObject.CreateInstance<GridPreset>());
            preset.PresetGridType = GridType.Custom;
            preset.GridSize = new Vector2Int(2, 2);
            preset.customGrid = new[,] { { 1, 0 }, { 0, 1 } };
            SetPrivateField(manager, "<CurrentGridPreset>k__BackingField", preset);
            SetPrivateField(manager, "<IsGameplay>k__BackingField", true);
            var cellArray = new CellInstance[2, 2];
            cellArray[0, 0] = CreateCell(grid, Vector2Int.zero);
            cellArray[1, 1] = CreateCell(grid, new Vector2Int(1, 1));
            SetPrivateField(manager, "_cellArray", cellArray);

            Assert.That(manager.CreateVacantSchema(out var vacant, out var vacantCount), Is.True);
            Assert.That(vacantCount, Is.EqualTo(2));
            Assert.That(vacant, Is.EqualTo(new[,] { { 1, 0 }, { 0, 1 } }));

            var model = new BlockModel(null, null);
            model.SetShapeSchema(new[,] { { 1 } });
            var blockSchema = model.BlockSchemas[0].schema;
            var available = ArrayHelper.CanBFitInA(vacant, blockSchema, out _);

            Assert.That(available, Is.True, "A single atom should fit into either playable cell despite the intervening mask holes.");
        }

        private CellInstance CreateCell(UnityEngine.Grid grid, Vector2Int index)
        {
            var model = new CellModel();
            model.ArrayIndex.Value = index;
            var gameObject = Keep(new GameObject($"Pass 5 cell {index}"));
            gameObject.transform.position = grid.GetCellCenterWorld(new Vector3Int(index.x, index.y, 0));
            return new CellInstance(model, null, gameObject);
        }

        private BlockPreset CreatePreset(int[,] schema)
        {
            var preset = Keep(ScriptableObject.CreateInstance<BlockPreset>());
            preset.BlockSchema.schema = schema;
            return preset;
        }

        private (GridPreset board, ShapeLevelDefinition level, ShapeLevelCatalog catalog) CreateMockLevelAndCatalog()
        {
            var board = Keep(ScriptableObject.CreateInstance<GridPreset>());
            board.name = "Test-only shape board";
            board.PresetGridType = GridType.Custom;
            board.GridSize = new Vector2Int(3, 2);
            board.customGrid = new[,] { { 1, 1, 0 }, { 1, 1, 1 } };
            typeof(GridPreset).GetProperty(nameof(GridPreset.ObstacleMode))
                .SetValue(board, ObstacleMode.Custom);
            board.CustomObstacleData = new ObstacleData[2, 3];
            for (var row = 0; row < 2; row++)
            for (var column = 0; column < 3; column++)
                board.CustomObstacleData[row, column] = new ObstacleData
                {
                    hasCell = board.customGrid[row, column] == 1,
                    hasObstacle = row == 1 && column == 2,
                    color = BlockColor.Green,
                    petrified = true
                };

            var level = Keep(ScriptableObject.CreateInstance<ShapeLevelDefinition>());
            level.name = "Test-only exact-fit shape level";
            SetPrivateField(level, "_board", board);
            SetPrivateField(level, "_playerBlocks", new List<ShapePlayerBlockEntry>
            {
                new ShapePlayerBlockEntry(CreatePreset(new[,] { { 1, 1 } }), 1),
                new ShapePlayerBlockEntry(CreatePreset(new[,] { { 1 }, { 1 } }), 1)
            });

            var catalog = Keep(ScriptableObject.CreateInstance<ShapeLevelCatalog>());
            catalog.name = "Test-only shape catalog";
            var levelsField = typeof(ShapeLevelCatalog).GetField("_levels", BindingFlags.Instance | BindingFlags.NonPublic);
            var levels = Activator.CreateInstance(levelsField.FieldType);
            var add = levelsField.FieldType.GetMethod("Add", new[] { typeof(string), typeof(ShapeLevelDefinition) });
            add.Invoke(levels, new object[] { "test-shape-001", level });
            levelsField.SetValue(catalog, levels);

            return (board, level, catalog);
        }

        private T Keep<T>(T instance) where T : UnityEngine.Object
        {
            _objects.Add(instance);
            return instance;
        }

        private static void AssertExactCover(int[,] mask, Vector2Int[][] placements)
        {
            var covered = new HashSet<Vector2Int>();
            foreach (var placement in placements)
            foreach (var cell in placement)
                Assert.That(covered.Add(cell), Is.True, $"Duplicate placement at {cell}");
            var expected = new HashSet<Vector2Int>();
            for (var row = 0; row < mask.GetLength(0); row++)
            for (var column = 0; column < mask.GetLength(1); column++)
                if (mask[row, column] != 0) expected.Add(new Vector2Int(row, column));
            Assert.That(covered, Is.EquivalentTo(expected));
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Expected field {name}");
            field.SetValue(target, value);
        }

        private sealed class NullMessageHub : IMessageHub
        {
            public void Publish<TMessage>(TMessage message) { }
            public IDisposable Subscribe<TMessage>(Action<TMessage> action) => R3.Disposable.Empty;
            public R3.Observable<TMessage> GetObservable<TMessage>() => R3.Observable.Empty<TMessage>();
        }
    }
}