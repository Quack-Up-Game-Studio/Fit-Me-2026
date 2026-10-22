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
    public partial class GridManagerLifecycleTests
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
        private readonly List<BlockViewModel> _additionalBlockViewModels = new();
        private readonly List<BlockController> _additionalBlockControllers = new();

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
            _additionalBlockViewModels.Clear();
            _additionalBlockControllers.Clear();
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
            foreach (var blockViewModel in _additionalBlockViewModels)
            {
                blockViewModel.Dispose();
                blockViewModel.BlockInteractionState.Dispose();
                blockViewModel.SetSortingLayerCommand.Dispose();
                blockViewModel.SetSortingOrderCommand.Dispose();
                blockViewModel.ExplodeCommand.Dispose();
                blockViewModel.ScaleInCommand.Dispose();
                blockViewModel.RotateCommand.Dispose();
                blockViewModel.DestroyCommand.Dispose();
            }
            foreach (var blockController in _additionalBlockControllers)
                blockController.Dispose();
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

        private BlockPreset CreateBlockPreset(int[,] schema)
        {
            var preset = Keep(ScriptableObject.CreateInstance<BlockPreset>());
            preset.BlockSchema.schema = schema;
            return preset;
        }

        private BlockInstance CreateShapeTestBlock(
            BlockManagerConfig config,
            ShapeBlockSupplyQueue.Item item,
            Transform parent,
            int spawnIndex,
            bool isActive)
        {
            var model = new BlockModel(null, null);
            typeof(BlockModel).GetProperty(nameof(BlockModel.BlockPreset))
                .SetValue(model, item.PlayerBlock.Preset);
            model.SetShapeSchema(item.PlayerBlock.Schema);
            model.ChangeColor(item.Color, updateGrid: false);
            model.SpawnIndex = spawnIndex;

            var viewModel = new BlockViewModel(model);
            var controller = new BlockController(config, _manager, null, null, null);
            var gameObject = Keep(new GameObject("Shape queue test block"));
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.rotation = item.Rotation;
            var block = new BlockInstance(model, viewModel, controller, gameObject);
            controller.Initialize(block);
            controller.SetActive(isActive);

            _additionalBlockModels.Add(model);
            _additionalBlockViewModels.Add(viewModel);
            _additionalBlockControllers.Add(controller);
            return block;
        }

        private static void RefreshBlockManagerSwapAvailability(BlockManager blockManager)
        {
            var refreshMethod = typeof(BlockManager).GetMethod("RefreshSwapAvailability",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(refreshMethod, Is.Not.Null);
            refreshMethod.Invoke(blockManager, null);
        }

        private static BlockManager.SpawnPointData CreateSpawnPoint(
            Transform transform,
            BlockInstance block,
            bool isFree)
        {
            var spawnPoint = new BlockManager.SpawnPointData
            {
                CurrentBlock = block,
                IsFree = isFree
            };
            typeof(BlockManager.SpawnPointData).GetProperty(nameof(BlockManager.SpawnPointData.Transform))
                .SetValue(spawnPoint, transform);
            return spawnPoint;
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
        public void ValidatePlacement_RejectsStageHole()
        {
            SetUpOneCellPlacement();
            var preset = Keep(ScriptableObject.CreateInstance<GridPreset>());
            preset.GridSize = Vector2Int.one;
            preset.PresetGridType = GridType.Custom;
            preset.customGrid = new[,] { { 0 } };
            SetManagerField("<CurrentGridPreset>k__BackingField", preset);

            Assert.That(_manager.ValidatePlacement(_blockModel), Is.False);
        }

        [Test]
        public void Placement_PreviewAndCommitUseSameCell_AndSnapAtomToExactCenter()
        {
            SetUpOneCellPlacement();
            var cellCenter = _grid.GetCellCenterWorld(Vector3Int.zero);
            var atom = _blockModel.Atoms[0];
            atom.GameObject.transform.SetParent(_block.GameObject.transform, true);
            atom.GameObject.transform.position = cellCenter + new Vector3(0.23f, -0.17f, 0f);
            _block.GameObject.transform.position = Vector3.zero;

            Assert.That(_manager.ValidatePlacement(_blockModel), Is.True);
            Assert.That(_cell.State.Value, Is.EqualTo(CellState.CanBePlaced));
            Assert.That(_cell.CurrentAtom.Value, Is.Null);

            Assert.That(_manager.TryPlaceBlock(_block, updateGrid: false), Is.True);

            Assert.That(_cell.CurrentAtom.Value, Is.SameAs(atom));
            Assert.That(_blockModel.BlockCells, Has.Count.EqualTo(1));
            Assert.That(_blockModel.BlockCells[0].Model, Is.SameAs(_cell));
            Assert.That(atom.GameObject.transform.position, Is.EqualTo(cellCenter));
            Assert.That(_cell.State.Value, Is.EqualTo(CellState.None));
        }

        [Test]
        public void ValidatePlacement_ClearsStalePreviewWhenTargetBecomesOccupied()
        {
            SetUpOneCellPlacement();
            Assert.That(_manager.ValidatePlacement(_blockModel), Is.True);
            Assert.That(_cell.State.Value, Is.EqualTo(CellState.CanBePlaced));

            _cell.CurrentAtom.Value = _blockModel.Atoms[0];

            Assert.That(_manager.ValidatePlacement(_blockModel), Is.False);
            Assert.That(_cell.State.Value, Is.EqualTo(CellState.CannotBePlaced));
        }

        [Test]
        public void Placement_PreservesPlacementScoreFitEventAndGridRegistration()
        {
            SetUpOneCellPlacement();
            ScoreEvent? scoreEvent = null;
            FitTypeEvent? fitEvent = null;
            using var scoreSubscription = _manager.OnScoreAdded.Subscribe(value => scoreEvent = value);
            using var fitSubscription = _manager.OnFitCheck.Subscribe(value => fitEvent = value);

            Assert.That(_manager.TryPlaceBlock(_block, updateGrid: true), Is.True);

            Assert.That(scoreEvent.HasValue, Is.True);
            Assert.That(scoreEvent.Value.ScoreType, Is.EqualTo(ScoreTypes.Placement));
            Assert.That(fitEvent.HasValue, Is.True);
            Assert.That(fitEvent.Value.FitType, Is.EqualTo(FitType.FitMe));
            Assert.That(fitEvent.Value.Block, Is.SameAs(_block));
            Assert.That(_manager.BlocksOnGrid, Has.Count.EqualTo(1));
        }

        [Test]
        public void ReorderRenderingOrder_AssignsStableAscendingOrders()
        {
            SetUpOneCellPlacement();
            var secondModel = new BlockModel(null, null);
            _additionalBlockModels.Add(secondModel);
            var secondViewModel = new BlockViewModel(secondModel);
            var secondBlock = new BlockInstance(secondModel, secondViewModel, null,
                Keep(new GameObject("Lifecycle second block")));
            var firstOrder = -1;
            var secondOrder = -1;
            using var firstSubscription = _blockViewModel.SetSortingOrderCommand.Subscribe(value => firstOrder = value);
            using var secondSubscription = secondViewModel.SetSortingOrderCommand.Subscribe(value => secondOrder = value);
            ((ObservableList<GridBlockData>)_manager.BlocksOnGrid).Add(new GridBlockData(_block, Disposable.Empty));
            ((ObservableList<GridBlockData>)_manager.BlocksOnGrid).Add(new GridBlockData(secondBlock, Disposable.Empty));

            _manager.ReorderRenderingOrder();

            Assert.That(firstOrder, Is.EqualTo(0));
            Assert.That(secondOrder, Is.EqualTo(2));
            secondViewModel.Dispose();
            secondViewModel.BlockInteractionState.Dispose();
            secondViewModel.SetSortingLayerCommand.Dispose();
            secondViewModel.SetSortingOrderCommand.Dispose();
            secondViewModel.ExplodeCommand.Dispose();
            secondViewModel.ScaleInCommand.Dispose();
            secondViewModel.RotateCommand.Dispose();
            secondViewModel.DestroyCommand.Dispose();
        }

        [Test]
        public void ClearGrid_RaisesLifecycleEventsAndClearsCommittedOccupancy()
        {
            SetUpOneCellPlacement();
            Assert.That(_manager.TryPlaceBlock(_block, updateGrid: false), Is.True);
            var aboutToClearCount = 0;
            var clearedCount = 0;
            using var aboutToClear = _manager.OnAboutToClearGrid.Subscribe(_ => aboutToClearCount++);
            using var cleared = _manager.OnClearGrid.Subscribe(_ => clearedCount++);

            var clearGrid = typeof(GridManager).GetMethod(nameof(GridManager.ClearGrid));
            var operation = clearGrid.Invoke(_manager, new object[] { true, false });
            var awaiter = operation.GetType().GetMethod("GetAwaiter").Invoke(operation, null);
            awaiter.GetType().GetMethod("GetResult").Invoke(awaiter, null);

            Assert.That(aboutToClearCount, Is.EqualTo(1));
            Assert.That(clearedCount, Is.EqualTo(1));
            Assert.That(_cell.CurrentAtom.Value, Is.Null);
            Assert.That(_manager.BlocksOnGrid, Is.Empty);
        }

        [Test]
        public void Placement_PreparesAllCellsBeforeVetoAndDoesNotPartiallyCommitIfCallbackOccupiesCell()
        {
            SetUpOneCellPlacement();
            var preset = Keep(ScriptableObject.CreateInstance<GridPreset>());
            preset.GridSize = new Vector2Int(2, 1);
            SetManagerField("<CurrentGridPreset>k__BackingField", preset);
            var secondCellModel = new CellModel();
            _additionalCellModels.Add(secondCellModel);
            _cell.ArrayIndex.Value = Vector2Int.zero;
            var secondCellObject = Keep(new GameObject("Lifecycle second cell"));
            secondCellObject.transform.position = _grid.GetCellCenterWorld(Vector3Int.right);
            var secondCell = new CellInstance(secondCellModel, null, secondCellObject);
            secondCellModel.ArrayIndex.Value = new Vector2Int(0, 1);
            var firstCell = new CellInstance(_cell, null, Keep(new GameObject("Lifecycle first cell")));
            SetManagerField("_cellArray", new CellInstance[,] { { firstCell, secondCell } });
            AddAtomAt(secondCellObject.transform.position);

            var callbackSawUnoccupiedCells = false;
            using var subscription = _manager.OnAboutToPlaceBlock.Subscribe(_ =>
            {
                callbackSawUnoccupiedCells = _cell.CurrentAtom.Value == null &&
                                             secondCellModel.CurrentAtom.Value == null;
                secondCellModel.CurrentAtom.Value = _blockModel.Atoms[0];
            });

            Assert.That(_manager.ValidatePlacement(_blockModel), Is.True);
            Assert.That(_manager.TryPlaceBlock(_block, updateGrid: false), Is.False);
            Assert.That(callbackSawUnoccupiedCells, Is.True);
            Assert.That(_cell.CurrentAtom.Value, Is.Null, "commit must not partially occupy the first cell");
            Assert.That(secondCellModel.CurrentAtom.Value, Is.SameAs(_blockModel.Atoms[0]));
            Assert.That(_manager.BlocksOnGrid, Is.Empty);
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
            _blockModel = new BlockModel(null, null);
            var color = (BlockColor)0;
            _blockModel.ChangeColor(color, updateGrid: false);
            var block = new BlockInstance(
                _blockModel, null, null, Keep(new GameObject("Atom view edge test block")));
            typeof(AtomView).GetField("spriteRenderer",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(atomView, spriteRenderer);
            typeof(AtomView).GetField("_blockManagerConfig",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(atomView, config);
            var updateColor = typeof(AtomView).GetMethod("UpdateAtomSpriteColor",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(updateColor, Is.Not.Null);
            if (updateColor == null) return;
            LogAssert.Expect(LogType.Error, $"BlockType.CurrentValue {color} is not defined");

            updateColor.Invoke(atomView, new object[] { block });

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
        public void CheckAvailableBlock_UsesRuntimeGeometryInsteadOfVisualPresetSchema()
        {
            SetUpOneCellPlacement();
            var blockModel = SetBlockPreset(new[,] { { 1 } });
            blockModel.SetShapeSchema(new[,] { { 1, 1 }, { 1, 0 } });

            var canPlaceBlock = _manager.CheckAvailableBlock(new List<BlockModel> { blockModel }, out var availableBlocks);

            Assert.That(canPlaceBlock, Is.False);
            Assert.That(availableBlocks, Is.Empty);
        }

        [Test]
        public void SpawnWithShapeLevelEvent_MakesRuntimeDataAvailableToGridConsumers()
        {
            var runtimeData = new ShapeLevelRuntimeData(null, null, null);

            _hub.Publish(new SpawnWithShapeLevelEvent(runtimeData));

            var property = typeof(GridManager).GetProperty("CurrentShapeLevelRuntimeData");
            Assert.That(property, Is.Not.Null, "GridManager must expose the active authored level data");
            Assert.That(property.GetValue(_manager), Is.SameAs(runtimeData));
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
        public void ShapeBlockSupplyQueue_PeekNextReturnsAuthoredOrderWithoutConsuming()
        {
            var first = new ShapeBlockSupplyQueue.Item(
                new ShapePlayerBlockRuntimeEntry(CreateBlockPreset(new[,] { { 1 } }), new[,] { { 1 } }),
                Quaternion.identity,
                default);
            var secondPreset = CreateBlockPreset(new[,] { { 1, 1 } });
            var second = new ShapeBlockSupplyQueue.Item(
                new ShapePlayerBlockRuntimeEntry(secondPreset, secondPreset.BlockSchema.schema),
                Quaternion.identity,
                default);
            var thirdPreset = CreateBlockPreset(new[,] { { 1 }, { 1 } });
            var third = new ShapeBlockSupplyQueue.Item(
                new ShapePlayerBlockRuntimeEntry(thirdPreset, thirdPreset.BlockSchema.schema),
                Quaternion.identity,
                default);
            var queue = new ShapeBlockSupplyQueue();
            queue.Reset(new[] { first, second, third });

            var preview = queue.PeekNext(2);

            Assert.That(preview, Has.Count.EqualTo(2));
            Assert.That(preview[0].PlayerBlock.Preset, Is.SameAs(first.PlayerBlock.Preset));
            Assert.That(preview[1].PlayerBlock.Preset, Is.SameAs(second.PlayerBlock.Preset));
            Assert.That(queue.Count, Is.EqualTo(3));
        }

        [Test]
        public void ShapeBlockSupplyQueue_SwapRequeuesDisplacedSupplyAtFront()
        {
            var activePreset = CreateBlockPreset(new[,] { { 1 } });
            var active = new ShapeBlockSupplyQueue.Item(
                new ShapePlayerBlockRuntimeEntry(activePreset, activePreset.BlockSchema.schema),
                Quaternion.identity,
                default);
            var nextPreset = CreateBlockPreset(new[,] { { 1, 1 } });
            var next = new ShapeBlockSupplyQueue.Item(
                new ShapePlayerBlockRuntimeEntry(nextPreset, nextPreset.BlockSchema.schema),
                Quaternion.Euler(0f, 0f, 90f),
                default);
            var remainingPreset = CreateBlockPreset(new[,] { { 1 }, { 1 } });
            var remaining = new ShapeBlockSupplyQueue.Item(
                new ShapePlayerBlockRuntimeEntry(remainingPreset, remainingPreset.BlockSchema.schema),
                Quaternion.identity,
                default);
            var queue = new ShapeBlockSupplyQueue();
            queue.Reset(new[] { next, remaining });

            var swapped = queue.TrySwapWithNext(active, out var incoming);
            var preview = queue.PeekNext(2);

            Assert.That(swapped, Is.True);
            Assert.That(incoming.PlayerBlock.Preset, Is.SameAs(nextPreset));
            Assert.That(queue.Count, Is.EqualTo(2));
            Assert.That(preview[0].PlayerBlock.Preset, Is.SameAs(activePreset));
            Assert.That(preview[1].PlayerBlock.Preset, Is.SameAs(remainingPreset));
        }

        [Test]
        public void ShapeBlockSupplyQueue_CannotSwapWhenUpcomingSupplyIsEmpty()
        {
            var queue = new ShapeBlockSupplyQueue();
            queue.Reset(Array.Empty<ShapeBlockSupplyQueue.Item>());

            var swapped = queue.TrySwapWithNext(default, out var incoming);

            Assert.That(swapped, Is.False);
            Assert.That(incoming.PlayerBlock, Is.Null);
            Assert.That(queue.Count, Is.Zero);
        }

        [Test]
        public void ShapeBlockSupplyQueue_ResetSkipsInvalidEntriesWithoutBlockingLaterSupply()
        {
            var queue = new ShapeBlockSupplyQueue();
            var validPreset = CreateBlockPreset(new[,] { { 1, 1 } });
            queue.Reset(new[]
            {
                new ShapeBlockSupplyQueue.Item(
                    new ShapePlayerBlockRuntimeEntry(null, null),
                    Quaternion.identity,
                    BlockColor.Red),
                new ShapeBlockSupplyQueue.Item(
                    new ShapePlayerBlockRuntimeEntry(validPreset, validPreset.BlockSchema.schema),
                    Quaternion.identity,
                    BlockColor.Blue)
            });

            Assert.That(queue.Count, Is.EqualTo(1));
            Assert.That(queue.PeekNext(1)[0].PlayerBlock.Preset, Is.SameAs(validPreset));
        }

        [Test]
        public void BlockManager_ShapeQueueSkipsInvalidEntriesBeforeLaterValidBlocks()
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
            var validPreset = CreateBlockPreset(new[,] { { 1, 1 } });
            var runtimeData = new ShapeLevelRuntimeData(
                null,
                new[,] { { 1, 1 } },
                Array.Empty<ShapeObstacleRuntimeData>(),
                new[]
                {
                    new ShapePlayerBlockRuntimeEntry(null, null),
                    new ShapePlayerBlockRuntimeEntry(validPreset, validPreset.BlockSchema.schema)
                });

            _hub.Publish(new SpawnWithShapeLevelEvent(runtimeData));

            var queueField = typeof(BlockManager).GetField("_shapeBlockQueue",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(queueField, Is.Not.Null);
            var queue = (ShapeBlockSupplyQueue)queueField.GetValue(blockManager);
            Assert.That(queue.Count, Is.EqualTo(1));
            Assert.That(queue.PeekNext(1)[0].PlayerBlock.Preset, Is.SameAs(validPreset));
        }

        [Test]
        public void BlockManager_NewShapeLevelClearsStaleSpawnBlocksBeforeReplacingSupply()
        {
            SetUpOneCellPlacement();
            var preset = SetBlockPreset(new[,] { { 1 } }).BlockPreset;
            var config = Keep(ScriptableObject.CreateInstance<BlockManagerConfig>());
            var handTransform = Keep(new GameObject("Old shape hand slot")).transform;
            using var controller = new BlockController(config, _manager, null, null, null);
            var handBlock = new BlockInstance(_blockModel, _blockViewModel, controller, _block.GameObject);
            _block.GameObject.transform.SetParent(handTransform, false);
            var handPoint = CreateSpawnPoint(handTransform, handBlock, false);
            using var blockManager = new BlockManager(
                _manager,
                config,
                null,
                new[] { handPoint },
                null,
                Array.Empty<BlockManager.SpawnPointData>(),
                _hub);
            var activeSupplyField = typeof(BlockManager).GetField("_activeShapeSupplyBySpawnIndex",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(activeSupplyField, Is.Not.Null);
            var activeSupplies = (Dictionary<int, ShapeBlockSupplyQueue.Item>)activeSupplyField.GetValue(blockManager);
            activeSupplies[0] = new ShapeBlockSupplyQueue.Item(
                new ShapePlayerBlockRuntimeEntry(preset, preset.BlockSchema.schema),
                Quaternion.identity,
                BlockColor.Red);

            _hub.Publish(new SpawnWithShapeLevelEvent(new ShapeLevelRuntimeData(
                null,
                new[,] { { 1 } },
                Array.Empty<ShapeObstacleRuntimeData>(),
                Array.Empty<ShapePlayerBlockRuntimeEntry>())));

            Assert.That(handPoint.IsFree, Is.True);
            Assert.That(handPoint.CurrentBlock, Is.Null);
            Assert.That(activeSupplies, Is.Empty);
        }

        [Test]
        public void BlockManager_ResetSpawnPointReturnsActiveShapeSupplyToQueueFront()
        {
            var activePreset = CreateBlockPreset(new[,] { { 1 } });
            var activeSchema = activePreset.BlockSchema.schema;
            var nextPreset = CreateBlockPreset(new[,] { { 1, 1 } });
            var config = Keep(ScriptableObject.CreateInstance<BlockManagerConfig>());
            var handTransform = Keep(new GameObject("Shape reset hand slot")).transform;
            var handPoint = CreateSpawnPoint(handTransform, null, true);
            using var blockManager = new BlockManager(
                _manager,
                config,
                null,
                new[] { handPoint },
                null,
                Array.Empty<BlockManager.SpawnPointData>(),
                _hub);
            _hub.Publish(new SpawnWithShapeLevelEvent(new ShapeLevelRuntimeData(
                null,
                new[,] { { 1, 1, 1 } },
                Array.Empty<ShapeObstacleRuntimeData>(),
                new[]
                {
                    new ShapePlayerBlockRuntimeEntry(null, activeSchema),
                    new ShapePlayerBlockRuntimeEntry(nextPreset, nextPreset.BlockSchema.schema)
                })));

            var queueField = typeof(BlockManager).GetField("_shapeBlockQueue",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var activeSupplyField = typeof(BlockManager).GetField("_activeShapeSupplyBySpawnIndex",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(queueField, Is.Not.Null);
            Assert.That(activeSupplyField, Is.Not.Null);
            var queue = (ShapeBlockSupplyQueue)queueField.GetValue(blockManager);
            Assert.That(queue.TryTakeNext(out var activeItem), Is.True);
            handPoint.CurrentBlock = CreateShapeTestBlock(config, activeItem, handTransform, 0, true);
            handPoint.IsFree = false;
            var activeSupplies = (Dictionary<int, ShapeBlockSupplyQueue.Item>)activeSupplyField.GetValue(blockManager);
            activeSupplies[0] = activeItem;

            blockManager.ResetSpawnPoint();

            Assert.That(handPoint.IsFree, Is.True);
            Assert.That(handPoint.CurrentBlock, Is.Null);
            Assert.That(queue.Count, Is.EqualTo(2));
            var supplies = queue.PeekNext(2);
            Assert.That(supplies[0].PlayerBlock.Preset, Is.Null);
            Assert.That(supplies[0].PlayerBlock.Schema, Is.EqualTo(activeSchema));
            Assert.That(supplies[1].PlayerBlock.Preset, Is.SameAs(nextPreset));
        }

        [Test]
        public void BlockManager_ResetSpawnPointPreservesMultipleActiveSupplyOrder()
        {
            SetUpOneCellPlacement();
            var presets = new[]
            {
                CreateBlockPreset(new[,] { { 1 } }),
                CreateBlockPreset(new[,] { { 1, 1 } }),
                CreateBlockPreset(new[,] { { 1, 1, 1 } }),
                CreateBlockPreset(new[,] { { 1, 1, 1, 1 } })
            };
            var entries = Array.ConvertAll(presets,
                preset => new ShapePlayerBlockRuntimeEntry(preset, preset.BlockSchema.schema));
            var config = Keep(ScriptableObject.CreateInstance<BlockManagerConfig>());
            var firstTransform = Keep(new GameObject("First shape hand slot")).transform;
            var secondTransform = Keep(new GameObject("Second shape hand slot")).transform;
            var spawnPoints = new[]
            {
                CreateSpawnPoint(firstTransform, null, true),
                CreateSpawnPoint(secondTransform, null, true)
            };
            using var blockManager = new BlockManager(
                _manager,
                config,
                null,
                spawnPoints,
                null,
                Array.Empty<BlockManager.SpawnPointData>(),
                _hub);
            _hub.Publish(new SpawnWithShapeLevelEvent(new ShapeLevelRuntimeData(
                null,
                new[,] { { 1, 1, 1, 1 } },
                Array.Empty<ShapeObstacleRuntimeData>(),
                entries)));

            var queueField = typeof(BlockManager).GetField("_shapeBlockQueue",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var activeSupplyField = typeof(BlockManager).GetField("_activeShapeSupplyBySpawnIndex",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(queueField, Is.Not.Null);
            Assert.That(activeSupplyField, Is.Not.Null);
            var queue = (ShapeBlockSupplyQueue)queueField.GetValue(blockManager);
            Assert.That(queue.TryTakeNext(out var firstSupply), Is.True);
            Assert.That(queue.TryTakeNext(out var secondSupply), Is.True);

            var firstActiveBlock = CreateShapeTestBlock(config, firstSupply, firstTransform, 0, true);
            spawnPoints[0].CurrentBlock = firstActiveBlock;
            spawnPoints[0].IsFree = false;
            spawnPoints[1].CurrentBlock = CreateShapeTestBlock(config, secondSupply, secondTransform, 1, true);
            spawnPoints[1].IsFree = false;
            var activeSupplies = (Dictionary<int, ShapeBlockSupplyQueue.Item>)activeSupplyField.GetValue(blockManager);
            activeSupplies[0] = firstSupply;
            activeSupplies[1] = secondSupply;

            Assert.That(queue.TrySwapWithNext(firstSupply, out var incomingSupply), Is.True);
            firstActiveBlock.ViewModel.DestroyCommand.Execute(Unit.Default);
            spawnPoints[0].CurrentBlock = CreateShapeTestBlock(config, incomingSupply, firstTransform, 0, true);
            activeSupplies[0] = incomingSupply;

            blockManager.ResetSpawnPoint();

            var supplies = queue.PeekNext(4);
            Assert.That(queue.Count, Is.EqualTo(4));
            var expectedPresets = new[] { presets[2], presets[1], presets[0], presets[3] };
            for (var i = 0; i < expectedPresets.Length; i++)
                Assert.That(supplies[i].PlayerBlock.Preset, Is.SameAs(expectedPresets[i]), $"Supply at position {i}");
        }

        [Test]
        public void BlockManager_ShapeModeDisablesSwapEvenIfLegacyHandAndPreviewArePopulated()
        {
            SetUpOneCellPlacement();
            var activePreset = SetBlockPreset(new[,] { { 1 } }).BlockPreset;
            var activeSchema = activePreset.BlockSchema.schema;
            var nextPreset = CreateBlockPreset(new[,] { { 1, 1 } });
            var remainingPreset = CreateBlockPreset(new[,] { { 1 }, { 1 } });
            var runtimeData = new ShapeLevelRuntimeData(
                null,
                new[,] { { 1, 1, 1, 1, 1, 1 } },
                Array.Empty<ShapeObstacleRuntimeData>(),
                new[]
                {
                    new ShapePlayerBlockRuntimeEntry(null, activeSchema),
                    new ShapePlayerBlockRuntimeEntry(nextPreset, nextPreset.BlockSchema.schema),
                    new ShapePlayerBlockRuntimeEntry(remainingPreset, remainingPreset.BlockSchema.schema)
                });
            var config = Keep(ScriptableObject.CreateInstance<BlockManagerConfig>());
            config.PreviewCount = 2;
            var handTransform = Keep(new GameObject("Shape hand slot")).transform;
            var previewTransform = Keep(new GameObject("Shape preview slot")).transform;
            var handPoint = CreateSpawnPoint(handTransform, null, true);
            var previewPoint = CreateSpawnPoint(previewTransform, null, true);

            using var blockManager = new BlockManager(
                _manager,
                config,
                null,
                new[] { handPoint },
                null,
                new[] { previewPoint },
                _hub);
            _hub.Publish(new SpawnWithShapeLevelEvent(runtimeData));

            var queueField = typeof(BlockManager).GetField("_shapeBlockQueue",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(queueField, Is.Not.Null);
            var queue = (ShapeBlockSupplyQueue)queueField.GetValue(blockManager);
            Assert.That(queue.TryTakeNext(out var activeItem), Is.True);
            var activeSupplyField = typeof(BlockManager).GetField("_activeShapeSupplyBySpawnIndex",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(activeSupplyField, Is.Not.Null);
            var activeSupplies = (Dictionary<int, ShapeBlockSupplyQueue.Item>)activeSupplyField.GetValue(blockManager);
            activeSupplies[0] = activeItem;
            var handBlock = CreateShapeTestBlock(config, activeItem, handTransform, 0, true);
            handPoint.CurrentBlock = handBlock;
            handPoint.IsFree = false;

            var nextItem = queue.PeekNext(2)[0];
            var nextBlock = CreateShapeTestBlock(config, nextItem, previewTransform, -1, false);
            previewPoint.CurrentBlock = nextBlock;
            previewPoint.IsFree = false;
            RefreshBlockManagerSwapAvailability(blockManager);
            Assert.That(blockManager.CanSwap.CurrentValue, Is.False);

            blockManager.Swap();

            Assert.That(handPoint.CurrentBlock, Is.SameAs(handBlock));
            Assert.That(previewPoint.CurrentBlock, Is.SameAs(nextBlock));
            Assert.That(nextBlock.GameObject.transform.parent, Is.SameAs(previewTransform));
            Assert.That(handBlock.GameObject.transform.parent, Is.SameAs(handTransform));
            Assert.That(queue.Count, Is.EqualTo(2));
            var remainingSupply = queue.PeekNext(2);
            Assert.That(remainingSupply[0].PlayerBlock.Preset, Is.SameAs(nextPreset));
            Assert.That(remainingSupply[1].PlayerBlock.Preset, Is.SameAs(remainingPreset));
            Assert.That(blockManager.CanSwap.CurrentValue, Is.False);
        }

        [Test]
        public void BlockManager_ShapeModeSwapStaysDisabledAcrossRotationStateChanges()
        {
            SetUpOneCellPlacement();
            var presets = new[]
            {
                CreateBlockPreset(new[,] { { 1 } }),
                CreateBlockPreset(new[,] { { 1, 1 } }),
                CreateBlockPreset(new[,] { { 1, 1, 1 } }),
                CreateBlockPreset(new[,] { { 1, 1, 1, 1 } })
            };
            var entries = Array.ConvertAll(presets,
                preset => new ShapePlayerBlockRuntimeEntry(preset, preset.BlockSchema.schema));
            var config = Keep(ScriptableObject.CreateInstance<BlockManagerConfig>());
            config.PreviewCount = 1;
            var firstHandTransform = Keep(new GameObject("First swap hand slot")).transform;
            var secondHandTransform = Keep(new GameObject("Second swap hand slot")).transform;
            var previewTransform = Keep(new GameObject("Multi-slot swap preview")).transform;
            var firstHandPoint = CreateSpawnPoint(firstHandTransform, null, true);
            var secondHandPoint = CreateSpawnPoint(secondHandTransform, null, true);
            var previewPoint = CreateSpawnPoint(previewTransform, null, true);
            using var blockManager = new BlockManager(
                _manager,
                config,
                null,
                new[] { firstHandPoint, secondHandPoint },
                null,
                new[] { previewPoint },
                _hub);
            _hub.Publish(new SpawnWithShapeLevelEvent(new ShapeLevelRuntimeData(
                null,
                new[,] { { 1, 1, 1, 1 } },
                Array.Empty<ShapeObstacleRuntimeData>(),
                entries)));

            var queueField = typeof(BlockManager).GetField("_shapeBlockQueue",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var activeSupplyField = typeof(BlockManager).GetField("_activeShapeSupplyBySpawnIndex",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(queueField, Is.Not.Null);
            Assert.That(activeSupplyField, Is.Not.Null);
            var queue = (ShapeBlockSupplyQueue)queueField.GetValue(blockManager);
            Assert.That(queue.TryTakeNext(out var firstSupply), Is.True);
            Assert.That(queue.TryTakeNext(out var secondSupply), Is.True);
            var firstActiveBlock = CreateShapeTestBlock(config, firstSupply, firstHandTransform, 0, true);
            var secondActiveBlock = CreateShapeTestBlock(config, secondSupply, secondHandTransform, 1, true);
            firstHandPoint.CurrentBlock = firstActiveBlock;
            firstHandPoint.IsFree = false;
            secondHandPoint.CurrentBlock = secondActiveBlock;
            secondHandPoint.IsFree = false;
            var activeSupplies = (Dictionary<int, ShapeBlockSupplyQueue.Item>)activeSupplyField.GetValue(blockManager);
            activeSupplies[0] = firstSupply;
            activeSupplies[1] = secondSupply;

            var nextSupply = queue.PeekNext(1)[0];
            var previewBlock = CreateShapeTestBlock(config, nextSupply, previewTransform, -1, false);
            previewPoint.CurrentBlock = previewBlock;
            previewPoint.IsFree = false;
            RefreshBlockManagerSwapAvailability(blockManager);
            Assert.That(blockManager.CanSwap.CurrentValue, Is.False);
            var setRotationState = typeof(BlockController).GetMethod("SetRotationState",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(setRotationState, Is.Not.Null);
            setRotationState.Invoke(firstActiveBlock.Controller, new object[] { true });
            Assert.That(blockManager.CanSwap.CurrentValue, Is.False);
            setRotationState.Invoke(firstActiveBlock.Controller, new object[] { false });
            Assert.That(blockManager.CanSwap.CurrentValue, Is.False);

            blockManager.Swap();

            Assert.That(firstHandPoint.CurrentBlock, Is.SameAs(firstActiveBlock));
            Assert.That(secondHandPoint.CurrentBlock, Is.SameAs(secondActiveBlock));
            Assert.That(previewPoint.CurrentBlock, Is.SameAs(previewBlock));
            Assert.That(activeSupplies[0].PlayerBlock.Preset, Is.SameAs(presets[0]));
            Assert.That(activeSupplies[1].PlayerBlock.Preset, Is.SameAs(presets[1]));
            Assert.That(queue.Count, Is.EqualTo(2));
            Assert.That(queue.PeekNext(2)[0].PlayerBlock.Preset, Is.SameAs(presets[2]));
            Assert.That(queue.PeekNext(2)[1].PlayerBlock.Preset, Is.SameAs(presets[3]));
        }

        [Test]
        public void BlockManager_ShapeModeSwapIsUnavailableWhenNoUpcomingBlockExists()
        {
            SetUpOneCellPlacement();
            var activePreset = SetBlockPreset(new[,] { { 1 } }).BlockPreset;
            var activeSchema = activePreset.BlockSchema.schema;
            var config = Keep(ScriptableObject.CreateInstance<BlockManagerConfig>());
            var handTransform = Keep(new GameObject("Shape hand slot without queue")).transform;
            var handPoint = CreateSpawnPoint(handTransform, null, true);
            var previewPoint = CreateSpawnPoint(Keep(new GameObject("Empty preview slot")).transform, null, true);
            using var blockManager = new BlockManager(
                _manager,
                config,
                null,
                new[] { handPoint },
                null,
                new[] { previewPoint },
                _hub);
            _hub.Publish(new SpawnWithShapeLevelEvent(new ShapeLevelRuntimeData(
                null,
                new[,] { { 1 } },
                Array.Empty<ShapeObstacleRuntimeData>(),
                new[] { new ShapePlayerBlockRuntimeEntry(null, activeSchema) })));

            var queueField = typeof(BlockManager).GetField("_shapeBlockQueue",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var activeSupplyField = typeof(BlockManager).GetField("_activeShapeSupplyBySpawnIndex",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(queueField, Is.Not.Null);
            Assert.That(activeSupplyField, Is.Not.Null);
            var queue = (ShapeBlockSupplyQueue)queueField.GetValue(blockManager);
            Assert.That(queue.TryTakeNext(out var activeItem), Is.True);
            var activeSupplies = (Dictionary<int, ShapeBlockSupplyQueue.Item>)activeSupplyField.GetValue(blockManager);
            activeSupplies[0] = activeItem;
            var handBlock = CreateShapeTestBlock(config, activeItem, handTransform, 0, true);
            handPoint.CurrentBlock = handBlock;
            handPoint.IsFree = false;
            RefreshBlockManagerSwapAvailability(blockManager);

            Assert.That(blockManager.CanSwap.CurrentValue, Is.False);
            blockManager.Swap();
            Assert.That(handPoint.CurrentBlock, Is.SameAs(handBlock));
            Assert.That(queue.Count, Is.Zero);
        }

        [Test]
        public void BlockManager_QueuesAuthoredPlayerBlocksInOrderOnShapeLevelEvent()
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
            var firstBlock = CreateBlockPreset(new[,] { { 1, 1 } });
            var secondBlock = CreateBlockPreset(new[,] { { 1 }, { 1 }, { 1 } });
            var runtimeData = new ShapeLevelRuntimeData(
                null,
                new[,] { { 1, 1, 1, 1, 1 } },
                Array.Empty<ShapeObstacleRuntimeData>(),
                new[]
                {
                    new ShapePlayerBlockRuntimeEntry(firstBlock, firstBlock.BlockSchema.schema),
                    new ShapePlayerBlockRuntimeEntry(secondBlock, secondBlock.BlockSchema.schema)
                });
            _hub.Publish(new SpawnWithShapeLevelEvent(runtimeData));

            var queueField = typeof(BlockManager).GetField("_shapeBlockQueue",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(queueField, Is.Not.Null, "BlockManager must retain authored player blocks");
            var queue = (ShapeBlockSupplyQueue)queueField.GetValue(blockManager);
            var entries = queue.PeekNext(2);
            Assert.That(entries, Has.Count.EqualTo(2));
            Assert.That(entries[0].PlayerBlock.Preset, Is.SameAs(firstBlock));
            Assert.That(entries[1].PlayerBlock.Preset, Is.SameAs(secondBlock));
        }

        [Test]
        public void BlockManager_QueuesNormalizedInlineAndPresetFootprintsInAuthoredOrder()
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
            var preset = CreateBlockPreset(new[,] { { 1 }, { 1 } });
            var inlineFootprint = new[,] { { 1, 1, 0 }, { 0, 1, 1 } };
            var runtimeData = new ShapeLevelRuntimeData(
                null,
                new[,] { { 1, 1, 1, 1, 1, 1 } },
                Array.Empty<ShapeObstacleRuntimeData>(),
                new[]
                {
                    new ShapePlayerBlockRuntimeEntry(null, inlineFootprint),
                    new ShapePlayerBlockRuntimeEntry(preset, preset.BlockSchema.schema)
                });

            _hub.Publish(new SpawnWithShapeLevelEvent(runtimeData));

            var queueField = typeof(BlockManager).GetField("_shapeBlockQueue",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(queueField, Is.Not.Null);
            var queue = (ShapeBlockSupplyQueue)queueField.GetValue(blockManager);
            var entries = queue.PeekNext(2);
            var first = entries[0].PlayerBlock;
            var second = entries[1].PlayerBlock;

            Assert.That(first.Preset, Is.Null);
            Assert.That(first.Schema, Is.EqualTo(inlineFootprint));
            Assert.That(second.Preset, Is.SameAs(preset));
            Assert.That(second.Schema, Is.EqualTo(preset.BlockSchema.schema));
        }

        [Test]
        public void ResetBag_WhenContinuingShapeLevelRestoresAuthoredPlayerBlockQueue()
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
            var inlineFootprint = new[,] { { 1, 1 } };
            var playerBlockEntries = new[]
            {
                new ShapePlayerBlockRuntimeEntry(null, inlineFootprint),
                new ShapePlayerBlockRuntimeEntry(
                    CreateBlockPreset(new[,] { { 1 }, { 1 } }),
                    new[,] { { 1 }, { 1 } })
            };
            var runtimeData = new ShapeLevelRuntimeData(
                null,
                new[,] { { 1, 1, 1, 1 } },
                Array.Empty<ShapeObstacleRuntimeData>(),
                playerBlockEntries);
            _hub.Publish(new SpawnWithShapeLevelEvent(runtimeData));

            var queueField = typeof(BlockManager).GetField("_shapeBlockQueue",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(queueField, Is.Not.Null);
            var queue = (ShapeBlockSupplyQueue)queueField.GetValue(blockManager);
            Assert.That(queue.TryTakeNext(out var activeItem), Is.True);
            Assert.That(queue.TrySwapWithNext(activeItem, out var incomingItem), Is.True);
            Assert.That(incomingItem.PlayerBlock.Preset, Is.SameAs(playerBlockEntries[1].Preset));
            Assert.That(queue.PeekNext(1)[0].PlayerBlock.Schema, Is.EqualTo(inlineFootprint));

            blockManager.ResetBag(shouldRespawn: true);

            queue = (ShapeBlockSupplyQueue)queueField.GetValue(blockManager);
            var entries = queue.PeekNext(playerBlockEntries.Length);
            Assert.That(entries, Has.Count.EqualTo(playerBlockEntries.Length));
            Assert.That(entries[0].PlayerBlock.Schema, Is.EqualTo(inlineFootprint));
            Assert.That(entries[1].PlayerBlock.Preset, Is.SameAs(playerBlockEntries[1].Preset));
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

        [Test]
        public void CreateObstacles_UsesAuthoredLevelInsteadOfLegacyObstacleGeneration()
        {
            SetUpOneCellPlacement(withViewModel: false);
            SetManagerField("<IsGameplay>k__BackingField", true);
            var board = (GridPreset)typeof(GridManager)
                .GetProperty(nameof(GridManager.CurrentGridPreset))
                .GetValue(_manager);
            _hub.Publish(new SpawnWithShapeLevelEvent(new ShapeLevelRuntimeData(
                board,
                Array.Empty<ShapeObstacleRuntimeData>(),
                Array.Empty<BlockPreset>())));
            var preset = Keep(ScriptableObject.CreateInstance<BlockPreset>());
            preset.BlockSchema.schema = new[,] { { 1 } };
            preset.GenerateSchema();
            using var obstacleHandler = CreateObstacleHandler(preset);
            var cellsCreated = (Subject<Unit>)typeof(GridManager).GetField("_onCellsCreated",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_manager);

            Assert.DoesNotThrow(() => cellsCreated.OnNext(Unit.Default));
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
            private readonly Dictionary<Type, IDisposable> _streams = new();
            public int ActiveHandlers { get; private set; }
            public void Publish<TMessage>(TMessage message) => GetStream<TMessage>().OnNext(message);
            public IDisposable Subscribe<TMessage>(Action<TMessage> action)
            {
                ActiveHandlers++;
                var subscription = GetStream<TMessage>().Subscribe(action);
                return Disposable.Create(() =>
                {
                    subscription.Dispose();
                    ActiveHandlers--;
                });
            }
            public Observable<TMessage> GetObservable<TMessage>() => GetStream<TMessage>();

            private Subject<TMessage> GetStream<TMessage>()
            {
                if (!_streams.TryGetValue(typeof(TMessage), out var stream))
                {
                    stream = new Subject<TMessage>();
                    _streams.Add(typeof(TMessage), stream);
                }
                return (Subject<TMessage>)stream;
            }
            public void Dispose()
            {
                foreach (var stream in _streams.Values) stream.Dispose();
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
