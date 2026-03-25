using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using FitMe.Shared;
using MessagePipe;
using PrimeTween;
using QuackUp.SceneManagement;
using QuackUp.Utils;
using R3;
using Redcode.Extensions;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using Unity.Android.Gradle;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Random = UnityEngine.Random;

namespace FitMe.Grid
{
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
        
        private struct SpawnBlockData
        {
            public readonly BlockShape blockShape;
            public readonly Quaternion rotation;
            public readonly BlockSchema blockSchema;
            public readonly BlockColor blockColor;
            
            public SpawnBlockData(BlockShape blockShape, Quaternion rotation, BlockSchema blockSchema, BlockColor blockColor)
            {
                this.blockShape = blockShape;
                this.rotation = rotation;
                this.blockSchema = blockSchema;
                this.blockColor = blockColor;
            }
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
        public IReadOnlyList<BlockInstance> BlocksOnHand => _spawnPoints
            .Where(x => !x.IsFree)
            .Select(x => x.CurrentBlock)
            .ToList();
        
        private Queue<SpawnBlockData> _spawnBag = new();
        private readonly List<SpawnBlockData> _blockPool = new();
        private BlockInstance _currentPreviewBlock;
        private List<BlockInstance> _previewBlocks = new List<BlockInstance>();
         
        private readonly GridManager _gridManager;
        private readonly SpawnPointData[] _spawnPoints;
        private readonly List<Transform> _previewTransforms = new List<Transform>();
        private readonly Transform _previewTransform;
        private readonly GameObject _previewParent;
        private readonly BlockManagerConfig _config;
        private readonly BlockFactory _blockFactory;
        private readonly IMessageHub _messageHub;

        private int _smartRandomCount;
        
        private IDisposable _subscriptions;
        #endregion

        [Inject]
        public BlockManager(
            GridManager gridManager,
            BlockManagerConfig config,
            [Key(PreviewTransformKey)] Transform previewTransform,
            SpawnPointData[] spawnPoints,
            BlockFactory blockFactory,
            GameObject previewParent,
            [Key(BlockManagerMessageHub.MessageHubKey)] IMessageHub messageHub)
        {
            _gridManager = gridManager;
            _config = config;
            _spawnPoints = spawnPoints;
            _previewTransform = previewTransform;
            _blockFactory = blockFactory;
            _previewParent = previewParent;
            _messageHub = messageHub;
            Subscribe();
        }

        private void Subscribe()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            _messageHub
                .Subscribe<SpawnWithBlockPresetEvent>(OnSpawnAtStart)
                .AddTo(ref disposableBuilder);
            _messageHub
                .Subscribe<ContinueEvent>(_ => OnContinue())
                .AddTo(ref disposableBuilder);
            _gridManager.OnFitCheck
                .Subscribe(OnFitCheck)
                .AddTo(ref disposableBuilder);
            _gridManager.OnClearGrid
                .Subscribe(_ => ResetBag())
                .AddTo(ref disposableBuilder);
            _subscriptions = disposableBuilder.Build();
        }

        public void Dispose()
        {
            _subscriptions?.Dispose();
        }

        #region Events

        private void OnSpawnAtStart(SpawnWithBlockPresetEvent withBlockPresetEventData)
        {
            CreatePool();
            _spawnPoints.ForEach(FreeSpawnPoint);
            if (!withBlockPresetEventData.BlockPreset)
                SpawnBlocksFromBag(true);
            else
                SpawnBlock(withBlockPresetEventData);
        }

        private void OnFitCheck(FitTypeEvent eventData)
        {
            if (_gridManager.CurrentSceneType != SceneType.Gameplay) return;
            FreeSpawnPoint(eventData.Block.Model.SpawnIndex);
            //ResetSpawnPoint();
            SpawnBlocksFromBag(_config.CanRefill);
            if (eventData.FitType is FitType.None or FitType.Combo) 
                GameOverCheck().Forget();
        }

        private void OnContinue()
        {
            _smartRandomCount++;
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
            for (int i = 0; i < _spawnPoints.Length; i++)
            {
                if (_spawnPoints[i].IsFree) continue;
                _spawnPoints[i].CurrentBlock.ViewModel.DestroyCommand.Execute(Unit.Default);
                _spawnPoints[i].CurrentBlock = null;
                _spawnPoints[i].IsFree = true;
            }
            
            _currentPreviewBlock?.ViewModel.DestroyCommand.Execute(Unit.Default);
            _currentPreviewBlock = null;
        }
        
        public void ResetBag()
        {
            _spawnBag.Clear();
            ResetBlockInSlot();
            SpawnBlocksFromBag(true);
            Debug.LogWarning("Yuirin: Bag Reset!");
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
            var randomAmount = _config.MaxRandomAmount;
            for (int i = 0; i < randomAmount; i++)
            {
                if (!_spawnPoints[i].IsFree)
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
            DebugUtils.Log($"Yuirin: Now has {_spawnBag.Count} items in bag.");
        }

        private BlockInstance InstantiateBlock(Transform spawnTransform, Quaternion rotation, BlockShape shape, BlockColor color, float objectScale)
        {
            //block.BlockView.Transform.rotation = randomRotationQuaternion;
            BlockInstance block = _blockFactory.Create(shape, spawnTransform.position, rotation, new InstantiateParameters
            {
                parent = spawnTransform,
                worldSpace = true,
            });
            block.GameObject.name = $"Block_{shape}";
            block.Model.ChangeType(color, false);
            block.GameObject.transform.localScale = Vector3.zero;
            Vector3 scale = new Vector3(objectScale, objectScale, 1f);
            var promise = new Promise<Unit>();
            block.ViewModel.ScaleInCommand.Execute(new ScaleInCommandData(promise, scale));
            return block;
        }

        private void SpawnBlock(SpawnWithBlockPresetEvent data)
        {
            var spawnedBlocks = new List<BlockInstance>();
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
        }

        //Temporary method for testing swap mechanic
        public void Swap()
        {
            var blockToSwap = BlocksOnHand[0];
            if (blockToSwap == null) return;
            var previewBlock = _previewBlocks[0];
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
            var newPreviewBlock = InstantiateBlock(_previewTransforms[0], swapBlockData.rotation, swapBlockData.blockShape, swapBlockData.blockColor, _config.PreviewScale);
            newPreviewBlock.Controller.SetActive(false);
            _previewBlocks[0] = newPreviewBlock;
            //PreviewMultiNextQueue(_config.PreviewCount);
        }

        private void SmartRandom()
        {
            _gridManager.CreateVacantSchema(out var vacantSchema, out var vacantCount);
            if (vacantCount > _config.SmartRandomThreshold) return;
            var schemasToCheck = _config.BlockPresetDictionary.Values.SelectMany(x => x.BlockSchemas).ToList();
            List<BestFitResult> bestFitResults = new();
            var allSchemaOnHand = BlocksOnHand
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
        
        /// <summary>
        /// Finds the best fit for the vacant schema from the list of schemas to check. SORTED.
        /// </summary>
        /// <param name="vacantSchema"></param>
        /// <param name="schemasToCheck"></param>
        /// <returns></returns>
        private List<BestFitResult> FindBestFitSorted(int[,] vacantSchema,
                List<BlockSchema> schemasToCheck)
        {
            var sortedSchemas = schemasToCheck
                    .OrderByDescending(x => x.schema.CountMember(y => y == 1))
                    .ToList();
            var bestFits = FindBestFit(vacantSchema, sortedSchemas);
            bestFits = bestFits
                .OrderBy(x => x.vacantCount)
                .ThenBy(x => x.schemaList.Count)
                .ToList();
            return bestFits;
        }

        private void PreviewNextQueue()
        {
            if (_spawnBag.Count == 0) return;
            _currentPreviewBlock?.ViewModel.DestroyCommand.Execute(Unit.Default);
            
            var nextBlock = _spawnBag.Peek(); 
            _currentPreviewBlock = InstantiateBlock(_previewTransform, nextBlock.rotation, nextBlock.blockShape, nextBlock.blockColor, _config.PreviewScale);
            _currentPreviewBlock.Controller.SetActive(false);
        }

        private void CheckAndExpandPositions(int requiredCount)
        {
            for (var i = _previewTransforms.Count; i < requiredCount; i++)
            {
                var newPoint = new GameObject($"PreviewPoint_{i}");
        
                if (_previewParent) 
                {
                    newPoint.transform.SetParent(_previewParent.transform, false);
                    var spacing = _config.SpawnSpace;
                    newPoint.transform.localPosition = new Vector3(spacing * i, 0, 0);
                    
                    var scaleFactor = Mathf.Pow(0.8f, i);
                    newPoint.transform.localScale = Vector3.one * scaleFactor;
                }
                _previewTransforms.Add(newPoint.transform);
            }
        }
        
        private void PreviewMultiNextQueue(int previewCount)
        {
            CheckAndExpandPositions(previewCount);
            
            if (_spawnBag.Count == 0) return;
            
            for (var i = 0; i < _previewBlocks.Count; i++)
            {
                var previewBlock = _previewBlocks[i];
                if (previewBlock == null) continue;
                previewBlock.ViewModel.DestroyCommand.Execute(Unit.Default);
                _previewBlocks[i] = null;
            }
            
            var bagList = _spawnBag.ToList();
            
            Debug.Log(_previewTransforms.Count);
            _previewBlocks.Clear();
            for (int i = 0; i < _previewTransforms.Count; i++)
            {
                if (i >= _previewTransforms.Count || i >= bagList.Count) break;

                var nextBlocks = bagList[i];
        
                var block = InstantiateBlock(
                    _previewTransforms[i], 
                    nextBlocks.rotation, 
                    nextBlocks.blockShape, 
                    nextBlocks.blockColor, 
                    _config.PreviewScale
                );
        
                block.Controller.SetActive(false);
                _previewBlocks.Add(block);
            }
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
        }

        public void FreeSpawnPoint(SpawnPointData spawnPointData)
        {
            spawnPointData.IsFree = true;
            spawnPointData.CurrentBlock = null;
        }
        
        public void ResetSpawnPoint()
        {
            foreach (var spawnPoint in _spawnPoints)
            {
                spawnPoint.IsFree = true;
                spawnPoint.CurrentBlock?.ViewModel.DestroyCommand.Execute(Unit.Default);
                spawnPoint.CurrentBlock = null;
            }
        }

        
        
        public async UniTask GameOverCheck()
        {
            var blockToCheck = BlocksOnHand.Select(x => x.Model).ToList();
            if (!_gridManager.CheckAvailableBlock(blockToCheck, out _))
            {
                _gridManager.CreateVacantSchema(out _, out var vacantCount);
                _messageHub.Publish(new NoPlaceableBlockEvent(vacantCount));    
                _messageHub.Publish(new GameOverEvent(true));
            }
        }
        #endregion
    }
}
