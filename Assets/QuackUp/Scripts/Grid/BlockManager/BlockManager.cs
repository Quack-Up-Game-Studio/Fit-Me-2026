using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using FitMe.Shared;
using MessagePipe;
using PrimeTween;
using QuackUp.SceneManagement;
using R3;
using Redcode.Extensions;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Grid
{
    public struct StartSpawnEvent
    {
        public readonly BlockPreset blockPreset;
        
        public StartSpawnEvent(BlockPreset blockPreset = null)
        {
            this.blockPreset = blockPreset;
        }
    }
    public class BlockManager : IDisposable, IStartable
    {
        #region Data Structures
        [Serializable]
        public record SpawnPointData
        {
            [field: SerializeField] public Transform Transform { get; private set; }

            [field: SerializeField, DisplayAsString] public bool IsFree { get; set; } = true;
            [field: SerializeField, Sirenix.OdinInspector.ReadOnly] public BlockModel CurrentBlock { get; set; }
        }
        
        private struct ShapeAndSchemaData
        {
            public readonly BlockShape blockShape;
            public readonly BlockSchema blockSchema;
            
            public ShapeAndSchemaData(BlockShape blockShape, BlockSchema blockSchema)
            {
                this.blockShape = blockShape;
                this.blockSchema = blockSchema;
            }
        }
        
        private struct BestFitResult
        {
            public readonly int vacantCount;
            public readonly List<ShapeAndSchemaData> schemaList;

            public BestFitResult(int vacantCount, List<ShapeAndSchemaData> schemaList)
            {
                this.vacantCount = vacantCount;
                this.schemaList = schemaList;
            }
        }
        #endregion
        
        #region Fields
        public static event Action OnGameOver;
        public static event Action<List<BlockModel>> OnBlockSpawned;

        private readonly GridManager _gridManager;
        private readonly SpawnPointData[] _spawnPoints;
        private readonly BlockManagerConfig _config;
        private readonly BlockFactory _blockFactory;
        private readonly ISubscriber<StartSpawnEvent> _startSpawnSubscription;
        
        private IDisposable _subscriptions;
        #endregion

        [Inject]
        public BlockManager(
            GridManager gridManager,
            BlockManagerConfig config,
            SpawnPointData[] spawnPoints,
            BlockFactory blockFactory,
            ISubscriber<StartSpawnEvent> startSpawnSubscription)
        {
            _gridManager = gridManager;
            _config = config;
            _spawnPoints = spawnPoints;
            _blockFactory = blockFactory;
            _startSpawnSubscription = startSpawnSubscription;
            Subscribe();
        }

        private void Subscribe()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            _startSpawnSubscription
                .Subscribe(OnSpawnAtStart)
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

        public void Start()
        {
            OnSpawnAtStart(new StartSpawnEvent());
        }

        private void OnSpawnAtStart(StartSpawnEvent eventData)
        {
            _spawnPoints.ForEach(FreeSpawnPoint);
            if (!eventData.blockPreset)
                SpawnRandomBlock();
            else
                SpawnBlock(eventData.blockPreset);
        }

        private void OnFitCheck(FitTypeEvent eventData)
        {
            if (_gridManager.CurrentSceneType != SceneType.Gameplay) return;
            FreeSpawnPoint(eventData.Block.SpawnIndex);
            ResetSpawnPoint();
            SpawnRandomBlock();
            if (eventData.FitType is FitType.None) 
                GameOverCheck().Forget();
        }
        #endregion
        
        #region Spawning
        /// <summary>
        /// Spawns random blocks at spawn points.
        /// </summary>
        public void SpawnRandomBlock()
        {
            //if (spawnPoints.Any(x => !x.IsFree)) return;
            var blockTypes = Enum.GetValues(typeof(BlockColor)).Cast<BlockColor>().ToList();
            var allSchemas = _config.BlockPresetDictionary
                .SelectMany(x => x.Value.BlockSchemas
                    .Select(schema => new ShapeAndSchemaData(x.Key, schema))).ToList();
            var shuffledSchemas = allSchemas.Shuffled().ToList();
            List<ShapeAndSchemaData> randomSchemas;
            _gridManager.CreateVacantSchema(out var vacantSchema, out var vacantCount);
            if (_config.UseSmartRandom && vacantCount <= _config.SmartRandomThreshold)
            {
                var bestFits = FindBestFitSorted(vacantSchema, shuffledSchemas);
                var bestFitSchemas = bestFits
                    .SelectMany(x => x.schemaList)
                    .Take(_config.MaxRandomAmount)
                    .ToList();
                var remainingAmount = _config.MaxRandomAmount - bestFitSchemas.Count;
                if (remainingAmount > 0)
                {
                    bestFitSchemas.AddRange(shuffledSchemas.Take(remainingAmount));
                }
                randomSchemas = bestFitSchemas.ToList();
            }
            else
            {
                randomSchemas = shuffledSchemas
                    .Take(_config.MaxRandomAmount)
                    .ToList();
            }
            
            
            var spawnedBlocks = new List<BlockModel>();
            for (int i = 0; i < randomSchemas.Count; i++)
            {
                if (!_spawnPoints[i].IsFree)
                {
                    continue;
                }
                Transform spawnTransform = _spawnPoints[i].Transform;
                var randomBlock = randomSchemas[i];
                var color = blockTypes.GetRandomElement();
                var face = randomBlock.blockShape;
                var index = randomBlock.blockSchema.Index;
                int randomRotation = index * 90;
                Debug.Log("Random Rotation: " + randomRotation);
                Quaternion randomRotationQuaternion = Quaternion.Euler(0f, 0f, randomRotation);
                //block.BlockView.Transform.rotation = randomRotationQuaternion;
                BlockModel block = _blockFactory.Create(face, spawnTransform.position, randomRotationQuaternion, out var blockGameObject, new InstantiateParameters
                {
                    parent = spawnTransform,
                    worldSpace = true,
                });
                blockGameObject.name = $"Block_{face}";
                block.ChangeType(color, false);
                block.SpawnIndex = i;
                block.BlockView.Transform.localScale = Vector3.zero;
                Vector3 scale = new Vector3(_config.ObjectScale, _config.ObjectScale, 1f);
                block.BlockView.ScaleIn(scale);
                _spawnPoints[i].IsFree = false;
                _spawnPoints[i].CurrentBlock = block;
                spawnedBlocks.Add(block);
            }
            if (spawnedBlocks.Count > 0)
                OnBlockSpawned?.Invoke(spawnedBlocks);
        }

        private void SpawnBlock(BlockPreset preset)
        {
            var spawnedBlocks = new List<BlockModel>();
            if (!_spawnPoints[0].IsFree) return;
            Transform spawnTransform = _spawnPoints[0].Transform;
            var blockTypes = Enum.GetValues(typeof(BlockColor)).Cast<BlockColor>().ToList();
            var blockType = blockTypes.GetRandomElement();
            var blockFace = _config.BlockPresetDictionary.FirstOrDefault(x => x.Value == preset).Key;
            BlockModel block = _blockFactory.Create(blockFace, spawnTransform.position, Quaternion.identity, out var blockGameObject, new InstantiateParameters
            {
                parent = spawnTransform
            });
            blockGameObject.name = $"Block_{blockFace}";
            block.ChangeType(blockType, false);
            block.SpawnIndex = 0;
            block.BlockView.Transform.localScale = Vector3.zero;
            Vector3 scale = new Vector3(_config.ObjectScale, _config.ObjectScale, 1f);
            block.BlockView.ScaleIn(scale);
            _spawnPoints[0].IsFree = false;
            _spawnPoints[0].CurrentBlock = block;
            spawnedBlocks.Add(block);
            if (spawnedBlocks.Count > 0)
                OnBlockSpawned?.Invoke(spawnedBlocks);
        }
        
        /// <summary>
        /// Finds the best fit for the vacant schema from the list of schemas to check. SORTED.
        /// </summary>
        /// <param name="vacantSchema"></param>
        /// <param name="schemasToCheck"></param>
        /// <returns></returns>
        private List<BestFitResult> FindBestFitSorted(int[,] vacantSchema,
                List<ShapeAndSchemaData> schemasToCheck)
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
            int[,] vacantSchema, List<ShapeAndSchemaData> schemasToCheck,
            List<ShapeAndSchemaData> previouslyTraversed = null, 
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
                var traversed = new List<ShapeAndSchemaData>();
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
                spawnPoint.CurrentBlock?.BlockView.Destroy();
                spawnPoint.CurrentBlock = null;
            }
        }

        public async UniTask GameOverCheck()
        {
            // if (_scaleTween.isAlive)
            // {
            //     await _scaleTween.ToUniTask();
            // }
            List<BlockModel> blockToCheck = _spawnPoints.Where(x => !x.IsFree).Select(spawnPoint => spawnPoint.CurrentBlock).ToList();
            if (!_gridManager.CheckAvailableBlock(blockToCheck, out _))
            {
                OnGameOver?.Invoke();
                GameStatic.CurrentGameState = GameState.GameOver;
            }
        }
        #endregion
    }
}
