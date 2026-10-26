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
            _root = new GameObject("Explosion VFX Pool").transform;

            var prefabs = new HashSet<ParticleSystem>();
            AddPrefabs(prefabs, blockConfig);
            if (blockManagerConfig)
            {
                foreach (var configuredBlock in blockManagerConfig.BlockConfigDictionary.Values)
                    AddPrefabs(prefabs, configuredBlock);
                AddPrefabs(prefabs, blockManagerConfig.ShapeModeBlockConfig);
            }

            foreach (var prefab in prefabs) GetOrCreatePool(prefab);
        }

        public void Play(ParticleSystem prefab, Vector3 position)
        {
            if (_disposed || !prefab) return;

            var pool = GetOrCreatePool(prefab);
            var wasRetained = pool.Pool.CountInactive > 0;
            var instance = pool.Pool.Get();
            if (wasRetained)
            {
                pool.RetainedCount--;
                _retainedCount--;
            }
            pool.Active.Add(instance);
            instance.transform.SetParent(null, true);
            instance.transform.SetPositionAndRotation(position, Quaternion.identity);
            instance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            instance.gameObject.SetActive(true);
            instance.Play(true);
        }

        public void Warmup(BlockConfig blockConfig)
        {
            if (_disposed || !blockConfig) return;

            var prefabs = new HashSet<ParticleSystem>();
            AddPrefabs(prefabs, blockConfig);
            foreach (var prefab in prefabs) GetOrCreatePool(prefab);
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

        private PrefabPool GetOrCreatePool(ParticleSystem prefab)
        {
            if (_pools.TryGetValue(prefab, out var existing)) return existing;

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
                _config.PrewarmCountPerPrefab,
                _config.CapacityPerPrefab);

            pool = new PrefabPool(objectPool);
            _pools.Add(prefab, pool);
            for (var i = 0; i < _config.PrewarmCountPerPrefab; i++)
            {
                var instance = objectPool.Get();
                objectPool.Release(instance);
            }
            return pool;
        }

        private void Return(ParticleSystem instance, PrefabPool pool)
        {
            if (_disposed || !instance || !pool.Active.Remove(instance)) return;

            if (_retainedCount >= GlobalSoftCapacity || pool.RetainedCount >= _config.CapacityPerPrefab)
            {
                Destroy(instance.gameObject);
                return;
            }

            pool.Pool.Release(instance);
        }

        private int GlobalSoftCapacity => _pools.Count * _config.CapacityPerPrefab;

        private static void AddPrefabs(HashSet<ParticleSystem> prefabs, BlockConfig blockConfig)
        {
            if (!blockConfig) return;
            foreach (var prefab in blockConfig.ExplodeVfx.Values)
            {
                if (prefab) prefabs.Add(prefab);
            }
        }

        private static void Destroy(GameObject target)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(target);
            else UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
