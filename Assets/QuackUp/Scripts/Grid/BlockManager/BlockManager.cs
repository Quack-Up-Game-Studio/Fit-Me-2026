using System;
using Debug = QuackUp.Utils.DebugUtils;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using FitMe.Shared;
using QuackUp.Utils;
using R3;
using Redcode.Extensions;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using Random = UnityEngine.Random;

namespace FitMe.Grid
{
    [Serializable]
    public struct SpawnBlockData
    {
        [SerializeField] public BlockShape blockShape;
        [SerializeField] public Quaternion rotation;
        [HideInInspector] public BlockSchema blockSchema;
        [SerializeField] public BlockColor blockColor;
            
        public SpawnBlockData(BlockShape blockShape, Quaternion rotation, BlockSchema blockSchema, BlockColor blockColor)
        {
            this.blockShape = blockShape;
            this.rotation = rotation;
            this.blockSchema = blockSchema;
            this.blockColor = blockColor;
        }
    }

    public sealed class ShapeBlockSupplyQueue
    {
        public readonly struct Item
        {
            public ShapePlayerBlockRuntimeEntry PlayerBlock { get; }
            public Quaternion Rotation { get; }
            public BlockColor Color { get; }

            public Item(ShapePlayerBlockRuntimeEntry playerBlock, Quaternion rotation, BlockColor color)
            {
                PlayerBlock = playerBlock;
                Rotation = rotation;
                Color = color;
            }

            public Item WithAppearance(Quaternion rotation, BlockColor color) =>
                new(PlayerBlock, rotation, color);
        }

        private readonly LinkedList<Item> _upcoming = new();

        public int Count => _upcoming.Count;

        public void Reset(IEnumerable<Item> items)
        {
            _upcoming.Clear();
            if (items == null) return;

            foreach (var item in items)
            {
                var schema = item.PlayerBlock?.Schema;
                if (schema == null || schema.GetLength(0) == 0 || schema.GetLength(1) == 0)
                    continue;

                _upcoming.AddLast(item);
            }
        }

        public IReadOnlyList<Item> PeekNext(int count)
        {
            if (count <= 0 || _upcoming.Count == 0) return Array.Empty<Item>();

            var result = new List<Item>(Math.Min(count, _upcoming.Count));
            var node = _upcoming.First;
            while (node != null && result.Count < count)
            {
                result.Add(node.Value);
                node = node.Next;
            }

            return result;
        }

        public bool TryTakeNext(out Item item)
        {
            if (_upcoming.First == null)
            {
                item = default;
                return false;
            }

            item = _upcoming.First.Value;
            _upcoming.RemoveFirst();
            return true;
        }

        public bool TrySwapWithNext(Item activeItem, out Item incomingItem)
        {
            if (!TryTakeNext(out incomingItem)) return false;

            _upcoming.AddFirst(activeItem);
            return true;
        }
    }
    
    [Serializable]
    public class BlockManager : IDisposable
    {
        #region Data Structures
        [Serializable]
        public record SpawnPointData
        {
            [field: SerializeField] public Transform Transform { get; private set; }

            [field: SerializeField, DisplayAsString] public bool IsFree { get; set; } = true;
            [field: SerializeField, Sirenix.OdinInspector.ReadOnly] public BlockInstance CurrentBlock { get; set; }
        }
        
        private struct BestFitResult
        {
            public readonly int vacantCount;
            public readonly List<BlockSchema> schemaList;

            public BestFitResult(int vacantCount, List<BlockSchema> schemaList)
            {
                this.vacantCount = vacantCount;
                this.schemaList = schemaList;
            }
        }
        #endregion
        
        #region Debug
        [Button("Test Game Over")]
        private void TestGameOver()
        {
            _gridManager.CreateVacantSchema(out _, out var vacantCount);
            _messageHub.Publish(new NoPlaceableBlockEvent(vacantCount));    
            _messageHub.Publish(new GameOverEvent(true));
        }
        
        [Button("Test Swap")]
        private void TestSwap()        
        {
            Swap();
        }
        #endregion
        
        #region Fields
        public const string PreviewTransformKey = "PreviewTransform";
        [ShowInInspector, Sirenix.OdinInspector.ReadOnly]
        public IReadOnlyList<BlockInstance> BlockOnHand => _shapeLevelRuntimeData != null
            ? _trayBlocks.Where(x => x.GameObject && !x.Model.TrayPiece.IsPlaced).ToList()
            : _spawnPoints
            .Where(x => !x.IsFree)
            .Select(x => x.CurrentBlock)
            .ToList();
        [ShowInInspector, Sirenix.OdinInspector.ReadOnly]
        public IReadOnlyList<BlockInstance> BlockOnPreview => _spawnPreviews
            .Where(x => !x.IsFree)
            .Select(x => x.CurrentBlock)
            .ToList();

        private bool _allowSwapping = true;
        public bool AllowSwapping
        {
            get => _allowSwapping;
            set
            {
                _allowSwapping = value;
                RefreshSwapAvailability();
            }
        }

        public ReadOnlyReactiveProperty<bool> CanSwap => _readOnlyCanSwap;
        public ReadOnlyReactiveProperty<bool> CanRotate => _readOnlyCanRotate;

        public bool TryGetShapeHintSupply(out List<int[,]> schemas, out int activeHandCount)
        {
            schemas = new List<int[,]>();
            activeHandCount = 0;
            if (_shapeLevelRuntimeData == null) return false;

            if (_traySpawned || _trayBlocks.Count > 0)
            {
                foreach (var block in BlockOnHand)
                {
                    var blockSchemas = block.Model.BlockSchemas;
                    if (block.Model.TrayPiece == null || blockSchemas.Count == 0 || blockSchemas[0].schema == null)
                        return false;
                    schemas.Add((int[,])blockSchemas[0].schema.Clone());
                }

                activeHandCount = schemas.Count;
                return activeHandCount > 0;
            }

            if (_spawnPoints == null) return false;

            for (var spawnIndex = 0; spawnIndex < _spawnPoints.Length; spawnIndex++)
            {
                var spawnPoint = _spawnPoints[spawnIndex];
                if (spawnPoint == null || spawnPoint.IsFree) continue;
                if (!_activeShapeSupplyBySpawnIndex.TryGetValue(spawnIndex, out var activeSupply) ||
                    activeSupply.PlayerBlock?.Schema == null)
                    return false;
                schemas.Add((int[,])activeSupply.PlayerBlock.Schema.Clone());
            }

            activeHandCount = schemas.Count;
            if (activeHandCount == 0) return false;

            foreach (var queuedSupply in _shapeBlockQueue.PeekNext(_shapeBlockQueue.Count))
            {
                var schema = queuedSupply.PlayerBlock?.Schema;
                if (schema == null) return false;
                schemas.Add((int[,])schema.Clone());
            }
            return true;
        }

        public Observable<Unit> OnSwap => _onSwap;
        private readonly Subject<Unit> _onSwap = new();
        private readonly ReactiveProperty<bool> _canSwap = new(false);
        private readonly ReadOnlyReactiveProperty<bool> _readOnlyCanSwap;
        private readonly ReactiveProperty<bool> _canRotate = new(false);
        private readonly ReadOnlyReactiveProperty<bool> _readOnlyCanRotate;
        private Queue<SpawnBlockData> _spawnBag = new();
        private readonly ShapeBlockSupplyQueue _shapeBlockQueue = new();
        public const string ShapeTrayKey = "ShapeTray";
        private readonly RectTransform _trayPanel;
        private readonly List<BlockInstance> _trayBlocks = new();
        private bool _traySpawned;
        private readonly Dictionary<int, ShapeBlockSupplyQueue.Item> _activeShapeSupplyBySpawnIndex = new();
        private ShapeLevelRuntimeData _shapeLevelRuntimeData;
        private readonly List<SpawnBlockData> _blockPool = new();
        private BlockInstance _currentPreviewBlock;
        //private List<BlockInstance> _previewBlocks = new List<BlockInstance>();
         
        private readonly GridManager _gridManager;
        private readonly SpawnPointData[] _spawnPoints;
        private readonly Transform _previewTransform;
        private readonly SpawnPointData[] _spawnPreviews;
        private readonly BlockManagerConfig _config;
        private readonly BlockFactory _blockFactory;
        private readonly IMessageHub _messageHub;

        private int _smartRandomCount;
        private bool _continued;
        
        private IDisposable _subscriptions;
        private IDisposable _swapTargetRotationSubscription;
        private BlockInstance _rotationTrackedSwapTarget;
        private IDisposable _rotateTargetSubscriptions;
        private BlockInstance _rotationTrackedRotateTarget;
        private readonly IGameStateManager _gameStateManager;
        private ReadOnlyReactiveProperty<GameState> _gameState;
        private ReadOnlyReactiveProperty<bool> _isPaused;
        private IDisposable _gameStateSubscriptions;
        #endregion

        [Inject]
        public BlockManager(
            GridManager gridManager,
            BlockManagerConfig config,
            [Key(PreviewTransformKey)] Transform previewTransform,
            SpawnPointData[] spawnPoints,
            BlockFactory blockFactory,
            [Key(PreviewTransformKey)] SpawnPointData[] spawnPreviews,
            [Key(BlockManagerMessageHub.MessageHubKey)] IMessageHub messageHub,
            [Key(ShapeTrayKey)] RectTransform trayPanel = null,
            IGameStateManager gameStateManager = null)
        {
            _gridManager = gridManager;
            _config = config;
            _spawnPoints = spawnPoints;
            _previewTransform = previewTransform;
            _blockFactory = blockFactory;
            _spawnPreviews = spawnPreviews;
            _messageHub = messageHub;
            _trayPanel = trayPanel;
            _gameStateManager = gameStateManager;
            if (_trayPanel) _trayPanel.gameObject.SetActive(false);
            _readOnlyCanSwap = _canSwap.ToReadOnlyReactiveProperty();
            _readOnlyCanRotate = _canRotate.ToReadOnlyReactiveProperty();
            if (_gameStateManager != null)
            {
                _gameState = _gameStateManager.GameState;
                _isPaused = _gameStateManager.IsPaused;
                var builder = Disposable.CreateBuilder();
                _gameState.Subscribe(_ => RefreshRotateAvailability()).AddTo(ref builder);
                _isPaused.Subscribe(_ => RefreshRotateAvailability()).AddTo(ref builder);
                _gameStateSubscriptions = builder.Build();
            }
            Subscribe();
        }

        private void Subscribe()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            _messageHub
                .Subscribe<SpawnWithBlockPresetEvent>(OnSpawnAtStart)
                .AddTo(ref disposableBuilder);
            _messageHub
                .Subscribe<SpawnWithShapeLevelEvent>(OnSpawnWithShapeLevel)
                .AddTo(ref disposableBuilder);
            _messageHub
                .Subscribe<ContinueEvent>(_ => OnContinue())
                .AddTo(ref disposableBuilder);
            _gridManager.OnFitCheck
                .Subscribe(OnFitCheck)
                .AddTo(ref disposableBuilder);
            _gridManager.OnClearGrid
                .Subscribe(_ => ResetBag(_continued))
                .AddTo(ref disposableBuilder);
            _gridManager.OnCellsCreated
                .Subscribe(_ => OnCellsCreated())
                .AddTo(ref disposableBuilder);
            _subscriptions = disposableBuilder.Build();
        }

        public void Dispose()
        {
            _subscriptions?.Dispose();
            _swapTargetRotationSubscription?.Dispose();
            _rotateTargetSubscriptions?.Dispose();
            _gameStateSubscriptions?.Dispose();
            _gameState?.Dispose();
            _isPaused?.Dispose();
            _readOnlyCanSwap.Dispose();
            _canSwap.Dispose();
            _readOnlyCanRotate.Dispose();
            _canRotate.Dispose();
        }

        #region Events

        private void OnSpawnAtStart(SpawnWithBlockPresetEvent withBlockPresetEventData)
        {
            CreatePool();
           // _spawnPoints.ForEach(FreeSpawnPoint);
            if (withBlockPresetEventData.BlockPreset)
                SpawnBlock(withBlockPresetEventData);
        }

        private void OnSpawnWithShapeLevel(SpawnWithShapeLevelEvent eventData)
        {
            ResetBlockInSlot();
            _shapeLevelRuntimeData = eventData.RuntimeData;
            if (_trayPanel) _trayPanel.gameObject.SetActive(_shapeLevelRuntimeData != null);
            _shapeBlockQueue.Reset(CreateShapeBlockSupplyItems(_shapeLevelRuntimeData));
            RefreshSwapAvailability();
        }

        private void OnCellsCreated()
        {
            if (!_gridManager.IsGameplay) return;
            if (_shapeLevelRuntimeData != null)
                SpawnShapeBlocks();
            else
                SpawnBlocksFromBag(true);
        }

        private void OnFitCheck(FitTypeEvent eventData)
        {
            if (!_gridManager.IsGameplay) return;
            if (eventData.Block.Model.TrayPiece != null)
                eventData.Block.Model.TrayPiece.MarkPlaced();
            else
                FreeSpawnPoint(eventData.Block.Model.SpawnIndex);
            //ResetSpawnPoint();
            if (_shapeLevelRuntimeData != null)
                SpawnShapeBlocks();
            else
                SpawnBlocksFromBag(_config.CanRefill);
            if (_shapeLevelRuntimeData == null && (eventData.FitType is FitType.None or FitType.Combo))
                GameOverCheck().Forget();
        }

        private void OnContinue()
        {
            _smartRandomCount++;
            _continued = true;
        }
        #endregion
        
        #region Spawning
        private void CreatePool()
        {
            var blockShapeCount = Enum.GetValues(typeof(BlockShape)).Length; 
            var blockColorCount = Enum.GetValues(typeof(BlockColor)).Length;
            for (int i = 0; i < blockShapeCount; i++)
            {
                for (int j = 0; j < blockColorCount; j++)
                {
                    _blockPool.Add(new SpawnBlockData((BlockShape)i, Quaternion.Euler(0f, 0f, 0f),null, (BlockColor)j));
                }
            }
        }
        
        private void RefillBag()
        {
            var currentGridPreset = _gridManager.CurrentGridPreset;
            if (!currentGridPreset)
            {
                return;
            }
            if (currentGridPreset.OverrideBag)
            {
                var finalList = currentGridPreset.ShuffleBag ? 
                    currentGridPreset.SpawnBlockData.Shuffled().ToList() : 
                    currentGridPreset.SpawnBlockData;
                _spawnBag = new Queue<SpawnBlockData>(finalList);
                DebugUtils.Log($"Yuirin: Bag Refilled from Grid Preset! Total {_spawnBag.Count} items.");
                return;
            }
            if (_blockPool == null || _blockPool.Count == 0)  CreatePool();
            
            var tempBag = new List<SpawnBlockData>();

            foreach (var pair in _config.BagSettings)
            {
                var shape = pair.Key;
                var count = pair.Value;

                if (!_config.BlockPresetDictionary.TryGetValue(shape, out var preset)) continue;
                var shuffledTemplates = _blockPool
                    .Where(x => x.blockShape == shape)
                    .Shuffled()
                    .ToList();
        
                if (shuffledTemplates.Count == 0)
                {
                    DebugUtils.LogWarning($"Yuirin: Can't find Template for Shape {shape} in Pool!");
                    continue;
                }

                var possibleSchemas = preset.BlockSchemas;
                for (int i = 0; i < count; i++)
                {
                    var index = Random.Range(0, 4);
                    int randomRotation = index * 90;
                    Quaternion randomRotationQuaternion = Quaternion.Euler(0f, 0f, randomRotation);
                    
                    var template = shuffledTemplates[Random.Range(0, shuffledTemplates.Count)];
                    var randomSchema = possibleSchemas[Random.Range(0, possibleSchemas.Count)];

                    tempBag.Add(new SpawnBlockData(shape, randomRotationQuaternion,randomSchema, template.blockColor));
                }
            }

            foreach (var data in tempBag.Shuffled())
            {
                _spawnBag.Enqueue(data);
            }
            DebugUtils.Log($"Yuirin: Bag Refilled from Pool! Total {_spawnBag.Count} items.");
        }

        private void ResetBlockInSlot()
        {
            foreach (var block in _trayBlocks)
                if (block.GameObject && !block.Model.TrayPiece.IsPlaced)
                    block.ViewModel.DestroyCommand.Execute(Unit.Default);
            _trayBlocks.Clear();
            _traySpawned = false;
            Debug.LogWarning("BlockManager: Resetting blocks in spawn points and previews.");
            foreach (var spawnPoint in _spawnPoints)
            {
                if (spawnPoint.IsFree) continue;
                spawnPoint.CurrentBlock.ViewModel.DestroyCommand.Execute(Unit.Default);
                spawnPoint.CurrentBlock = null;
                spawnPoint.IsFree = true;
            }

            ClearPreviewSlots();
            _activeShapeSupplyBySpawnIndex.Clear();
            _currentPreviewBlock?.ViewModel.DestroyCommand.Execute(Unit.Default);
            _currentPreviewBlock = null;
        }
        
        public void ResetBag(bool shouldRespawn = false)
        {
            _spawnBag.Clear();
            ResetBlockInSlot();
            if (shouldRespawn)
            {
                Debug.LogWarning("BlockManager: Bag Respawned!");
                _continued = false;
                if (_shapeLevelRuntimeData != null)
                {
                    // Continue starts a fresh authored supply; swap order is preserved only during the run.
                    _shapeBlockQueue.Reset(CreateShapeBlockSupplyItems(_shapeLevelRuntimeData));
                    SpawnShapeBlocks();
                }
                else
                {
                    SpawnBlocksFromBag(true);
                }
            }
            else
            {
                RefreshSwapAvailability();
            }
            Debug.LogWarning("BlockManager: Bag Reset!");
        }
        
        /// <summary>
        /// Spawns blocks from the bag into the spawn points. If refill is true, it will check the bag count and refill if necessary.
        /// </summary>
        public void SpawnBlocksFromBag(bool refill)
        {
            if (_spawnBag.Count <= _config.MaxRandomAmount && refill)
            {
                RefillBag();
            }
            
            var spawnedBlocks = new List<BlockInstance>();
            var randomAmount = Math.Min(_config.MaxRandomAmount, _spawnPoints.Length);
            for (int i = 0; i < randomAmount; i++)
            {
                if (!_spawnPoints[i].IsFree || _spawnBag.Count == 0)
                {
                    continue;
                }
                Transform spawnTransform = _spawnPoints[i].Transform;
                var spawnData = _spawnBag.Dequeue();
                var block = InstantiateClassicBlock(spawnTransform, spawnData, _config.ObjectScale);
                block.Model.SpawnIndex = i;
                _spawnPoints[i].IsFree = false;
                _spawnPoints[i].CurrentBlock = block;
                spawnedBlocks.Add(block);
            }
            if (spawnedBlocks.Count > 0)
                _messageHub.Publish(new BlockSpawnedEvent(spawnedBlocks));
            if (_smartRandomCount > 0) SmartRandom();
            //PreviewNextQueue();
            PreviewMultiNextQueue(_config.PreviewCount);
            RefreshSwapAvailability();
            DebugUtils.Log($"Yuirin: Now has {_spawnBag.Count} items in bag.");
        }

        private void SpawnShapeBlocks()
        {
            if (_traySpawned || _blockFactory == null) return;
            if (!_trayPanel)
                throw new InvalidOperationException("Shape Mode requires the Gameplay Tray Panel bounds in BlockManagerInstaller.");
            _traySpawned = true;
            var count = _shapeBlockQueue.Count;
            var corners = new Vector3[4];
            _trayPanel.GetWorldCorners(corners);
            var panelBounds = Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
            var inset = _config.TrayInset;
            var bounds = new Rect(panelBounds.x + panelBounds.width * inset.x,
                panelBounds.y + panelBounds.height * inset.y,
                panelBounds.width * (1f - 2f * inset.x), panelBounds.height * (1f - 2f * inset.y));
            var items = _shapeBlockQueue.PeekNext(count);
            var pieces = ShapeTrayLayout.Create(items, bounds, _config.TrayBlockScale,
                _config.TrayTiltMin, _config.TrayTiltMax);
            var spawnedBlocks = new List<BlockInstance>();
            for (var i = 0; i < count; i++)
            {
                if (!_shapeBlockQueue.TryTakeNext(out var item)) continue;
                var schema = item.PlayerBlock.Schema;
                var piece = pieces[i];
                piece.DropBounds = panelBounds;
                var block = InstantiateBlock(_trayPanel, item.Rotation, BlockShape.OneByOne, item.Color,
                    piece.Scale, schema, false, item.PlayerBlock.Preset, _config.ArbitraryBlockConfig);
                block.Model.SpawnIndex = -1;
                block.Model.TrayPiece = piece;
                block.GameObject.transform.position = piece.Position;
                block.GameObject.transform.rotation = piece.TrayRotation;
                block.ViewModel.SetSortingOrderCommand.Execute(i);
                _trayBlocks.Add(block);
                spawnedBlocks.Add(block);
            }
            if (spawnedBlocks.Count > 0)
                _messageHub.Publish(new BlockSpawnedEvent(spawnedBlocks));
            ClearPreviewSlots();
            RefreshSwapAvailability();
        }

        private BlockInstance InstantiateClassicBlock(
            Transform spawnTransform,
            SpawnBlockData spawnData,
            float objectScale,
            bool allowRotation = true)
        {
            // Classic's root rotation orients both the visible block asset and the preset atom footprint.
            // Applying the queued rotated schema as well would rotate only the atoms and desync grid occupancy.
            return InstantiateBlock(
                spawnTransform,
                spawnData.rotation,
                spawnData.blockShape,
                spawnData.blockColor,
                objectScale,
                schemaOverride: null,
                allowRotation: allowRotation);
        }

        private BlockInstance InstantiateBlock(
            Transform spawnTransform,
            Quaternion rotation,
            BlockShape shape,
            BlockColor color,
            float objectScale,
            int[,] schemaOverride = null,
            bool allowRotation = true,
            BlockPreset presetOverride = null,
            IBlockPresentationConfig blockConfigOverride = null)
        {
            //block.BlockView.Transform.rotation = randomRotationQuaternion;
            BlockInstance block = _blockFactory.Create(
                shape,
                spawnTransform.position,
                rotation,
                new InstantiateParameters
                {
                    parent = spawnTransform,
                    worldSpace = true,
                },
                schemaOverride,
                presetOverride,
                blockConfigOverride);
            block.GameObject.name = $"Block_{shape}";
            block.Model.ChangeColor(color, false);
            block.Controller.AllowRotation = allowRotation;
            block.GameObject.transform.localScale = Vector3.zero;
            Vector3 scale = new Vector3(objectScale, objectScale, 1f);
            var promise = new Promise<Unit>();
            block.ViewModel.ScaleInCommand.Execute(new ScaleInCommandData(promise, scale));
            return block;
        }

        private void SpawnBlock(SpawnWithBlockPresetEvent data)
        {
            var spawnedBlocks = new List<BlockInstance>();
            if (_spawnPoints.Length == 0) return;
            if (!_spawnPoints[0].IsFree) return;
            var spawnTransform = _spawnPoints[0].Transform;
            var blockTypes = Enum.GetValues(typeof(BlockColor)).Cast<BlockColor>().ToList();
            var color = blockTypes.GetRandomElement();
            var face = _config.BlockPresetDictionary.FirstOrDefault(x => x.Value == data.BlockPreset).Key;
            var block = InstantiateBlock(spawnTransform, Quaternion.identity, face, color, _config.ObjectScale);
            block.Controller.AllowRotation = data.AllowRotation;
            _spawnPoints[0].IsFree = false;
            _spawnPoints[0].CurrentBlock = block;
            spawnedBlocks.Add(block);
            if (spawnedBlocks.Count > 0)
                _messageHub.Publish(new BlockSpawnedEvent(spawnedBlocks));
            RefreshSwapAvailability();
        }
        
        public void Swap()
        {
            if (!AllowSwapping || _shapeLevelRuntimeData != null) return;
            if (_spawnBag.Count == 0) return;
            var blockToSwap = BlockOnHand.FirstOrDefault();
            if (blockToSwap == null) return;
            if (blockToSwap.Controller.IsRotating) return;
            var previewBlock = BlockOnPreview.FirstOrDefault();
            if (previewBlock == null) return;
            var spawnIndex = blockToSwap.Model.SpawnIndex;
            if (spawnIndex < 0 || spawnIndex >= _spawnPoints.Length || _spawnPreviews == null || _spawnPreviews.Length == 0)
                return;
            var spawnPoint = _spawnPoints[spawnIndex];
            var previewPoint = _spawnPreviews[0];
            if (spawnPoint == null || previewPoint == null || spawnPoint.Transform == null || previewPoint.Transform == null)
                return;

            var activeSchema = blockToSwap.Model.BlockSchemas.FirstOrDefault();
            var displacedSchema = activeSchema?.schema != null
                ? new BlockSchema((int[,])activeSchema.schema.Clone(), activeSchema.Index)
                : blockToSwap.Model.BlockPreset.BlockSchema;
            var swapBlockData = new SpawnBlockData(blockToSwap.Model.BlockShape, 
                blockToSwap.GameObject.transform.rotation, 
                displacedSchema,
                blockToSwap.Model.BlockColor.CurrentValue);
            var nextQueue = _spawnBag.Dequeue();
            var tempBag = _spawnBag.ToList();
            tempBag.Insert(0, swapBlockData);
            _spawnBag = new Queue<SpawnBlockData>(tempBag);

            blockToSwap.ViewModel.DestroyCommand.Execute(Unit.Default);
            FreeSpawnPoint(spawnIndex);
            var newBlock = InstantiateClassicBlock(spawnPoint.Transform, nextQueue, _config.ObjectScale);
            newBlock.Model.SpawnIndex = spawnIndex;
            spawnPoint.IsFree = false;
            spawnPoint.CurrentBlock = newBlock;
            _messageHub.Publish(new BlockSpawnedEvent(new List<BlockInstance> { newBlock }));

            previewBlock.ViewModel.DestroyCommand.Execute(Unit.Default);
            var newPreviewBlock = InstantiateClassicBlock(
                previewPoint.Transform,
                swapBlockData,
                _config.PreviewScale,
                allowRotation: false);
            newPreviewBlock.Controller.SetActive(false);
            previewPoint.IsFree = false;
            previewPoint.CurrentBlock = newPreviewBlock;
            _onSwap?.OnNext(Unit.Default);
            RefreshSwapAvailability();
        }

        public void Rotate()
        {
            RefreshRotateAvailability();
            if (!_canRotate.CurrentValue) return;

            BlockOnHand.FirstOrDefault()?.Controller.RequestRotation();
        }

        private void SwapShapeBlock()
        {
            if (_shapeBlockQueue.Count == 0 || _spawnPoints == null || _spawnPreviews == null ||
                _spawnPreviews.Length == 0)
                return;

            // The swap control is global, so target the first occupied hand slot in configured order.
            var blockToSwap = BlockOnHand.FirstOrDefault();
            var previewPoint = _spawnPreviews[0];
            var previewBlock = previewPoint?.CurrentBlock;
            if (blockToSwap == null || blockToSwap.Controller.IsRotating || previewPoint == null ||
                previewPoint.IsFree || previewBlock == null)
                return;

            var spawnIndex = blockToSwap.Model.SpawnIndex;
            if (spawnIndex < 0 || spawnIndex >= _spawnPoints.Length)
                return;

            var spawnPoint = _spawnPoints[spawnIndex];
            if (spawnPoint == null || spawnPoint.IsFree ||
                !ReferenceEquals(spawnPoint.CurrentBlock, blockToSwap) ||
                spawnPoint.Transform == null || previewPoint.Transform == null)
                return;

            if (!_activeShapeSupplyBySpawnIndex.TryGetValue(spawnIndex, out var activeSupply) ||
                activeSupply.PlayerBlock == null || activeSupply.PlayerBlock.Schema == null)
                return;

            activeSupply = activeSupply.WithAppearance(
                blockToSwap.GameObject.transform.rotation,
                blockToSwap.Model.BlockColor.CurrentValue);
            if (!_shapeBlockQueue.TrySwapWithNext(
                    activeSupply,
                    out var incomingSupply))
                return;

            _activeShapeSupplyBySpawnIndex[spawnIndex] = incomingSupply;

            MoveBlockToSpawnPoint(previewBlock, spawnPoint, _config.ObjectScale, true);
            previewBlock.Model.SpawnIndex = spawnIndex;
            spawnPoint.CurrentBlock = previewBlock;
            spawnPoint.IsFree = false;

            MoveBlockToSpawnPoint(blockToSwap, previewPoint, _config.PreviewScale, false);
            previewPoint.CurrentBlock = blockToSwap;
            previewPoint.IsFree = false;

            _messageHub.Publish(new BlockSpawnedEvent(new List<BlockInstance> { previewBlock }));
            _onSwap?.OnNext(Unit.Default);
            RefreshSwapAvailability();
        }

        private void MoveBlockToSpawnPoint(BlockInstance block, SpawnPointData spawnPoint, float scale, bool isActive)
        {
            var blockTransform = block.GameObject.transform;
            blockTransform.SetParent(spawnPoint.Transform, true);
            blockTransform.position = spawnPoint.Transform.position;
            block.ViewModel?.SetSpawnReturnTarget(spawnPoint.Transform);
            blockTransform.localScale = Vector3.zero;
            block.ViewModel.ScaleInCommand.Execute(new ScaleInCommandData(
                new Promise<Unit>(),
                new Vector3(scale, scale, 1f)));
            block.Controller.SetActive(isActive);
        }

        private void SmartRandom()
        {
            _gridManager.CreateVacantSchema(out var vacantSchema, out var vacantCount);
            if (vacantCount > _config.SmartRandomThreshold) return;
            var schemasToCheck = _config.BlockPresetDictionary.Values.SelectMany(x => x.BlockSchemas).ToList();
            List<BestFitResult> bestFitResults = new();
            var allSchemaOnHand = BlockOnHand
                .SelectMany(x => x.Model.BlockPreset.DistinctBlockSchemas)
                .Where(x => x != null)
                .ToList();
            foreach (var block in allSchemaOnHand)
            {
                if (!ArrayHelper.CanBFitInA(vacantSchema, block.schema, out var placedArray, true)) continue;
                var bestFits = FindBestFit(placedArray, schemasToCheck);
                bestFitResults.AddRange(bestFits);
            }
            if (bestFitResults.Count == 0) return;
            DebugUtils.Log($"Smart Random: Found {bestFitResults.Count} best fits with vacant count {bestFitResults[0].vacantCount}");
            var bestFit = bestFitResults.GetRandomElement();
            if (bestFit.schemaList.Count == 0) return;
            DebugUtils.Log($"Smart Random: Best fit has {bestFit.schemaList.Count} schemas. Vacant count: {bestFit.vacantCount}");
            var schema = bestFit.schemaList.GetRandomElement();
            var shape = _config.BlockPresetDictionary.FirstOrDefault(p => p.Value.BlockSchemas.Contains(schema)).Key;
            var color = EnumUtils.RandomValue<BlockColor>();
            var rotation = Quaternion.Euler(0f, 0f, schema.Index * 90f);
            var spawnData = new SpawnBlockData(shape, rotation, schema, color);
            //insert the smart random block at the front of the queue
            var queueList = _spawnBag.ToList();
            queueList.Insert(0, spawnData);
            _spawnBag = new Queue<SpawnBlockData>(queueList);
            DebugUtils.Log($"Smart Random activated! Vacant Count: {vacantCount}, Inserted Shape: {shape}, Rotation: {rotation.eulerAngles.z}");
            _smartRandomCount--;
        }
        
        private void PreviewNextQueue()
        {
            if (_spawnBag.Count == 0) return;
            _currentPreviewBlock?.ViewModel.DestroyCommand.Execute(Unit.Default);
            
            var nextBlock = _spawnBag.Peek();
            _currentPreviewBlock = InstantiateClassicBlock(_previewTransform, nextBlock, _config.PreviewScale, allowRotation: false);
            _currentPreviewBlock.Controller.SetActive(false);
        }
        
        private void PreviewMultiNextQueue(int previewCount)
        {
            ClearPreviewSlots();
            if (_spawnPreviews == null || _spawnPreviews.Length == 0)
            {
                RefreshSwapAvailability();
                return;
            }
            if (_spawnBag.Count == 0)
            {
                RefreshSwapAvailability();
                return;
            }

            var bagList = _spawnBag.ToList();
            var count = Math.Min(Math.Min(previewCount, _spawnPreviews.Length), bagList.Count);
            for (var i = 0; i < count; i++)
            {
                var nextBlocks = bagList[i];
                var block = InstantiateClassicBlock(
                    _spawnPreviews[i].Transform,
                    nextBlocks,
                    _config.PreviewScale,
                    allowRotation: false);

                block.Controller.SetActive(false);
                _spawnPreviews[i].CurrentBlock = block;
                _spawnPreviews[i].IsFree = false;
            }

            RefreshSwapAvailability();
        }

        private void PreviewShapeQueue()
        {
            ClearPreviewSlots();
            if (_spawnPreviews == null || _spawnPreviews.Length == 0 || _shapeBlockQueue.Count == 0)
            {
                RefreshSwapAvailability();
                return;
            }

            var previewItems = _shapeBlockQueue.PeekNext(_config.PreviewCount);
            var count = Math.Min(previewItems.Count, _spawnPreviews.Length);
            for (var i = 0; i < count; i++)
            {
                var item = previewItems[i];
                var entry = item.PlayerBlock;
                var schema = entry?.Schema;
                if (schema == null || schema.GetLength(0) == 0 || schema.GetLength(1) == 0)
                    break;

                var spawnPoint = _spawnPreviews[i];
                var block = InstantiateBlock(
                    spawnPoint.Transform,
                    item.Rotation,
                    BlockShape.OneByOne,
                    item.Color,
                    _config.PreviewScale,
                    schema,
                    false,
                    entry.Preset,
                    _config.ArbitraryBlockConfig);

                block.Controller.SetActive(false);
                spawnPoint.CurrentBlock = block;
                spawnPoint.IsFree = false;
            }

            RefreshSwapAvailability();
        }

        private void ClearPreviewSlots()
        {
            if (_spawnPreviews == null)
                return;

            foreach (var spawnPoint in _spawnPreviews)
            {
                if (spawnPoint == null)
                    continue;

                spawnPoint.CurrentBlock?.ViewModel.DestroyCommand.Execute(Unit.Default);
                spawnPoint.CurrentBlock = null;
                spawnPoint.IsFree = true;
            }
        }

        private static IEnumerable<ShapeBlockSupplyQueue.Item> CreateShapeBlockSupplyItems(
            ShapeLevelRuntimeData runtimeData)
        {
            if (runtimeData?.PlayerBlockEntries == null)
                return Array.Empty<ShapeBlockSupplyQueue.Item>();

            var items = new List<ShapeBlockSupplyQueue.Item>(runtimeData.PlayerBlockEntries.Count);
            foreach (var entry in runtimeData.PlayerBlockEntries)
            {
                var schema = entry?.Schema;
                if (schema == null || schema.GetLength(0) == 0 || schema.GetLength(1) == 0)
                {
                    DebugUtils.LogWarning("Shape Mode skipped a player block with an empty schema.");
                    continue;
                }

                items.Add(new ShapeBlockSupplyQueue.Item(
                    entry,
                    Quaternion.identity,
                    EnumUtils.RandomValue<BlockColor>()));
            }

            return items;
        }

        private void RefreshSwapAvailability()
        {
            RefreshRotateAvailability();
            if (_shapeLevelRuntimeData != null)
            {
                TrackSwapTargetRotation(null);
                _canSwap.Value = false;
                return;
            }
            var activeBlock = _spawnPoints == null ? null : BlockOnHand.FirstOrDefault();
            TrackSwapTargetRotation(activeBlock);

            var spawnIndex = activeBlock?.Model.SpawnIndex ?? -1;
            var targetSpawnPoint = _spawnPoints != null && spawnIndex >= 0 && spawnIndex < _spawnPoints.Length
                ? _spawnPoints[spawnIndex]
                : null;
            var hasHandBlock = activeBlock != null && activeBlock.Controller != null &&
                               !activeBlock.Controller.IsRotating && targetSpawnPoint != null &&
                               !targetSpawnPoint.IsFree && ReferenceEquals(targetSpawnPoint.CurrentBlock, activeBlock) &&
                               targetSpawnPoint.Transform != null;
            if (_shapeLevelRuntimeData != null)
                hasHandBlock &= _activeShapeSupplyBySpawnIndex.TryGetValue(spawnIndex, out var activeSupply) &&
                                IsValidShapeSupply(activeSupply);

            var previewPoint = _spawnPreviews != null && _spawnPreviews.Length > 0 ? _spawnPreviews[0] : null;
            var hasPreview = previewPoint != null && !previewPoint.IsFree &&
                             previewPoint.CurrentBlock != null && previewPoint.Transform != null;
            var hasUpcomingBlock = _shapeLevelRuntimeData != null
                ? _shapeBlockQueue.Count > 0
                : _spawnBag.Count > 0;
            var canSwap = AllowSwapping && hasHandBlock && hasPreview && hasUpcomingBlock;
            if (_canSwap.Value != canSwap)
                _canSwap.Value = canSwap;
        }

        private void TrackSwapTargetRotation(BlockInstance activeBlock)
        {
            if (ReferenceEquals(_rotationTrackedSwapTarget, activeBlock))
                return;

            _swapTargetRotationSubscription?.Dispose();
            _swapTargetRotationSubscription = null;
            _rotationTrackedSwapTarget = activeBlock;
            if (activeBlock?.Controller == null)
                return;

            _swapTargetRotationSubscription = activeBlock.Controller.OnRotationStateChanged
                .Subscribe(_ => RefreshSwapAvailability());
        }

        private void RefreshRotateAvailability()
        {
            var activeBlock = _shapeLevelRuntimeData == null && _spawnPoints != null
                ? BlockOnHand.FirstOrDefault()
                : null;
            TrackRotateTarget(activeBlock);

            var spawnIndex = activeBlock?.Model.SpawnIndex ?? -1;
            var spawnPoint = _spawnPoints != null && spawnIndex >= 0 && spawnIndex < _spawnPoints.Length
                ? _spawnPoints[spawnIndex]
                : null;
            var hasActiveBlock = activeBlock != null && activeBlock.GameObject &&
                                 activeBlock.Controller != null && activeBlock.Controller.AllowRotation &&
                                 !activeBlock.Controller.IsRotating &&
                                 activeBlock.ViewModel != null &&
                                 activeBlock.ViewModel.BlockInteractionState.CurrentValue != BlockInteractionState.PlacedOnGrid &&
                                 spawnPoint != null && !spawnPoint.IsFree &&
                                 ReferenceEquals(spawnPoint.CurrentBlock, activeBlock) && spawnPoint.Transform != null;
            var canRotate = hasActiveBlock &&
                            (_gameState == null || _gameState.CurrentValue == GameState.PlaceBlock) &&
                            (_isPaused == null || !_isPaused.CurrentValue);
            if (_canRotate.Value != canRotate)
                _canRotate.Value = canRotate;
        }

        private void TrackRotateTarget(BlockInstance activeBlock)
        {
            if (ReferenceEquals(_rotationTrackedRotateTarget, activeBlock))
                return;

            _rotateTargetSubscriptions?.Dispose();
            _rotateTargetSubscriptions = null;
            _rotationTrackedRotateTarget = activeBlock;
            if (activeBlock?.Controller == null || activeBlock.ViewModel == null)
                return;

            var builder = Disposable.CreateBuilder();
            activeBlock.Controller.OnRotationStateChanged
                .Subscribe(_ => RefreshRotateAvailability())
                .AddTo(ref builder);
            activeBlock.ViewModel.BlockInteractionState
                .Subscribe(_ => RefreshRotateAvailability())
                .AddTo(ref builder);
            _rotateTargetSubscriptions = builder.Build();
        }

        private static bool IsValidShapeSupply(ShapeBlockSupplyQueue.Item supply)
        {
            var schema = supply.PlayerBlock?.Schema;
            return schema != null && schema.GetLength(0) > 0 && schema.GetLength(1) > 0;
        }
        
        /// <summary>
        /// Returns the best fit for the vacant schema from the list of schemas to check. UNSORTED.
        /// </summary>
        /// <param name="vacantSchema"></param>
        /// <param name="schemasToCheck"></param>
        /// <param name="previouslyTraversed"></param>
        /// <param name="currentDepth"></param>
        /// <param name="vacantToBeat"></param>
        /// <param name="blockCountToBeat"></param>
        /// <returns></returns>
        private List<BestFitResult> FindBestFit(
            int[,] vacantSchema, List<BlockSchema> schemasToCheck,
            List<BlockSchema> previouslyTraversed = null, 
            int currentDepth = 0,
            int vacantToBeat = int.MaxValue, 
            int blockCountToBeat = int.MaxValue)
        {
            List<BestFitResult> unsorted = new();
            var vacantCount = vacantSchema.CountMember(x => x == 1);
            if (currentDepth >= _config.SmartRandomDepth)
            {
                if (previouslyTraversed != null)
                {
                    unsorted.Add(new BestFitResult(vacantCount, previouslyTraversed));
                }

                return unsorted;
            }

            if (previouslyTraversed != null && vacantCount >= vacantToBeat &&
                previouslyTraversed.Count + 1 >= blockCountToBeat)
            {
                unsorted.Add(new BestFitResult(vacantCount, previouslyTraversed));
                return unsorted;
            }

            foreach (var schema in schemasToCheck)
            {
                var traversed = new List<BlockSchema>();
                if (previouslyTraversed != null)
                {
                    traversed.AddRange(previouslyTraversed);
                }

                if (!ArrayHelper.CanBFitInA(vacantSchema, schema.schema, out var placedArray, true))
                    continue;
                traversed.Add(schema);
                var bestFits = FindBestFit(placedArray, schemasToCheck, traversed, currentDepth + 1, vacantToBeat,
                    blockCountToBeat);
                if (bestFits.Count == 0) continue;
                var bestFitResults = bestFits
                    .GroupBy(x => new { x.vacantCount, x.schemaList.Count })
                    .OrderBy(x => x.Key.vacantCount)
                    .ThenBy(x => x.Key.Count)
                    .First()
                    .ToList();
                if (bestFitResults.Count == 0) continue;
                var bestVacantCount = bestFitResults.First().vacantCount;
                var bestBlockCount = bestFitResults.First().schemaList.Count;
                if (bestVacantCount> vacantToBeat || bestBlockCount > blockCountToBeat) 
                    continue;
                unsorted.AddRange(bestFitResults);
                vacantToBeat = bestVacantCount;
                blockCountToBeat = bestBlockCount;
            }

            if (previouslyTraversed != null && unsorted.Count == 0) 
                unsorted.Add(new BestFitResult(vacantCount, previouslyTraversed));
            return unsorted;
        }
        #endregion
        
        #region Utils
        public void FreeSpawnPoint(int index)
        {
            _spawnPoints[index].IsFree = true;
            _spawnPoints[index].CurrentBlock = null;
            _activeShapeSupplyBySpawnIndex.Remove(index);
        }

        public void FreeSpawnPoint(SpawnPointData spawnPointData)
        {
            spawnPointData.IsFree = true;
            spawnPointData.CurrentBlock = null;
        }
        
        public void ResetSpawnPoint()
        {
            var isShapeMode = _shapeLevelRuntimeData != null;
            if (isShapeMode)
                ReturnActiveShapeSupplyToQueue();

            _activeShapeSupplyBySpawnIndex.Clear();
            if (_spawnPoints != null)
            {
                foreach (var spawnPoint in _spawnPoints)
                {
                    spawnPoint.IsFree = true;
                    spawnPoint.CurrentBlock?.ViewModel.DestroyCommand.Execute(Unit.Default);
                    spawnPoint.CurrentBlock = null;
                }
            }

            if (isShapeMode)
            {
                ClearPreviewSlots();
                if (_blockFactory != null)
                    PreviewShapeQueue();
            }

            RefreshSwapAvailability();
        }

        private void ReturnActiveShapeSupplyToQueue()
        {
            if (_spawnPoints == null)
                return;

            var returnedItems = new List<ShapeBlockSupplyQueue.Item>();
            for (var i = 0; i < _spawnPoints.Length; i++)
            {
                var spawnPoint = _spawnPoints[i];
                if (spawnPoint.IsFree || spawnPoint.CurrentBlock == null ||
                    !_activeShapeSupplyBySpawnIndex.TryGetValue(i, out var supply))
                    continue;

                var block = spawnPoint.CurrentBlock;
                returnedItems.Add(supply.WithAppearance(
                    block.GameObject.transform.rotation,
                    block.Model.BlockColor.CurrentValue));
            }

            if (returnedItems.Count == 0)
                return;

            returnedItems.AddRange(_shapeBlockQueue.PeekNext(_shapeBlockQueue.Count));
            _shapeBlockQueue.Reset(returnedItems);
        }
        
        
        public UniTask GameOverCheck()
        {
            if (_shapeLevelRuntimeData != null)
                return UniTask.CompletedTask;

            var blocksToCheck = BlockOnHand.Select(x => x.Model).ToList();
            
            var blockOnSwap = BlockOnPreview.FirstOrDefault();
            if (blockOnSwap != null)
            {
                blocksToCheck.Add(blockOnSwap.Model);
            }
            
            if (!_gridManager.CheckAvailableBlock(blocksToCheck, out _))
            {
                _gridManager.CreateVacantSchema(out _, out var vacantCount);
                _messageHub.Publish(new NoPlaceableBlockEvent(vacantCount));    
                _messageHub.Publish(new GameOverEvent(true));
            }
            return UniTask.CompletedTask;
        }
        #endregion
    }
}
