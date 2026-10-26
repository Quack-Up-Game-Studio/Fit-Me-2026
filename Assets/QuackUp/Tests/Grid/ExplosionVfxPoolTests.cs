using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using FitMe.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace FitMe.Grid.Tests
{
    public class ExplosionVfxPoolTests
    {
        private ExplosionVfxPool _pool;
        private ExplosionVfxPoolConfig _config;
        private BlockConfig _dynamicBlockConfig;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _pool?.Dispose();
            if (_dynamicBlockConfig) Object.DestroyImmediate(_dynamicBlockConfig);
            if (_config) Object.DestroyImmediate(_config);
            yield return null;
        }

        [Test]
        public void Config_DefaultsCapacityAndPrewarmToTenPerPrefab()
        {
            _config = ScriptableObject.CreateInstance<ExplosionVfxPoolConfig>();

            Assert.That(_config.CapacityPerPrefab, Is.EqualTo(10));
            Assert.That(_config.PrewarmCountPerPrefab, Is.EqualTo(10));
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

            _pool = new ExplosionVfxPool(_config, null, null);
            _pool.Warmup(_dynamicBlockConfig);

            var pools = (System.Collections.IDictionary)typeof(ExplosionVfxPool)
                .GetField("_pools", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_pool);
            Assert.That(pools.Contains(prefab), Is.True);
            var prefabPool = pools[prefab];
            Assert.That(prefabPool.GetType().GetField("RetainedCount").GetValue(prefabPool), Is.EqualTo(10));
        }
    }
}
