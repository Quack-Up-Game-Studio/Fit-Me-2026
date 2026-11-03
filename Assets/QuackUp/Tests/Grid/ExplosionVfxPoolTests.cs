using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using FitMe.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
#if FITME_PERFORMANCE_DIAGNOSTICS
using QuackUp.Core;
using VContainer;
#endif

namespace FitMe.Grid.Tests
{
    public class ExplosionVfxPoolTests
    {
        private ExplosionVfxPool _pool;
        private ExplosionVfxPoolConfig _config;
        private BlockConfig _dynamicBlockConfig;
        private GameObject _testPrefabObject;
#if FITME_PERFORMANCE_DIAGNOSTICS
        private BlockManagerConfig _blockManagerConfig;
        private PerformanceDiagnosticsRecorder _diagnosticsRecorder;
#endif

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _pool?.Dispose();
            if (_testPrefabObject) Object.DestroyImmediate(_testPrefabObject);
            if (_dynamicBlockConfig) Object.DestroyImmediate(_dynamicBlockConfig);
#if FITME_PERFORMANCE_DIAGNOSTICS
            if (_blockManagerConfig) Object.DestroyImmediate(_blockManagerConfig);
            _diagnosticsRecorder?.Dispose();
#endif
            if (_config) Object.DestroyImmediate(_config);
            yield return null;
        }

        [Test]
        public void Config_DefaultsCapacityAndPrewarmToTenPerPrefab()
        {
            _config = ScriptableObject.CreateInstance<ExplosionVfxPoolConfig>();

            Assert.That(_config.CapacityPerPrefab, Is.EqualTo(10));
            Assert.That(_config.PrewarmCountPerPrefab, Is.EqualTo(10));
            var placementPrewarmProperty = typeof(ExplosionVfxPoolConfig).GetProperty("PlacementPrewarmCountPerPrefab");
            Assert.That(placementPrewarmProperty, Is.Not.Null,
                "Placement VFX should have an independent prewarm setting in the shared config.");
            Assert.That(placementPrewarmProperty.GetValue(_config), Is.EqualTo(2));
        }

        [Test]
        public void Play_CreatesConfiguredPrewarmCountAndChecksOutOneInstance()
        {
            _config = ScriptableObject.CreateInstance<ExplosionVfxPoolConfig>();
            var prefab = AssetDatabase.LoadAssetAtPath<ParticleSystem>(
                "Assets/QuackUp/Prefabs/VFX/Explode/Explode_Red.prefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.main.stopAction, Is.EqualTo(ParticleSystemStopAction.Callback));

            _pool = new ExplosionVfxPool(_config, null, null);
            _pool.Play(prefab, new Vector3(3f, 4f, 0f));

            var poolsField = typeof(ExplosionVfxPool).GetField("_pools", BindingFlags.Instance | BindingFlags.NonPublic);
            var pools = (System.Collections.IDictionary)poolsField.GetValue(_pool);
            var prefabPool = pools[prefab];
            var activeField = prefabPool.GetType().GetField("Active");
            var active = (System.Collections.Generic.HashSet<ParticleSystem>)activeField.GetValue(prefabPool);
            Assert.That(active.Count, Is.EqualTo(1));
            var instance = System.Linq.Enumerable.First(active);

            var retainedField = typeof(ExplosionVfxPool).GetField("_retainedCount", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(retainedField.GetValue(_pool), Is.EqualTo(9),
                "Ten instances should be created for prewarm, with the played instance checked out.");

            var returnHandler = instance.GetComponent("ExplosionVfxPoolReturnHandler");
            returnHandler.GetType().GetMethod("OnParticleSystemStopped", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(returnHandler, null);
            Assert.That(retainedField.GetValue(_pool), Is.EqualTo(10));
            Assert.That(active.Count, Is.Zero);

            _pool.Play(prefab, Vector3.zero);
            Assert.That(active.Count, Is.EqualTo(1));
            Assert.That(System.Linq.Enumerable.First(active), Is.SameAs(instance));
        }

        [Test]
        public void Play_WhenConcurrentRequestsExceedPrewarm_TracksPoolMissAndPeakActiveCount()
        {
            _config = ScriptableObject.CreateInstance<ExplosionVfxPoolConfig>();
            _testPrefabObject = new GameObject("ExplosionTelemetryTestPrefab");
            var prefab = _testPrefabObject.AddComponent<ParticleSystem>();
            _pool = new ExplosionVfxPool(_config, null, null);

            for (var i = 0; i < 11; i++)
                _pool.Play(prefab, Vector3.zero);

            var pools = (System.Collections.IDictionary)typeof(ExplosionVfxPool)
                .GetField("_pools", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_pool);
            var prefabPool = pools[prefab];
            var createdOnDemand = prefabPool.GetType().GetField("CreatedOnDemandCount");
            var peakActive = prefabPool.GetType().GetField("PeakActiveCount");

            Assert.That(createdOnDemand, Is.Not.Null,
                "The diagnostic pool snapshot should expose on-demand creation beyond the ten-instance prewarm.");
            Assert.That(peakActive, Is.Not.Null,
                "The diagnostic pool snapshot should track the maximum simultaneous checkouts.");
            if (createdOnDemand == null || peakActive == null) return;

            Assert.That(createdOnDemand.GetValue(prefabPool), Is.EqualTo(1));
            Assert.That(peakActive.GetValue(prefabPool), Is.EqualTo(11));
        }

#if FITME_PERFORMANCE_DIAGNOSTICS
        [Test]
        public void ContainerInjection_AssignsPerformanceDiagnosticsRecorderToPool()
        {
            _config = ScriptableObject.CreateInstance<ExplosionVfxPoolConfig>();
            _blockManagerConfig = ScriptableObject.CreateInstance<BlockManagerConfig>();
            _dynamicBlockConfig = ScriptableObject.CreateInstance<BlockConfig>();
            _diagnosticsRecorder = new PerformanceDiagnosticsRecorder();

            var builder = new ContainerBuilder();
            builder.RegisterInstance(_config);
            builder.RegisterInstance(_blockManagerConfig);
            builder.RegisterInstance(_dynamicBlockConfig);
            builder.RegisterInstance(_diagnosticsRecorder);
            builder.Register<ExplosionVfxPool>(Lifetime.Scoped);

            using var container = builder.Build();
            _pool = container.Resolve<ExplosionVfxPool>();

            var recorderField = typeof(ExplosionVfxPool)
                .GetField("_diagnosticsRecorder", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(recorderField, Is.Not.Null);
            Assert.That(recorderField.GetValue(_pool), Is.SameAs(_diagnosticsRecorder));
        }
#endif

        [Test]
        public void Warmup_PrewarmsPrefabsFromLateBoundBlockConfig()
        {
            _config = ScriptableObject.CreateInstance<ExplosionVfxPoolConfig>();
            _dynamicBlockConfig = ScriptableObject.CreateInstance<BlockConfig>();
            var prefab = AssetDatabase.LoadAssetAtPath<ParticleSystem>(
                "Assets/QuackUp/Prefabs/VFX/Explode/Explode_Red.prefab");
            typeof(BlockConfig).GetField("_explodeVfx", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_dynamicBlockConfig, new Dictionary<BlockColor, ParticleSystem>
                {
                    [BlockColor.Red] = prefab
                });

            var placePrefab = AssetDatabase.LoadAssetAtPath<ParticleSystem>(
                "Assets/QuackUp/Prefabs/VFX/Placing/Placing_Blue.prefab");
            typeof(BlockConfig).GetField("_placeVfx", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_dynamicBlockConfig, new Dictionary<BlockColor, ParticleSystem>
                {
                    [BlockColor.Blue] = placePrefab
                });

            _pool = new ExplosionVfxPool(_config, null, null);
            _pool.Warmup(_dynamicBlockConfig);

            var pools = (System.Collections.IDictionary)typeof(ExplosionVfxPool)
                .GetField("_pools", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_pool);
            Assert.That(pools.Contains(prefab), Is.True);
            Assert.That(pools.Contains(placePrefab), Is.True,
                "Warmup should also initialize placement VFX from the block config.");
            var prefabPool = pools[prefab];
            var objectPool = prefabPool.GetType().GetField("Pool").GetValue(prefabPool);
            Assert.That((int)objectPool.GetType().GetProperty("CountInactive").GetValue(objectPool), Is.EqualTo(10),
                "Explosion VFX should retain the explosion prewarm count.");
            Assert.That(prefabPool.GetType().GetField("RetainedCount").GetValue(prefabPool), Is.EqualTo(10));

            var placePool = pools[placePrefab];
            var placeObjectPool = placePool.GetType().GetField("Pool").GetValue(placePool);
            Assert.That((int)placeObjectPool.GetType().GetProperty("CountInactive").GetValue(placeObjectPool), Is.EqualTo(2),
                "Placement VFX should use its separate prewarm count.");
        }

        [Test]
        public void Warmup_ExpandsSharedPrefabToLargerPrewarmCount()
        {
            _config = ScriptableObject.CreateInstance<ExplosionVfxPoolConfig>();
            _dynamicBlockConfig = ScriptableObject.CreateInstance<BlockConfig>();
            var prefab = AssetDatabase.LoadAssetAtPath<ParticleSystem>(
                "Assets/QuackUp/Prefabs/VFX/Placing/Placing_Blue.prefab");
            typeof(BlockConfig).GetField("_placeVfx", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_dynamicBlockConfig, new Dictionary<BlockColor, ParticleSystem>
                {
                    [BlockColor.Blue] = prefab
                });

            _pool = new ExplosionVfxPool(_config, null, null);
            _pool.Warmup(_dynamicBlockConfig);

            typeof(BlockConfig).GetField("_explodeVfx", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_dynamicBlockConfig, new Dictionary<BlockColor, ParticleSystem>
                {
                    [BlockColor.Blue] = prefab
                });
            _pool.Warmup(_dynamicBlockConfig);

            var pools = (System.Collections.IDictionary)typeof(ExplosionVfxPool)
                .GetField("_pools", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_pool);
            Assert.That(pools.Count, Is.EqualTo(1), "A prefab shared across effect categories should have one pool.");
            var prefabPool = pools[prefab];
            var objectPool = prefabPool.GetType().GetField("Pool").GetValue(prefabPool);
            Assert.That((int)objectPool.GetType().GetProperty("CountInactive").GetValue(objectPool), Is.EqualTo(10),
                "A later higher prewarm target should increase the shared pool to ten distinct instances.");
        }

        [Test]
        public void Warmup_ExpandsSharedPrefabWithoutLosingActiveInstance()
        {
            _config = ScriptableObject.CreateInstance<ExplosionVfxPoolConfig>();
            _dynamicBlockConfig = ScriptableObject.CreateInstance<BlockConfig>();
            var prefab = AssetDatabase.LoadAssetAtPath<ParticleSystem>(
                "Assets/QuackUp/Prefabs/VFX/Placing/Placing_Blue.prefab");
            typeof(BlockConfig).GetField("_placeVfx", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_dynamicBlockConfig, new Dictionary<BlockColor, ParticleSystem>
                {
                    [BlockColor.Blue] = prefab
                });

            _pool = new ExplosionVfxPool(_config, null, null);
            _pool.Warmup(_dynamicBlockConfig);
            _pool.PlayPlacement(prefab, Vector3.zero, null);

            typeof(BlockConfig).GetField("_explodeVfx", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_dynamicBlockConfig, new Dictionary<BlockColor, ParticleSystem>
                {
                    [BlockColor.Blue] = prefab
                });
            _pool.Warmup(_dynamicBlockConfig);

            var pools = (System.Collections.IDictionary)typeof(ExplosionVfxPool)
                .GetField("_pools", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_pool);
            var prefabPool = pools[prefab];
            var objectPool = prefabPool.GetType().GetField("Pool").GetValue(prefabPool);
            var active = (System.Collections.Generic.HashSet<ParticleSystem>)
                prefabPool.GetType().GetField("Active").GetValue(prefabPool);
            Assert.That(objectPool.GetType().GetProperty("CountAll").GetValue(objectPool), Is.EqualTo(10));
            Assert.That(objectPool.GetType().GetProperty("CountInactive").GetValue(objectPool), Is.EqualTo(9));
            Assert.That(active.Count, Is.EqualTo(1));
        }
    }
}
