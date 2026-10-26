using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;

namespace FitMe.Grid
{
    public sealed class ExplosionVfxPool : IDisposable
    {
        private sealed class PrefabPool
        {
            public readonly ObjectPool<ParticleSystem> Pool;
            public readonly HashSet<ParticleSystem> Active = new();
            public int RetainedCount;

            public PrefabPool(ObjectPool<ParticleSystem> pool)
            {
                Pool = pool;
            }
        }

        private readonly ExplosionVfxPoolConfig _config;
        private readonly Transform _root;
        private readonly Dictionary<ParticleSystem, PrefabPool> _pools = new();
        private int _retainedCount;
        private bool _disposed;

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

        public void Play(ParticleSystem prefab, Vector3 position)
        {
            Play(prefab, position, _config.PrewarmCountPerPrefab, null);
        }

        public ParticleSystem PlayPlacement(
            ParticleSystem prefab,
            Vector3 position,
            Action<ParticleSystem> onReturned)
        {
            return Play(prefab, position, _config.PlacementPrewarmCountPerPrefab, onReturned);
        }

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
            Action<ParticleSystem> onReturned)
        {
            if (_disposed || !prefab) return null;

            var pool = GetOrCreatePool(prefab, prewarmCount);
            var wasRetained = pool.Pool.CountInactive > 0;
            var instance = pool.Pool.Get();
            if (wasRetained)
            {
                pool.RetainedCount--;
                _retainedCount--;
            }
            pool.Active.Add(instance);
            instance.GetComponent<ExplosionVfxPoolReturnHandler>().SetOnReturned(onReturned);
            instance.transform.SetParent(null, true);
            instance.transform.SetPositionAndRotation(position, Quaternion.identity);
            instance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            instance.gameObject.SetActive(true);
            instance.Play(true);
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

            pool = new PrefabPool(objectPool);
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

            if (_retainedCount >= GlobalSoftCapacity || pool.RetainedCount >= _config.CapacityPerPrefab)
            {
                instance.GetComponent<ExplosionVfxPoolReturnHandler>().NotifyReturned(instance);
                Destroy(instance.gameObject);
                return;
            }

            pool.Pool.Release(instance);
            instance.GetComponent<ExplosionVfxPoolReturnHandler>().NotifyReturned(instance);
        }

        private int GlobalSoftCapacity => _pools.Count * _config.CapacityPerPrefab;

        private static void Destroy(GameObject target)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(target);
            else UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
