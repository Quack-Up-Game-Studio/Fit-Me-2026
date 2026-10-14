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
        public IReadOnlyList<BlockInstance> BlockOnHand => _spawnPoints
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

        public Observable<Unit> OnSwap => _onSwap;
        private readonly Subject<Unit> _onSwap = new();
        private readonly ReactiveProperty<bool> _canSwap = new(false);
        private readonly ReadOnlyReactiveProperty<bool> _readOnlyCanSwap;
        private Queue<SpawnBlockData> _spawnBag = new();
        private readonly ShapeBlockSupplyQueue _shapeBlockQueue = new();
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
        #endregion

        [Inject]
        public BlockManager(
            GridManager gridManager,
            BlockManagerConfig config,
            [Key(PreviewTransformKey)] Transform previewTransform,
            SpawnPointData[] spawnPoints,
            BlockFactory blockFactory,
            [Key(PreviewTransformKey)] SpawnPointData[] spawnPreviews,
            [Key(BlockManagerMessageHub.MessageHubKey)] IMessageHub messageHub)
        {
            _gridManager = gridManager;
            _config = config;
            _spawnPoints = spawnPoints;
            _previewTransform = previewTransform;
            _blockFactory = blockFactory;
            _spawnPreviews = spawnPreviews;
            _messageHub = messageHub;
            _readOnlyCanSwap = _canSwap.ToReadOnlyReactiveProperty();
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
            _readOnlyCanSwap.Dispose();
            _canSwap.Dispose();
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
            FreeSpawnPoint(eventData.Block.Model.SpawnIndex);
            //ResetSpawnPoint();
            if (_shapeLevelRuntimeData != null)
                SpawnShapeBlocks();
            else
                SpawnBlocksFromBag(_config.CanRefill);
            if (eventData.FitType is FitType.None or FitType.Combo) 
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
                var randomSchema = _spawnBag.Dequeue();
                var block = InstantiateBlock(spawnTransform, randomSchema.rotation, randomSchema.blockShape, randomSchema.blockColor, _config.ObjectScale);
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
            var spawnedBlocks = new List<BlockInstance>();
            var spawnCount = Math.Min(_config.MaxRandomAmount, _spawnPoints.Length);
            for (var i = 0; i < spawnCount; i++)
            {
                if (!_spawnPoints[i].IsFree || !_shapeBlockQueue.TryTakeNext(out var playerBlock)) continue;

                var spawnPoint = _spawnPoints[i];
                if (playerBlock.PlayerBlock == null) continue;

                var schema = playerBlock.PlayerBlock.Schema;
                if (schema == null || schema.GetLength(0) == 0 || schema.GetLength(1) == 0)
                {
                    DebugUtils.LogWarning("Shape Mode skipped a player block with an empty schema.");
                    continue;
                }

                var block = InstantiateBlock(
                    spawnPoint.Transform,
                    playerBlock.Rotation,
                    BlockShape.OneByOne,
                    playerBlock.Color,
                    _config.ObjectScale,
                    schema,
                    true,
                    playerBlock.PlayerBlock.Preset);
                block.Model.SpawnIndex = i;
                spawnPoint.IsFree = false;
                spawnPoint.CurrentBlock = block;
                _activeShapeSupplyBySpawnIndex[i] = playerBlock;
                spawnedBlocks.Add(block);
            }

            if (spawnedBlocks.Count > 0)
                _messageHub.Publish(new BlockSpawnedEvent(spawnedBlocks));

            PreviewShapeQueue();
            RefreshSwapAvailability();
        }

        private BlockInstance InstantiateBlock(
            Transform spawnTransform,
            Quaternion rotation,
            BlockShape shape,
            BlockColor color,
            float objectScale,
            int[,] schemaOverride = null,
            bool allowRotation = true,
            BlockPreset presetOverride = null)
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
                presetOverride);
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
            if (!AllowSwapping) return;
            if (_shapeLevelRuntimeData != null)
            {
                SwapShapeBlock();
                return;
            }
            if (_spawnBag.Count == 0) return;
            var blockToSwap = BlockOnHand.FirstOrDefault();
            if (blockToSwap == null) return;
            if (blockToSwap.Controller.IsRotating) return;
            var previewBlock = BlockOnPreview.FirstOrDefault();
            if (previewBlock == null) return;
            var swapBlockData = new SpawnBlockData(blockToSwap.Model.BlockShape, 
                blockToSwap.GameObject.transform.rotation, 
                blockToSwap.Model.BlockPreset.BlockSchema,
                blockToSwap.Model.BlockColor.CurrentValue);
            var nextQueue = _spawnBag.Dequeue();
            var tempBag = _spawnBag.ToList();
            tempBag.Insert(0, swapBlockData);
            _spawnBag = new Queue<SpawnBlockData>(tempBag);
            blockToSwap.ViewModel.DestroyCommand.Execute(Unit.Default);
            var spawnIndex = blockToSwap.Model.SpawnIndex;
            var spawnPoint = _spawnPoints[spawnIndex];
            FreeSpawnPoint(spawnIndex);
            var newBlock = InstantiateBlock(spawnPoint.Transform, nextQueue.rotation, nextQueue.blockShape, nextQueue.blockColor, _config.ObjectScale);
            newBlock.Model.SpawnIndex = spawnIndex;
            spawnPoint.IsFree = false;
            spawnPoint.CurrentBlock = newBlock;
            _messageHub.Publish(new BlockSpawnedEvent(new List<BlockInstance>{newBlock}));
            previewBlock.ViewModel.DestroyCommand.Execute(Unit.Default);
            var newPreviewBlock = InstantiateBlock(_spawnPreviews[0].Transform, swapBlockData.rotation, swapBlockData.blockShape, swapBlockData.blockColor, _config.PreviewScale);
            newPreviewBlock.Controller.SetActive(false);
            _spawnPreviews[0].IsFree = false;
            _spawnPreviews[0].CurrentBlock = newPreviewBlock;
            //_previewBlocks[0] = newPreviewBlock;
            _onSwap?.OnNext(Unit.Default);
            //PreviewMultiNextQueue(_config.PreviewCount);
            RefreshSwapAvailability();
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
            _currentPreviewBlock = InstantiateBlock(_previewTransform, nextBlock.rotation, nextBlock.blockShape, nextBlock.blockColor, _config.PreviewScale);
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
                var block = InstantiateBlock(
                    _spawnPreviews[i].Transform,
                    nextBlocks.rotation,
                    nextBlocks.blockShape,
                    nextBlocks.blockColor,
                    _config.PreviewScale);

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
                    true,
                    entry.Preset);

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
                    Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0, 4) * 90f),
                    EnumUtils.RandomValue<BlockColor>()));
            }

            return items;
        }

        private void RefreshSwapAvailability()
        {
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
