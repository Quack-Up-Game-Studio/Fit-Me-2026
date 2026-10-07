using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using FitMe.Grid;
using FitMe.Shared;
using NUnit.Framework;
using ObservableCollections;
using QuackUp.Utils;
using R3;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace FitMe.Grid.Tests
{
    public class GridManagerLifecycleTests
    {
        private readonly List<Object> _objects = new();
        private GridManager _manager;
        private CountingHub _hub;
        private UnityEngine.Grid _grid;
        private CellModel _cell;
        private AtomModel _atom;
        private BlockModel _blockModel;
        private BlockViewModel _blockViewModel;
        private BlockInstance _block;
        private readonly List<AtomModel> _additionalAtomModels = new();
        private readonly List<CellModel> _additionalCellModels = new();
        private readonly List<BlockModel> _additionalBlockModels = new();

        [SetUp]
        public void SetUp()
        {
            _cell = null;
            _atom = null;
            _blockModel = null;
            _blockViewModel = null;
            _block = null;
            _additionalAtomModels.Clear();
            _additionalCellModels.Clear();
            _additionalBlockModels.Clear();
            var gridObject = Keep(new GameObject("Lifecycle test grid"));
            _grid = gridObject.AddComponent<UnityEngine.Grid>();
            var config = Keep(ScriptableObject.CreateInstance<GridManagerConfig>());
            _hub = new CountingHub();
            // Factory and audio are deliberately null: these tests never create
            // cells through the factory, play audio, or run async grid effects.
            _manager = new GridManager(_grid, config, null, null, _hub);
        }

        [TearDown]
        public void TearDown()
        {
            // Avoid an extra manager Dispose obscuring failures on old code.
            // A fresh fixture is created for each test.
            _hub?.Dispose();
            _cell?.Dispose();
            _atom?.ParentBlock.Dispose();
            _atom?.ArrayIndex.Dispose();
            foreach (var atomModel in _additionalAtomModels)
            {
                atomModel.ParentBlock.Dispose();
                atomModel.ArrayIndex.Dispose();
            }
            foreach (var cellModel in _additionalCellModels)
                cellModel.Dispose();
            foreach (var blockModel in _additionalBlockModels)
            {
                blockModel.UpdateGridCommand.Dispose();
                blockModel.BlockState.Dispose();
            }
            _blockModel?.UpdateGridCommand.Dispose();
            _blockModel?.BlockState.Dispose();
            if (_blockViewModel != null)
            {
                _blockViewModel.Dispose();
                _blockViewModel.BlockInteractionState.Dispose();
                _blockViewModel.SetSortingLayerCommand.Dispose();
                _blockViewModel.SetSortingOrderCommand.Dispose();
                _blockViewModel.ExplodeCommand.Dispose();
                _blockViewModel.ScaleInCommand.Dispose();
                _blockViewModel.RotateCommand.Dispose();
                _blockViewModel.DestroyCommand.Dispose();
            }
            // Release Subjects even in RED runs without calling manager Dispose
            // a second time. This is fixture cleanup, not the behavior under test.
            foreach (var field in typeof(GridManager).GetFields(
                         BindingFlags.Instance | BindingFlags.NonPublic))
            {
                if (field.FieldType.IsGenericType &&
                    field.FieldType.GetGenericTypeDefinition() == typeof(Subject<>))
                    ((IDisposable)field.GetValue(_manager)).Dispose();
            }
            foreach (var item in _objects)
                if (item) Object.DestroyImmediate(item);
            _objects.Clear();
        }

        private T Keep<T>(T obj) where T : Object
        {
            _objects.Add(obj);
            return obj;
        }

        private void SetManagerField(string name, object value)
        {
            var field = typeof(GridManager).GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Fixture anchor missing: {name}");
            field.SetValue(_manager, value);
        }

        private void SetUpOneCellPlacement(bool withViewModel = true)
        {
            var preset = Keep(ScriptableObject.CreateInstance<GridPreset>());
            preset.GridSize = Vector2Int.one;
            SetManagerField("<CurrentGridPreset>k__BackingField", preset);
            _cell = new CellModel();
            var cellObject = Keep(new GameObject("Lifecycle test cell"));
            cellObject.transform.position = _grid.GetCellCenterWorld(Vector3Int.zero);
            SetManagerField("_cellArray", new[,]
            {
                { new CellInstance(_cell, null, cellObject) }
            });
            var blockObject = Keep(new GameObject("Lifecycle test block"));
            var atomObject = Keep(new GameObject("Lifecycle test atom"));
            atomObject.transform.position = cellObject.transform.position;
            _blockModel = new BlockModel(null, null);
            if (withViewModel) _blockViewModel = new BlockViewModel(_blockModel);
            _block = new BlockInstance(_blockModel, _blockViewModel, null, blockObject);
            _atom = new AtomModel();
            _atom.ParentBlock.Value = _block;
            _blockModel.Atoms.Add(new AtomInstance(_atom, null, atomObject));
        }

        private void AddAtomAt(Vector3 position)
        {
            var atomModel = new AtomModel();
            atomModel.ParentBlock.Value = _block;
            _additionalAtomModels.Add(atomModel);
            var atomObject = Keep(new GameObject("Lifecycle test additional atom"));
            atomObject.transform.position = position;
            _blockModel.Atoms.Add(new AtomInstance(atomModel, null, atomObject));
        }

        private BlockModel SetBlockPreset(int[,] schema)
        {
            var preset = Keep(ScriptableObject.CreateInstance<BlockPreset>());
            preset.BlockSchema.schema = schema;
            preset.GenerateSchema();
            typeof(BlockModel).GetProperty(nameof(BlockModel.BlockPreset)).SetValue(_blockModel, preset);
            return _blockModel;
        }

        private ObstacleHandler CreateObstacleHandler(BlockPreset preset)
        {
            var config = Keep(ScriptableObject.CreateInstance<BlockManagerConfig>());
            var presetsField = typeof(BlockManagerConfig).GetField("_blockPresetDictionary",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(presetsField, Is.Not.Null);
            presetsField.SetValue(config, new Dictionary<BlockShape, BlockPreset> { [default] = preset });
            var levelManager = new ObstacleLevelManagerMock(1);
            Assert.That(((ILevelManager)levelManager).CurrentObstacleCount, Is.EqualTo(1));
            return new ObstacleHandler(_manager, null, config, null, levelManager);
        }

        private static void GenerateObstacles(ObstacleHandler handler)
        {
            var method = typeof(ObstacleHandler).GetMethod("GenerateObstacles",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(handler, null);
        }

        private static void AssertDisposed(CancellationTokenSource source)
        {
            Assert.That(source, Is.Not.Null, "veto event must supply a source");
            Assert.Throws<ObjectDisposedException>(() => source.Cancel());
        }

        [Test]
        public void Dispose_UnsubscribesIncomingHubHandlers()
        {
            Assert.That(_hub.ActiveHandlers, Is.GreaterThan(0));
            _manager.Dispose();
            Assert.That(_hub.ActiveHandlers, Is.Zero);
        }

        [Test]
        public void Placement_VetoSourceIsDisposed_WhenCancelled()
        {
            SetUpOneCellPlacement();
            CancellationTokenSource captured = null;
            using var subscription = _manager.OnAboutToPlaceBlock.Subscribe(x =>
            {
                captured = x.placeCancellation;
                x.placeCancellation.Cancel();
            });
            Assert.That(_manager.TryPlaceBlock(_block, updateGrid: false), Is.False);
            Assert.That(_cell.CurrentAtom.Value, Is.Null, "veto prevents occupancy");
            Assert.That(_manager.BlocksOnGrid.Count, Is.Zero);
            AssertDisposed(captured);
            _manager.Dispose();
        }

        [Test]
        public void Placement_VetoSourceIsDisposed_AfterSuccessfulPlacement()
        {
            SetUpOneCellPlacement();
            CancellationTokenSource captured = null;
            var placedEvents = 0;
            using var veto = _manager.OnAboutToPlaceBlock.Subscribe(x =>
                captured = x.placeCancellation);
            using var placed = _manager.OnBlockPlaced.Subscribe(_ => placedEvents++);
            Assert.That(_manager.TryPlaceBlock(_block, updateGrid: false), Is.True);
            Assert.That(_cell.CurrentAtom.Value, Is.SameAs(_blockModel.Atoms[0]));
            Assert.That(_manager.BlocksOnGrid.Count, Is.EqualTo(1));
            Assert.That(_blockViewModel.BlockInteractionState.Value,
                Is.EqualTo(BlockInteractionState.PlacedOnGrid));
            Assert.That(_block.GameObject.transform.parent, Is.SameAs(_grid.transform));
            Assert.That(placedEvents, Is.EqualTo(1));
            AssertDisposed(captured);
            _manager.Dispose();
        }

        [Test]
        public void Placement_VetoSourceIsDisposed_IfDownstreamPlacementThrows()
        {
            // Deliberate invalid fixture to exercise exceptional-exit disposal.
            // This test does not claim partially committed placement is rolled back.
            SetUpOneCellPlacement(withViewModel: false);
            CancellationTokenSource captured = null;
            using var subscription = _manager.OnAboutToPlaceBlock.Subscribe(x =>
                captured = x.placeCancellation);
            Assert.Throws<NullReferenceException>(() =>
                _manager.TryPlaceBlock(_block, updateGrid: false));
            AssertDisposed(captured);
            _manager.Dispose();
        }

        [Test]
        public void ValidatePlacement_RejectsMultipleAtomsMappedToOneCell()
        {
            SetUpOneCellPlacement();
            AddAtomAt(_grid.GetCellCenterWorld(Vector3Int.zero));

            Assert.That(_manager.ValidatePlacement(_blockModel), Is.False);
        }

        [Test]
        public void TryPlaceBlock_RejectsMultipleAtomsMappedToOneCell()
        {
            SetUpOneCellPlacement();
            AddAtomAt(_grid.GetCellCenterWorld(Vector3Int.zero));

            try
            {
                Assert.That(_manager.TryPlaceBlock(_block, updateGrid: false), Is.False);
                Assert.That(_cell.CurrentAtom.Value, Is.Null);
                Assert.That(_manager.BlocksOnGrid.Count, Is.Zero);
            }
            finally
            {
                _manager.Dispose();
            }
        }

        [Test]
        public void BlockPreset_GenerateSchemaDoesNotRebuildUnchangedSchemas()
        {
            var preset = Keep(ScriptableObject.CreateInstance<BlockPreset>());
            preset.GenerateSchema();
            var originalSchema = preset.BlockSchemas[0];

            preset.GenerateSchema();

            Assert.That(preset.BlockSchemas[0], Is.SameAs(originalSchema));
        }

        [Test]
        public void AtomView_MissingColorMappingPreservesRendererColor()
        {
            var viewObject = Keep(new GameObject("Atom view edge test"));
            var atomView = viewObject.AddComponent<AtomView>();
            var spriteRenderer = viewObject.AddComponent<SpriteRenderer>();
            var expectedColor = Color.magenta;
            spriteRenderer.color = expectedColor;
            var config = Keep(ScriptableObject.CreateInstance<BlockManagerConfig>());
            typeof(AtomView).GetField("spriteRenderer",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(atomView, spriteRenderer);
            typeof(AtomView).GetField("_blockManagerConfig",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(atomView, config);
            var handler = typeof(AtomView).GetMethod("OnBlockTypeChanged",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(handler, Is.Not.Null);
            var color = Enum.ToObject(handler.GetParameters()[0].ParameterType, 0);
            LogAssert.Expect(LogType.Error, $"BlockType.CurrentValue {color} is not defined");

            handler.Invoke(atomView, new[] { color });

            Assert.That(spriteRenderer.color, Is.EqualTo(expectedColor));
        }

        [Test]
        public void CreateVacantSchema_ReportsEmptyCellAsVacant()
        {
            SetUpOneCellPlacement();

            var hasVacantCells = _manager.CreateVacantSchema(out var schema, out var vacantCount);

            Assert.That(hasVacantCells, Is.True);
            Assert.That(vacantCount, Is.EqualTo(1));
            Assert.That(schema[0, 0], Is.EqualTo(1));
        }

        [Test]
        public void CreateVacantSchema_ExcludesCellOccupiedByActiveBlock()
        {
            SetUpOneCellPlacement();
            _cell.CurrentAtom.Value = _blockModel.Atoms[0];

            var hasVacantCells = _manager.CreateVacantSchema(out var schema, out var vacantCount);

            Assert.That(hasVacantCells, Is.False);
            Assert.That(vacantCount, Is.Zero);
            Assert.That(schema[0, 0], Is.Zero);
        }

        [Test]
        public void CreateVacantSchema_TreatsExplodingBlockCellAsVacant()
        {
            SetUpOneCellPlacement();
            _cell.CurrentAtom.Value = _blockModel.Atoms[0];
            _blockModel.BlockState.Value = BlockState.Exploding;

            var hasVacantCells = _manager.CreateVacantSchema(out var schema, out var vacantCount);

            Assert.That(hasVacantCells, Is.True);
            Assert.That(vacantCount, Is.EqualTo(1));
            Assert.That(schema[0, 0], Is.EqualTo(1));
        }

        [Test]
        public void CheckAvailableBlock_AcceptsPresetThatFitsEmptyCell()
        {
            SetUpOneCellPlacement();
            var blockModel = SetBlockPreset(new[,] { { 1 } });

            var canPlaceBlock = _manager.CheckAvailableBlock(new List<BlockModel> { blockModel }, out var availableBlocks);

            Assert.That(canPlaceBlock, Is.True);
            Assert.That(availableBlocks, Is.EquivalentTo(new[] { blockModel }));
        }

        [Test]
        public void CheckAvailableBlock_RejectsPresetLargerThanGrid()
        {
            SetUpOneCellPlacement();
            var blockModel = SetBlockPreset(new[,] { { 1, 1 }, { 1, 1 } });

            var canPlaceBlock = _manager.CheckAvailableBlock(new List<BlockModel> { blockModel }, out var availableBlocks);

            Assert.That(canPlaceBlock, Is.False);
            Assert.That(availableBlocks, Is.Empty);
        }

        [Test]
        public void CheckForContact_CollectsConnectedSameColorBlocksOnce()
        {
            SetUpOneCellPlacement(withViewModel: false);
            var gridPreset = Keep(ScriptableObject.CreateInstance<GridPreset>());
            gridPreset.GridSize = new Vector2Int(2, 1);
            SetManagerField("<CurrentGridPreset>k__BackingField", gridPreset);

            var firstCellObject = Keep(new GameObject("Contact first cell"));
            var firstCell = new CellInstance(_cell, null, firstCellObject);
            _cell.ArrayIndex.Value = Vector2Int.zero;
            _cell.CurrentAtom.Value = _blockModel.Atoms[0];
            _blockModel.BlockCells = new List<CellInstance> { firstCell };

            var secondCellModel = new CellModel();
            _additionalCellModels.Add(secondCellModel);
            secondCellModel.ArrayIndex.Value = new Vector2Int(0, 1);
            var secondCellObject = Keep(new GameObject("Contact second cell"));
            var secondCell = new CellInstance(secondCellModel, null, secondCellObject);

            var secondBlockModel = new BlockModel(null, null);
            _additionalBlockModels.Add(secondBlockModel);
            var secondBlockObject = Keep(new GameObject("Contact second block"));
            var secondBlock = new BlockInstance(secondBlockModel, null, null, secondBlockObject);
            var secondAtomModel = new AtomModel();
            secondAtomModel.ParentBlock.Value = secondBlock;
            _additionalAtomModels.Add(secondAtomModel);
            var secondAtomObject = Keep(new GameObject("Contact second atom"));
            var secondAtom = new AtomInstance(secondAtomModel, null, secondAtomObject);
            secondBlockModel.Atoms.Add(secondAtom);
            secondBlockModel.BlockCells = new List<CellInstance> { secondCell };
            secondCellModel.CurrentAtom.Value = secondAtom;

            SetManagerField("_cellArray", new[,] { { firstCell, secondCell } });
            var checkForContact = typeof(GridManager).GetMethod("CheckForContact",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(checkForContact, Is.Not.Null);
            var contacts = new List<BlockInstance>();

            checkForContact.Invoke(_manager, new object[] { _block, contacts });

            Assert.That(contacts, Has.Count.EqualTo(2));
            Assert.That(contacts, Is.EquivalentTo(new[] { _block, secondBlock }));
        }

        [Test]
        public void SpawnBlocksFromBag_DoesNotIndexWhenThereAreNoSpawnPoints()
        {
            var config = Keep(ScriptableObject.CreateInstance<BlockManagerConfig>());
            using var blockManager = new BlockManager(
                _manager,
                config,
                null,
                Array.Empty<BlockManager.SpawnPointData>(),
                null,
                Array.Empty<BlockManager.SpawnPointData>(),
                _hub);

            Assert.DoesNotThrow(() => blockManager.SpawnBlocksFromBag(refill: false));
        }

        [Test]
        public void Swap_DoesNothingWhenSpawnBagIsEmpty()
        {
            SetUpOneCellPlacement();
            SetBlockPreset(new[,] { { 1 } });
            var config = Keep(ScriptableObject.CreateInstance<BlockManagerConfig>());
            using var controller = new BlockController(config, _manager, null, null, null);
            var swappableBlock = new BlockInstance(_blockModel, _blockViewModel, controller, _block.GameObject);
            var handPoint = new BlockManager.SpawnPointData
            {
                IsFree = false,
                CurrentBlock = swappableBlock
            };
            var previewPoint = new BlockManager.SpawnPointData
            {
                IsFree = false,
                CurrentBlock = swappableBlock
            };
            using var blockManager = new BlockManager(
                _manager,
                config,
                null,
                new[] { handPoint },
                null,
                new[] { previewPoint },
                _hub);

            Assert.DoesNotThrow(blockManager.Swap);
            Assert.That(handPoint.CurrentBlock, Is.SameAs(swappableBlock));
            Assert.That(previewPoint.CurrentBlock, Is.SameAs(swappableBlock));
        }

        [Test]
        public void GenerateObstacles_DoesNotChooseFromAnEmptyVacantCellSet()
        {
            SetUpOneCellPlacement(withViewModel: false);
            _cell.CurrentAtom.Value = _blockModel.Atoms[0];
            var preset = Keep(ScriptableObject.CreateInstance<BlockPreset>());
            preset.BlockSchema.schema = new[,] { { 1 } };
            preset.GenerateSchema();
            using var obstacleHandler = CreateObstacleHandler(preset);

            Assert.DoesNotThrow(() => GenerateObstacles(obstacleHandler));
        }

        [Test]
        public void GenerateObstacles_SkipsWhenNoPresetFitsTheSelectedWindow()
        {
            SetUpOneCellPlacement(withViewModel: false);
            var preset = Keep(ScriptableObject.CreateInstance<BlockPreset>());
            preset.BlockSchema.schema = new[,] { { 1, 1 }, { 1, 1 } };
            preset.GenerateSchema();
            using var obstacleHandler = CreateObstacleHandler(preset);

            Assert.DoesNotThrow(() => GenerateObstacles(obstacleHandler));
        }

        [TestCase("OnCellsCreated")]
        [TestCase("OnAboutToPlaceBlock")]
        [TestCase("OnBlockPlaced")]
        [TestCase("OnScoreAdded")]
        [TestCase("OnFitCheck")]
        [TestCase("OnAboutToClearGrid")]
        [TestCase("OnClearGrid")]
        public void Dispose_ReleasesEveryOwnedSubject(string propertyName)
        {
            var property = typeof(GridManager).GetProperty(propertyName);
            Assert.That(property, Is.Not.Null);
            var subject = property.GetValue(_manager);
            var isDisposed = subject.GetType().GetProperty("IsDisposed");
            Assert.That(isDisposed, Is.Not.Null, "R3 Subject exposes disposal state");
            _manager.Dispose();
            Assert.That((bool)isDisposed.GetValue(subject), Is.True, propertyName);
        }

        [Test]
        public void Dispose_ReleasesPerBlockSubscriptionExactlyOnce()
        {
            var counter = new CountingDisposable();
            // This isolates cleanup; Dispose does not access BlockInstance.
            ((ObservableList<GridBlockData>)_manager.BlocksOnGrid)
                .Add(new GridBlockData(null, counter));
            _manager.Dispose();
            _manager.Dispose();
            Assert.That(counter.Count, Is.EqualTo(1));
        }

        [Test]
        public void Dispose_ReentrantCleanupDoesNotDisposeResourceTwice()
        {
            var counter = new CountingDisposable();
            counter.OnDispose = () =>
            {
                // Bound reentry so a regression fails rather than overflowing.
                counter.OnDispose = null;
                _manager.Dispose();
            };
            ((ObservableList<GridBlockData>)_manager.BlocksOnGrid)
                .Add(new GridBlockData(null, counter));
            _manager.Dispose();
            Assert.That(counter.Count, Is.EqualTo(1));
        }

        private sealed class CountingDisposable : IDisposable
        {
            public int Count { get; private set; }
            public Action OnDispose { get; set; }
            public void Dispose()
            {
                Count++;
                OnDispose?.Invoke();
            }
        }

        private sealed class CountingHub : IMessageHub, IDisposable
        {
            private readonly List<IDisposable> _streams = new();
            public int ActiveHandlers { get; private set; }
            public void Publish<TMessage>(TMessage message) { }
            public IDisposable Subscribe<TMessage>(Action<TMessage> action)
            {
                ActiveHandlers++;
                return Disposable.Create(() => ActiveHandlers--);
            }
            public Observable<TMessage> GetObservable<TMessage>()
            {
                var subject = new Subject<TMessage>();
                _streams.Add(subject);
                return subject;
            }
            public void Dispose()
            {
                foreach (var stream in _streams) stream.Dispose();
            }
        }

        private sealed class ObstacleLevelManagerMock : LevelManagerMock, ILevelManager
        {
            public new int CurrentObstacleCount { get; }

            public ObstacleLevelManagerMock(int obstacleCount) : base(global::FitMe.Shared.GameState.PlaceBlock)
            {
                CurrentObstacleCount = obstacleCount;
            }
        }
    }
}
