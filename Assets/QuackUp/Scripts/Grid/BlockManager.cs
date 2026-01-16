using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using MadDuck.Scripts.Units;
using MadDuck.Scripts.Utils;
using MessagePipe;
using PrimeTween;
using Redcode.Extensions;
using Sherbert.Framework.Generic;
using Sirenix.OdinInspector;
using Unity.VisualScripting;
using UnityCommunity.UnitySingleton;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.U2D.Animation;
using VContainer;
using VContainer.Unity;
using Random = UnityEngine.Random;

namespace MadDuck.Scripts.Managers
{
    public struct BlockPresetRequest{}

    public struct StartSpawnEvent
    {
        public readonly BlockPreset blockPreset;
        
        public StartSpawnEvent(BlockPreset blockPreset = null)
        {
            this.blockPreset = blockPreset;
        }
    }
    public class BlockManager : MonoSingleton<BlockManager>,
        IRequestHandler<BlockPresetRequest, List<BlockPreset>>
    {
        #region Data Structures
        [Serializable]
        public record SpawnPoint
        {
            [field: SerializeField] public Transform Transform { get; private set; }

            [field: SerializeField, DisplayAsString] public bool IsFree { get; set; } = true;
            [field: SerializeField, Sirenix.OdinInspector.ReadOnly] public Block CurrentBlock { get; set; }
        }
        
        private struct FaceAndSchemaData
        {
            public readonly string blockFace;
            public readonly BlockSchema blockSchema;
            
            public FaceAndSchemaData(string blockFace, BlockSchema blockSchema)
            {
                this.blockFace = blockFace;
                this.blockSchema = blockSchema;
            }
        }
        
        private struct BestFitResult
        {
            public readonly int vacantCount;
            public readonly List<FaceAndSchemaData> schemaList;

            public BestFitResult(int vacantCount, List<FaceAndSchemaData> schemaList)
            {
                this.vacantCount = vacantCount;
                this.schemaList = schemaList;
            }
        }
        #endregion

        #region Inspectors

        [Title("Random References")] 
        [SerializeField] private Block blockPrefab;
        [field: SerializeField] public SerializableDictionary<string, BlockView> BlockViewDictionary { get; private set; } = new();
        [field: SerializeField] public SerializableDictionary<BlockTypes, Color> AtomColorDictionary { get; private set; } = new();
        [SerializeField] [DictionaryDrawerSettings(KeyLabel = "Block Face", ValueLabel = "Block Preset")] 
        private SerializableDictionary<string, BlockPreset> blockPresetDictionary = new();
        [SerializeField] private SpawnPoint[] spawnPoints;

        [Title("Random Settings")]
        [SerializeField] private int maxRandomAmount = 3;
        [SerializeField] private bool smartRandom = true;
        [SerializeField, ShowIf(nameof(smartRandom)), MinValue(1)] private int smartRandomThreshold = 6;
        [SerializeField, ShowIf(nameof(smartRandom)), MinValue(1)] private int smartRandomDepth = 1;
        [SerializeField] private float objectScale = 0.5f;
        #endregion
        
        #region Fields
        private Tween _scaleTween;
        private readonly Dictionary<string, Block> _blockPool = new();
        public static event Action OnGameOver;
        public static event Action<List<Block>> OnBlockSpawned;
        private IDisposable _startSpawnSubscription;
        #endregion
        
        #region Events
        private void OnEnable()
        {
            _startSpawnSubscription = GlobalMessagePipe.GetSubscriber<StartSpawnEvent>()
                .Subscribe(SpawnAtStart);
        }

        private void OnDisable()
        {
            _startSpawnSubscription?.Dispose();
        }
        
        private void SpawnAtStart(StartSpawnEvent eventData)
        {
            SetupPool();
            spawnPoints.ForEach(FreeSpawnPoint);
            if (!eventData.blockPreset)
                SpawnRandomBlock();
            else
                SpawnBlock(eventData.blockPreset);
        }
        
        private void SetupPool()
        {
           foreach (var pair in blockPresetDictionary)
           {
               if (_blockPool.ContainsKey(pair.Key)) continue;
               var block = _objectResolver.Instantiate(blockPrefab, transform);
               block.name = pair.Key;
               block.GenerateAtom(pair.Key, pair.Value);
               block.gameObject.SetActive(false);
               _blockPool.Add(pair.Key, block);
           }
        }
        #endregion
        
        #region Spawning
        /// <summary>
        /// Spawns random blocks at spawn points.
        /// </summary>
        public void SpawnRandomBlock()
        {
            //if (spawnPoints.Any(x => !x.IsFree)) return;
            var blockTypes = Enum.GetValues(typeof(BlockTypes)).Cast<BlockTypes>().ToList();
            var allSchemas = _blockPool
                .SelectMany(x => x.Value.BlockPreset.BlockSchemas
                    .Select(schema => new FaceAndSchemaData(x.Key, schema))).ToList();
            var shuffledSchemas = allSchemas.Shuffled().ToList();
            List<FaceAndSchemaData> randomSchemas;
            GridManager.Instance.CreateVacantSchema(out var vacantSchema,out var vacantCount);
            if (smartRandom && vacantCount <= smartRandomThreshold)
            {
                var bestFits = FindBestFitSorted(vacantSchema, shuffledSchemas);
                var bestFitSchemas = bestFits
                    .SelectMany(x => x.schemaList)
                    .Take(maxRandomAmount)
                    .ToList();
                var remainingAmount = maxRandomAmount - bestFitSchemas.Count;
                if (remainingAmount > 0)
                {
                    bestFitSchemas.AddRange(shuffledSchemas.Take(remainingAmount));
                }
                randomSchemas = bestFitSchemas.ToList();
            }
            else
            {
                randomSchemas = shuffledSchemas
                    .Take(maxRandomAmount)
                    .ToList();
            }
            var spawnedBlocks = new List<Block>();
            for (int i = 0; i < randomSchemas.Count; i++)
            {
                if (!spawnPoints[i].IsFree)
                {
                    continue;
                }
                Transform spawnTransform = spawnPoints[i].Transform;
                var randomBlock = randomSchemas[i];
                var blockType = blockTypes.GetRandomElement();
                var blockFace = randomBlock.blockFace;
                var index = randomBlock.blockSchema.Index;
                if (!_blockPool.TryGetValue(blockFace, out var blockToSpawn))
                {
                    Debug.LogError($"Block face {blockFace} not found in block pool.");
                    continue;
                }
                Block block = _objectResolver.Instantiate(blockToSpawn, spawnTransform.position, Quaternion.identity, transform);
                block.gameObject.SetActive(true);
                block.ChangeType(blockType, false);
                block.SpawnIndex = i;
                block.transform.localScale = Vector3.zero;
                Vector3 scale = new Vector3(objectScale, objectScale, 1f);
                int randomRotation = index * 90;
                block.transform.eulerAngles = new Vector3(0, 0, randomRotation);
                _scaleTween = Tween.Scale(block.transform, scale, 0.2f).OnComplete(() => block.Initialize());
                spawnPoints[i].IsFree = false;
                spawnPoints[i].CurrentBlock = block;
                spawnedBlocks.Add(block);
            }
            if (spawnedBlocks.Count > 0)
                OnBlockSpawned?.Invoke(spawnedBlocks);
        }

        private void SpawnBlock(BlockPreset preset)
        {
            var spawnedBlocks = new List<Block>();
            if (!spawnPoints[0].IsFree) return;
            Transform spawnTransform = spawnPoints[0].Transform;
            var blockTypes = Enum.GetValues(typeof(BlockTypes)).Cast<BlockTypes>().ToList();
            var blockType = blockTypes.GetRandomElement();
            var blockFace = blockPresetDictionary.FirstOrDefault(x => x.Value == preset).Key;
            if (!_blockPool.TryGetValue(blockFace, out var blockToSpawn))
            {
                Debug.LogError($"Block face {blockFace} not found in block pool.");
                return;
            }
            Block block = _objectResolver.Instantiate(blockToSpawn, spawnTransform.position, Quaternion.identity, transform);
            block.gameObject.SetActive(true);
            block.ChangeType(blockType, false);
            block.SpawnIndex = 0;
            block.transform.localScale = Vector3.zero;
            Vector3 scale = new Vector3(objectScale, objectScale, 1f);
            block.transform.eulerAngles = Vector3.zero;
            _scaleTween = Tween.Scale(block.transform, scale, 0.2f).OnComplete(() => block.Initialize());
            spawnPoints[0].IsFree = false;
            spawnPoints[0].CurrentBlock = block;
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
                List<FaceAndSchemaData> schemasToCheck)
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
            int[,] vacantSchema, List<FaceAndSchemaData> schemasToCheck,
            List<FaceAndSchemaData> previouslyTraversed = null, 
            int currentDepth = 0,
            int vacantToBeat = int.MaxValue, 
            int blockCountToBeat = int.MaxValue)
        {
            List<BestFitResult> unsorted = new();
            var vacantCount = vacantSchema.CountMember(x => x == 1);
            if (currentDepth >= smartRandomDepth)
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
                var traversed = new List<FaceAndSchemaData>();
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
            spawnPoints[index].IsFree = true;
            spawnPoints[index].CurrentBlock = null;
        }

        public void FreeSpawnPoint(SpawnPoint spawnPoint)
        {
            spawnPoint.IsFree = true;
            spawnPoint.CurrentBlock = null;
        }
        
        public void ResetSpawnPoint()
        {
            foreach (var spawnPoint in spawnPoints)
            {
                spawnPoint.IsFree = true;
                if (spawnPoint.CurrentBlock)
                    Destroy(spawnPoint.CurrentBlock.gameObject);
                spawnPoint.CurrentBlock = null;
            }
        }

        public async UniTask GameOverCheck()
        {
            if (_scaleTween.isAlive)
            {
                await _scaleTween.ToUniTask();
            }
            List<Block> blockToCheck = spawnPoints.Where(x => !x.IsFree).Select(spawnPoint => spawnPoint.CurrentBlock).ToList();
            if (!GridManager.Instance.CheckAvailableBlock(blockToCheck, out _))
            {
                OnGameOver?.Invoke();
            }
        }
        #endregion

        public List<BlockPreset> Invoke(BlockPresetRequest request)
        {
            return blockPresetDictionary.Values.ToList();
        }
    }
}
