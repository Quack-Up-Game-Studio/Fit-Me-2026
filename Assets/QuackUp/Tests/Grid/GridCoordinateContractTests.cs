using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FitMe.Grid;
using GridManagerType = FitMe.Grid.GridManager;
using NUnit.Framework;
using QuackUp.Utils;
using R3;
using UnityEngine;

namespace FitMe.Grid.Tests
{
    public sealed class GridCoordinateContractTests
    {
        [TestCase(0, 0, -3, 2, -3, 2)]
        [TestCase(2, 4, -3, 2, 1, 0)]
        [TestCase(1, 3, 5, -2, 8, -3)]
        public void ArrayToGridIndex_UsesColumnForXAndInvertsRowForY(
            int row, int column, int offsetX, int offsetY, int expectedX, int expectedY)
        {
            var actual = GridUtils.ArrayToGridIndex(
                new Vector2Int(row, column), new Vector2Int(offsetX, offsetY));

            Assert.That(actual, Is.EqualTo(new Vector2Int(expectedX, expectedY)));
        }

        [TestCase(0, 0, -3, 2)]
        [TestCase(2, 4, -3, 2)]
        [TestCase(1, 3, 5, -2)]
        public void GridToArrayIndex_RoundTripsArrayIndex(
            int row, int column, int offsetX, int offsetY)
        {
            var arrayIndex = new Vector2Int(row, column);
            var offset = new Vector2Int(offsetX, offsetY);

            var gridIndex = GridUtils.ArrayToGridIndex(arrayIndex, offset);
            var actual = GridUtils.GridToArrayIndex(gridIndex, offset);

            Assert.That(actual, Is.EqualTo(arrayIndex));
        }

        [Test]
        public void CustomGridMask_ZeroCellIsUnavailableAndOneCellIsPlayable()
        {
            var fixture = new GridCreationFixture();
            var customGrid = new[,] { { 1, 0, 1 }, { 0, 1, 0 } };

            try
            {
                fixture.CreateGrid(customGrid);

                Assert.That(fixture.Cells.GetLength(0), Is.EqualTo(2));
                Assert.That(fixture.Cells.GetLength(1), Is.EqualTo(3));
                Assert.That(fixture.Cells[0, 0], Is.Not.Null);
                Assert.That(fixture.Cells[0, 1], Is.Null);
                Assert.That(fixture.Cells[0, 2], Is.Not.Null);
                Assert.That(fixture.Cells[1, 0], Is.Null);
                Assert.That(fixture.Cells[1, 1], Is.Not.Null);
                Assert.That(fixture.Cells[1, 2], Is.Null);

                Assert.That(fixture.CreatedCells, Has.Count.EqualTo(3));
                Assert.That(fixture.CreatedCells[0].Model.ArrayIndex.Value, Is.EqualTo(new Vector2Int(0, 0)));
                Assert.That(fixture.CreatedCells[1].Model.ArrayIndex.Value, Is.EqualTo(new Vector2Int(0, 2)));
                Assert.That(fixture.CreatedCells[2].Model.ArrayIndex.Value, Is.EqualTo(new Vector2Int(1, 1)));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void ShapeRuntimeMask_CreatesOnlyNormalizedPlayableCellsWithoutAReusablePreset()
        {
            var fixture = new GridCreationFixture();
            var mask = new[,] { { 1, 0, 1 }, { 0, 1, 0 } };
            var runtimeData = new ShapeLevelRuntimeData(
                null,
                mask,
                Array.Empty<ShapeObstacleRuntimeData>(),
                Array.Empty<ShapePlayerBlockRuntimeEntry>());

            try
            {
                fixture.CreateShapeGrid(runtimeData);

                Assert.That(fixture.Cells.GetLength(0), Is.EqualTo(2));
                Assert.That(fixture.Cells.GetLength(1), Is.EqualTo(3));
                Assert.That(fixture.Cells[0, 0], Is.Not.Null);
                Assert.That(fixture.Cells[0, 1], Is.Null);
                Assert.That(fixture.Cells[0, 2], Is.Not.Null);
                Assert.That(fixture.Cells[1, 0], Is.Null);
                Assert.That(fixture.Cells[1, 1], Is.Not.Null);
                Assert.That(fixture.Cells[1, 2], Is.Null);
                Assert.That(fixture.CreatedCells, Has.Count.EqualTo(3));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        private sealed class GridCreationFixture : IDisposable
        {
            private readonly GridPreset _preset;
            private readonly GridManagerConfig _config;
            private readonly CellConfig _cellConfig;
            private readonly GameObject _gridObject;
            private readonly GameObject _cellViewObject;
            private readonly TestMessageHub _messageHub = new();
            private readonly GridManagerType _manager;

            public GridCreationFixture()
            {
                _gridObject = new GameObject("Grid");
                var grid = _gridObject.AddComponent<global::UnityEngine.Grid>();
                _config = ScriptableObject.CreateInstance<GridManagerConfig>();
                _preset = ScriptableObject.CreateInstance<GridPreset>();
                _cellConfig = ScriptableObject.CreateInstance<CellConfig>();
                typeof(CellConfig).GetProperty(nameof(CellConfig.UseDedicatedSprite))
                    .SetValue(_cellConfig, false);
                _cellViewObject = new GameObject("CellView");
                var cellView = _cellViewObject.AddComponent<CellView>();
                var spriteRenderer = cellView.gameObject.AddComponent<SpriteRenderer>();
                typeof(CellView).GetField("spriteRenderer", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(cellView, spriteRenderer);
                cellView.transform.SetParent(_gridObject.transform);
                var cellFactory = new CellFactory(_cellConfig, cellView, _gridObject.transform);
                _manager = new GridManagerType(grid, _config, cellFactory, null, _messageHub);
                typeof(GridManagerType).GetProperty(nameof(GridManagerType.CurrentGridPreset))
                    .SetValue(_manager, _preset);
            }

            public CellInstance[,] Cells => (CellInstance[,])typeof(GridManagerType)
                .GetField("_cellArray", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(_manager);

            public List<CellInstance> CreatedCells =>
                new List<CellInstance>(Cells.Cast<CellInstance>().Where(cell => cell != null));

            public void CreateGrid(int[,] customGrid)
            {
                _preset.PresetGridType = GridType.Custom;
                _preset.customGrid = customGrid;
                _preset.GridSize = new Vector2Int(customGrid.GetLength(1), customGrid.GetLength(0));
                var isGameplay = typeof(GridManagerType).GetProperty(nameof(GridManagerType.IsGameplay));
                isGameplay.SetValue(_manager, true);
                var createCells = typeof(GridManagerType).GetMethod("CreateCells", BindingFlags.Instance | BindingFlags.NonPublic);
                createCells.Invoke(_manager, null);
            }

            public void CreateShapeGrid(ShapeLevelRuntimeData runtimeData)
            {
                _preset.PresetGridType = GridType.Rectangle;
                _preset.GridSize = runtimeData.GridSize;
                typeof(GridManagerType).GetProperty(nameof(GridManagerType.CurrentShapeLevelRuntimeData))
                    .SetValue(_manager, runtimeData);
                typeof(GridManagerType).GetProperty(nameof(GridManagerType.IsGameplay))
                    .SetValue(_manager, true);
                typeof(GridManagerType).GetMethod("CreateCells", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(_manager, null);
            }

            public void Dispose()
            {
                _manager.Dispose();
                _messageHub.Dispose();
                var cells = Cells;
                foreach (var cell in cells)
                {
                    if (cell?.ViewModel != null) cell.ViewModel.Dispose();
                    if (cell?.GameObject) UnityEngine.Object.DestroyImmediate(cell.GameObject);
                }
                if (_cellViewObject) UnityEngine.Object.DestroyImmediate(_cellViewObject);
                if (_cellConfig) UnityEngine.Object.DestroyImmediate(_cellConfig);
                if (_preset) UnityEngine.Object.DestroyImmediate(_preset);
                if (_config) UnityEngine.Object.DestroyImmediate(_config);
                if (_gridObject) UnityEngine.Object.DestroyImmediate(_gridObject);
            }
        }

        private sealed class TestMessageHub : IMessageHub, IDisposable
        {
            private readonly Dictionary<Type, IDisposable> _subjects = new();

            public void Publish<TMessage>(TMessage message) => GetSubject<TMessage>().OnNext(message);

            public IDisposable Subscribe<TMessage>(Action<TMessage> action) => GetSubject<TMessage>().Subscribe(action);

            public Observable<TMessage> GetObservable<TMessage>() => GetSubject<TMessage>();

            public void Dispose()
            {
                foreach (var subject in _subjects.Values) subject.Dispose();
            }

            private Subject<TMessage> GetSubject<TMessage>()
            {
                if (_subjects.TryGetValue(typeof(TMessage), out var subject)) return (Subject<TMessage>)subject;
                var newSubject = new Subject<TMessage>();
                _subjects.Add(typeof(TMessage), newSubject);
                return newSubject;
            }
        }
    }
}
