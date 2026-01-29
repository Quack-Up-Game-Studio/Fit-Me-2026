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
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Random = UnityEngine.Random;

namespace FitMe.Grid
{
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
            public readonly BlockSchema blockSchema;
            public readonly BlockColor blockColor;
            
            public SpawnBlockData(BlockShape blockShape, BlockSchema blockSchema, BlockColor blockColor)
            {
                this.blockShape = blockShape;
                this.blockSchema = blockSchema;
                this.blockColor = blockColor;
            }
        }
        
        private struct BestFitResult
        {
            public readonly int vacantCount;
            public readonly List<SpawnBlockData> schemaList;

            public BestFitResult(int vacantCount, List<SpawnBlockData> schemaList)
            {
                this.vacantCount = vacantCount;
                this.schemaList = schemaList;
            }
        }
        #endregion
        
        #region Fields
        public const string PreviewTransformKey = "PreviewTransform";
        public static event Action OnGameOver;
        public static event Action<List<BlockInstance>> OnBlockSpawned;
        
        private readonly Queue<SpawnBlockData> _spawnBag = new();
        private readonly List<SpawnBlockData> _blockPool = new();
        private BlockInstance _currentPreviewBlock;
        
        private readonly GridManager _gridManager;
        private readonly SpawnPointData[] _spawnPoints;
        private readonly Transform _previewTransform;
        private readonly BlockManagerConfig _config;
        private readonly BlockFactory _blockFactory;
        private readonly IMessageHub _messageHub;
        
        private IDisposable _subscriptions;
        #endregion

        [Inject]
        public BlockManager(
            GridManager gridManager,
            BlockManagerConfig config,
            [Key(PreviewTransformKey)] Transform previewTransform,
            SpawnPointData[] spawnPoints,
            BlockFactory blockFactory,
            [Key(BlockManagerMessageHub.MessageHubKey)] IMessageHub messageHub)
        {
            _gridManager = gridManager;
            _config = config;
            _spawnPoints = spawnPoints;
            _previewTransform = previewTransform;
            _blockFactory = blockFactory;
            _messageHub = messageHub;
            Subscribe();
        }

        private void Subscribe()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            _messageHub
                .Subscribe<StartSpawnEvent>(OnSpawnAtStart)
                .AddTo(ref disposableBuilder);
            _gridManager.OnFitCheck
                .Subscribe(OnFitCheck)
                .AddTo(ref disposableBuilder);
            _subscriptions = disposableBuilder.Build();
        }

        public void Dispose()
        {
            _subscriptions?.Dispose();
        }

        #region Events

        private void OnSpawnAtStart(StartSpawnEvent eventData)
        {
            CreatePool();
            _spawnPoints.ForEach(FreeSpawnPoint);
            if (!eventData.BlockPreset)
                SpawnRandomBlock();
            else
                SpawnBlock(eventData.BlockPreset);
        }

        private void OnFitCheck(FitTypeEvent eventData)
        {
            if (_gridManager.CurrentSceneType != SceneType.Gameplay) return;
            FreeSpawnPoint(eventData.Block.Model.SpawnIndex);
            //ResetSpawnPoint();
            SpawnRandomBlock();
            if (eventData.FitType is FitType.None or FitType.Combo) 
                GameOverCheck().Forget();
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
                    _blockPool.Add(new SpawnBlockData((BlockShape)i, null, (BlockColor)j));
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
                    var template = shuffledTemplates[Random.Range(0, shuffledTemplates.Count)];
                    var randomSchema = possibleSchemas[Random.Range(0, possibleSchemas.Count)];

                    tempBag.Add(new SpawnBlockData(shape, randomSchema, template.blockColor));
                }
            }

            foreach (var data in tempBag.Shuffled())
            {
                _spawnBag.Enqueue(data);
            }
            DebugUtils.Log($"Yuirin: Bag Refilled from Pool! Total {_spawnBag.Count} items.");
        }
        
        /// <summary>
        /// Spawns random blocks at spawn points.
        /// </summary>
        public void SpawnRandomBlock()
        {
            var blockTypes = Enum.GetValues(typeof(BlockColor)).Cast<BlockColor>().ToList();
            if (_spawnBag.Count <= _config.MaxRandomAmount) 
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
                var index = Random.Range(0, 4);
                int randomRotation = index * 90;
                DebugUtils.Log("Random Rotation: " + randomRotation);
                Quaternion randomRotationQuaternion = Quaternion.Euler(0f, 0f, randomRotation);
                var block = InstantiateBlock(spawnTransform, randomRotationQuaternion, randomSchema, randomSchema.blockColor, _config.ObjectScale);
                block.Model.SpawnIndex = i;
                _spawnPoints[i].IsFree = false;
                _spawnPoints[i].CurrentBlock = block;
                spawnedBlocks.Add(block);
            }
            if (spawnedBlocks.Count > 0)
                _messageHub.Publish(new BlockSpawnedEvent(spawnedBlocks));
            PreviewNextQueue();
            DebugUtils.Log($"Yuirin: Refilled Bag! Now has {_spawnBag.Count} items.");
        }

        private BlockInstance InstantiateBlock(Transform spawnTransform, Quaternion rotation, SpawnBlockData randomSchema, BlockColor color, float objectScale)
        {
            var face = randomSchema.blockShape;
            //block.BlockView.Transform.rotation = randomRotationQuaternion;
            BlockInstance block = _blockFactory.Create(face, spawnTransform.position, rotation, new InstantiateParameters
            {
                parent = spawnTransform,
                worldSpace = true,
            });
            block.GameObject.name = $"Block_{face}";
            block.Model.ChangeType(color, false);
            block.GameObject.transform.localScale = Vector3.zero;
            Vector3 scale = new Vector3(objectScale, objectScale, 1f);
            var promise = new Promise<Unit>();
            block.ViewModel.ScaleInCommand.Execute(new ScaleInCommandData(promise, scale));
            return block;
        }

        private void SpawnBlock(BlockPreset preset)
        {
            var spawnedBlocks = new List<BlockInstance>();
            if (!_spawnPoints[0].IsFree) return;
            Transform spawnTransform = _spawnPoints[0].Transform;
            var blockTypes = Enum.GetValues(typeof(BlockColor)).Cast<BlockColor>().ToList();
            var color = blockTypes.GetRandomElement();
            var face = _config.BlockPresetDictionary.FirstOrDefault(x => x.Value == preset).Key;
            BlockInstance block = _blockFactory.Create(face, spawnTransform.position, Quaternion.identity, new InstantiateParameters
            {
                parent = spawnTransform
            });
            block.GameObject.name = $"Block_{face}";
            block.Model.ChangeType(color, false);
            block.Model.SpawnIndex = 0;
            block.GameObject.transform.localScale = Vector3.zero;
            Vector3 scale = new Vector3(_config.ObjectScale, _config.ObjectScale, 1f);
            var promise = new Promise<Unit>();
            block.ViewModel.ScaleInCommand.Execute(new ScaleInCommandData(promise, scale));
            _spawnPoints[0].IsFree = false;
            _spawnPoints[0].CurrentBlock = block;
            spawnedBlocks.Add(block);
            if (spawnedBlocks.Count > 0)
                _messageHub.Publish(new BlockSpawnedEvent(spawnedBlocks));
        }
        
        /// <summary>
        /// Finds the best fit for the vacant schema from the list of schemas to check. SORTED.
        /// </summary>
        /// <param name="vacantSchema"></param>
        /// <param name="schemasToCheck"></param>
        /// <returns></returns>
        private List<BestFitResult> FindBestFitSorted(int[,] vacantSchema,
                List<SpawnBlockData> schemasToCheck)
        {
            var sortedSchemas = schemasToCheck
                    .OrderByDescending(x => x.blockSchema.schema.CountMember(y => y == 1))
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
            _currentPreviewBlock?.ViewModel.DestroyCommand.Execute(Unit.Default);

            var nextBlock = _spawnBag.Peek(); 
            _currentPreviewBlock = InstantiateBlock(_previewTransform, Quaternion.identity, nextBlock, nextBlock.blockColor, _config.PreviewScale);
            _currentPreviewBlock.Controller.SetActive(false);
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
            int[,] vacantSchema, List<SpawnBlockData> schemasToCheck,
            List<SpawnBlockData> previouslyTraversed = null, 
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
                var traversed = new List<SpawnBlockData>();
                if (previouslyTraversed != null)
                {
                    traversed.AddRange(previouslyTraversed);
                }

                if (!ArrayHelper.CanBFitInA(vacantSchema, schema.blockSchema.schema, out var placedArray, true))
                    continue;
                traversed.Add(schema);
                var bestFits = FindBestFit(placedArray, schemasToCheck, traversed, currentDepth + 1, vacantToBeat,
                    blockCountToBeat);
                var best = bestFits
                    .OrderBy(x => x.vacantCount)
                    .ThenBy(x => x.schemaList.Count)
                    .FirstOrDefault();
                if (best.vacantCount >= vacantToBeat || best.schemaList.Count >= blockCountToBeat) 
                    continue;
                unsorted.Add(best);
                var bestUnsorted = unsorted.OrderBy(x => x.vacantCount).ThenBy(x => x.schemaList.Count)
                    .FirstOrDefault();
                vacantToBeat = bestUnsorted.vacantCount;
                blockCountToBeat = bestUnsorted.schemaList.Count;
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
            // if (_scaleTween.isAlive)
            // {
            //     await _scaleTween.ToUniTask();
            // }
            List<BlockModel> blockToCheck = _spawnPoints.Where(x => !x.IsFree).Select(spawnPoint => spawnPoint.CurrentBlock.Model).ToList();
            if (!_gridManager.CheckAvailableBlock(blockToCheck, out _))
            {
                _gridManager.CreateVacantSchema(out _, out var vacantCount);
                _messageHub.Publish(new NoPlaceableBlockEvent(vacantCount));    
                await _gridManager.RemoveAllBlocks(true);
            }
        }
        #endregion
    }
}
