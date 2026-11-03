using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;
#if FITME_PERFORMANCE_DIAGNOSTICS
using QuackUp.Core;
#endif

namespace FitMe.Grid
{
    public sealed class ExplosionVfxPool : IDisposable
    {
        private sealed class PrefabPool
        {
#if FITME_PERFORMANCE_DIAGNOSTICS
            public readonly ParticleSystem Prefab;
#endif
            public readonly ObjectPool<ParticleSystem> Pool;
            public readonly HashSet<ParticleSystem> Active = new();
            public int RetainedCount;
#if UNITY_EDITOR || FITME_PERFORMANCE_DIAGNOSTICS
            public int CreatedCount;
            public int CreatedOnDemandCount;
            public int PeakActiveCount;
#endif

            public PrefabPool(ObjectPool<ParticleSystem> pool
#if FITME_PERFORMANCE_DIAGNOSTICS
                , ParticleSystem prefab
#endif
                )
            {
                Pool = pool;
#if FITME_PERFORMANCE_DIAGNOSTICS
                Prefab = prefab;
#endif
            }
        }

        private readonly ExplosionVfxPoolConfig _config;
        private readonly Transform _root;
        private readonly Dictionary<ParticleSystem, PrefabPool> _pools = new();
        private int _retainedCount;
        private bool _disposed;
#if UNITY_EDITOR || FITME_PERFORMANCE_DIAGNOSTICS
        private int _activeVfxCount;
        private int _peakActiveVfxCount;
#endif
#if FITME_PERFORMANCE_DIAGNOSTICS
        private readonly Dictionary<ParticleSystem, string> _activeVfxKinds = new();
        private readonly Dictionary<ParticleSystem, int> _activeVfxBlockIds = new();
        private PerformanceDiagnosticsRecorder _diagnosticsRecorder;
#endif

        [Inject]
        public ExplosionVfxPool(
            ExplosionVfxPoolConfig config,
            BlockManagerConfig blockManagerConfig,
            BlockConfig blockConfig)
        {
            _config = config ? config : throw new ArgumentNullException(nameof(config));
            _root = new GameObject("Block VFX Pool").transform;

            Warmup(blockConfig);
            if (blockManagerConfig)
            {
                foreach (var configuredBlock in blockManagerConfig.BlockConfigDictionary.Values)
                    Warmup(configuredBlock);
                Warmup(blockManagerConfig.ArbitraryBlockConfig);
            }
        }

#if FITME_PERFORMANCE_DIAGNOSTICS
        [Inject]
        public void SetPerformanceDiagnosticsRecorder(PerformanceDiagnosticsRecorder diagnosticsRecorder)
        {
            _diagnosticsRecorder = diagnosticsRecorder;
        }
#endif

        public void Play(ParticleSystem prefab, Vector3 position)
        {
#if FITME_PERFORMANCE_DIAGNOSTICS
            Play(prefab, position, _config.PrewarmCountPerPrefab, null, false, 0);
#else
            Play(prefab, position, _config.PrewarmCountPerPrefab, null);
#endif
        }

#if FITME_PERFORMANCE_DIAGNOSTICS
        public void Play(ParticleSystem prefab, Vector3 position, int blockInstanceId)
        {
            Play(prefab, position, _config.PrewarmCountPerPrefab, null, false, blockInstanceId);
        }
#endif

        public ParticleSystem PlayPlacement(
            ParticleSystem prefab,
            Vector3 position,
            Action<ParticleSystem> onReturned)
        {
#if FITME_PERFORMANCE_DIAGNOSTICS
            return Play(prefab, position, _config.PlacementPrewarmCountPerPrefab, onReturned, true, 0);
#else
            return Play(prefab, position, _config.PlacementPrewarmCountPerPrefab, onReturned);
#endif
        }

#if FITME_PERFORMANCE_DIAGNOSTICS
        public void RecordExplosionEvent(string stage, int blockInstanceId, string color, string fitType)
        {
            _diagnosticsRecorder?.RecordEvent(stage,
                "blockId=" + blockInstanceId + ";color=" + color + ";fitType=" + fitType +
                ";activeVfx=" + _activeVfxCount + ";peakActiveVfx=" + _peakActiveVfxCount);
        }
#endif

        public void CancelReturnCallback(ParticleSystem instance, Action<ParticleSystem> onReturned)
        {
            if (!instance) return;

            var returnHandler = instance.GetComponent<ExplosionVfxPoolReturnHandler>();
            if (returnHandler) returnHandler.ClearOnReturned(onReturned);
        }

        private ParticleSystem Play(
            ParticleSystem prefab,
            Vector3 position,
            int prewarmCount,
            Action<ParticleSystem> onReturned
#if FITME_PERFORMANCE_DIAGNOSTICS
            , bool isPlacement,
            int blockInstanceId
#endif
            )
        {
            if (_disposed || !prefab) return null;

#if UNITY_EDITOR || FITME_PERFORMANCE_DIAGNOSTICS
            var createdBeforePoolSetup = _pools.TryGetValue(prefab, out var existingPool)
                ? existingPool.CreatedCount
                : 0;
#endif
            var pool = GetOrCreatePool(prefab, prewarmCount);
#if UNITY_EDITOR || FITME_PERFORMANCE_DIAGNOSTICS
            var createdDuringPoolSetup = pool.CreatedCount - createdBeforePoolSetup;
            var createdBeforeGet = pool.CreatedCount;
#endif
            var wasRetained = pool.Pool.CountInactive > 0;
            var instance = pool.Pool.Get();
#if UNITY_EDITOR || FITME_PERFORMANCE_DIAGNOSTICS
            var poolMiss = pool.CreatedCount > createdBeforeGet;
            if (poolMiss) pool.CreatedOnDemandCount++;
#endif
            if (wasRetained)
            {
                pool.RetainedCount--;
                _retainedCount--;
            }
            pool.Active.Add(instance);
#if UNITY_EDITOR || FITME_PERFORMANCE_DIAGNOSTICS
            if (pool.Active.Count > pool.PeakActiveCount)
                pool.PeakActiveCount = pool.Active.Count;
            _activeVfxCount++;
            if (_activeVfxCount > _peakActiveVfxCount)
                _peakActiveVfxCount = _activeVfxCount;
#endif
#if FITME_PERFORMANCE_DIAGNOSTICS
            var kind = isPlacement ? "placement" : "explosion";
            _activeVfxKinds[instance] = kind;
            _activeVfxBlockIds[instance] = blockInstanceId;
#endif
            instance.GetComponent<ExplosionVfxPoolReturnHandler>().SetOnReturned(onReturned);
            instance.transform.SetParent(null, true);
            instance.transform.SetPositionAndRotation(position, Quaternion.identity);
            instance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            instance.gameObject.SetActive(true);
            instance.Play(true);
#if FITME_PERFORMANCE_DIAGNOSTICS
            _diagnosticsRecorder?.RecordEvent("vfx_started",
                "kind=" + kind + ";prefab=" + prefab.name +
                ";poolSetupCreated=" + createdDuringPoolSetup +
                ";poolMiss=" + (poolMiss ? "1" : "0") +
                ";blockId=" + blockInstanceId +
                ";createdOnDemandTotal=" + pool.CreatedOnDemandCount +
                ";createdTotal=" + pool.CreatedCount +
                ";activeForPrefab=" + pool.Active.Count +
                ";peakForPrefab=" + pool.PeakActiveCount +
                ";activeTotal=" + _activeVfxCount +
                ";peakTotal=" + _peakActiveVfxCount +
                ";retainedInactive=" + pool.Pool.CountInactive);
#endif
            return instance;
        }

        public void Warmup(IBlockPresentationConfig blockConfig)
        {
            if (_disposed || blockConfig == null) return;

            Warmup(blockConfig.ExplodeVfx.Values, _config.PrewarmCountPerPrefab);
            Warmup(blockConfig.PlaceVFX.Values, _config.PlacementPrewarmCountPerPrefab);
        }

        private void Warmup(IEnumerable<ParticleSystem> prefabs, int prewarmCount)
        {
            foreach (var prefab in new HashSet<ParticleSystem>(prefabs))
            {
                if (prefab) GetOrCreatePool(prefab, prewarmCount);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var entry in _pools.Values)
            {
                foreach (var active in entry.Active)
                {
                    if (active) Destroy(active.gameObject);
                }
                entry.Active.Clear();
                entry.Pool.Dispose();
            }

            _pools.Clear();
#if FITME_PERFORMANCE_DIAGNOSTICS
            _activeVfxKinds.Clear();
            _activeVfxBlockIds.Clear();
#endif
            if (_root) Destroy(_root.gameObject);
        }

        private PrefabPool GetOrCreatePool(ParticleSystem prefab, int prewarmCount)
        {
            if (_pools.TryGetValue(prefab, out var existing))
            {
                Prewarm(existing, prewarmCount);
                return existing;
            }
            PrefabPool pool = null;
            var objectPool = new ObjectPool<ParticleSystem>(
                () =>
                {
                    var instance = UnityEngine.Object.Instantiate(prefab, _root);
#if UNITY_EDITOR || FITME_PERFORMANCE_DIAGNOSTICS
                    pool.CreatedCount++;
#endif
                    instance.gameObject.SetActive(false);
                    var handler = instance.gameObject.AddComponent<ExplosionVfxPoolReturnHandler>();
                    handler.Initialize(instance, stopped => Return(stopped, pool));
                    var main = instance.main;
                    main.stopAction = ParticleSystemStopAction.Callback;
                    return instance;
                },
                instance => instance.GetComponent<ExplosionVfxPoolReturnHandler>().BeginPlayback(),
                instance =>
                {
                    instance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    instance.gameObject.SetActive(false);
                    instance.transform.SetParent(_root, false);
                    pool.RetainedCount++;
                    _retainedCount++;
                },
                instance =>
                {
                    if (instance) Destroy(instance.gameObject);
                },
                true,
                _config.CapacityPerPrefab,
                _config.CapacityPerPrefab);

#if FITME_PERFORMANCE_DIAGNOSTICS
            pool = new PrefabPool(objectPool, prefab);
#else
            pool = new PrefabPool(objectPool);
#endif
            _pools.Add(prefab, pool);
            Prewarm(pool, prewarmCount);
            return pool;
        }

        private void Prewarm(PrefabPool pool, int prewarmCount)
        {
            var targetCount = Mathf.Clamp(prewarmCount, 0, _config.CapacityPerPrefab);
            var countToCreate = targetCount - pool.Pool.CountAll;
            if (countToCreate <= 0) return;

            var instancesToRelease = pool.Pool.CountInactive + countToCreate;
            var prewarmedInstances = new List<ParticleSystem>(instancesToRelease);
            for (var i = 0; i < instancesToRelease; i++)
            {
                prewarmedInstances.Add(pool.Pool.Get());
            }

            foreach (var instance in prewarmedInstances)
            {
                pool.Pool.Release(instance);
            }
        }

        private void Return(ParticleSystem instance, PrefabPool pool)
        {
            if (_disposed || !instance || !pool.Active.Remove(instance)) return;

#if FITME_PERFORMANCE_DIAGNOSTICS
            var kind = _activeVfxKinds.TryGetValue(instance, out var activeKind) ? activeKind : "unknown";
            var blockInstanceId = _activeVfxBlockIds.TryGetValue(instance, out var activeBlockId) ? activeBlockId : 0;
            _activeVfxKinds.Remove(instance);
            _activeVfxBlockIds.Remove(instance);
            var prefabName = pool.Prefab ? pool.Prefab.name : "unknown";
#endif
#if UNITY_EDITOR || FITME_PERFORMANCE_DIAGNOSTICS
            _activeVfxCount--;
#endif
            if (_retainedCount >= GlobalSoftCapacity || pool.RetainedCount >= _config.CapacityPerPrefab)
            {
                instance.GetComponent<ExplosionVfxPoolReturnHandler>().NotifyReturned(instance);
                Destroy(instance.gameObject);
            }
            else
            {
                pool.Pool.Release(instance);
                instance.GetComponent<ExplosionVfxPoolReturnHandler>().NotifyReturned(instance);
            }
#if FITME_PERFORMANCE_DIAGNOSTICS
            _diagnosticsRecorder?.RecordEvent("vfx_returned",
                "kind=" + kind + ";prefab=" + prefabName + ";blockId=" + blockInstanceId +
                ";activeForPrefab=" + pool.Active.Count +
                ";peakForPrefab=" + pool.PeakActiveCount +
                ";activeTotal=" + _activeVfxCount +
                ";peakTotal=" + _peakActiveVfxCount +
                ";createdOnDemandTotal=" + pool.CreatedOnDemandCount +
                ";createdTotal=" + pool.CreatedCount +
                ";retainedInactive=" + pool.Pool.CountInactive);
#endif
        }

        private int GlobalSoftCapacity => _pools.Count * _config.CapacityPerPrefab;

        private static void Destroy(GameObject target)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(target);
            else UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
