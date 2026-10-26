using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using FitMe.Shared;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace FitMe.Grid.PlayModeTests
{
    public class ExplosionVfxPoolPlayModeTests
    {
        private ExplosionVfxPool _pool;
        private ExplosionVfxPoolConfig _config;
        private BlockConfig _blockConfig;
        private GameObject _prefabObject;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _pool?.Dispose();
            if (_prefabObject) Object.Destroy(_prefabObject);
            if (_blockConfig) Object.Destroy(_blockConfig);
            if (_config) Object.Destroy(_config);
            yield return null;
        }

        [UnityTest, Timeout(10000)]
        public IEnumerator Play_ReturnsOnUnityParticleStopCallbackAndReusesSameInstance()
        {
            _config = ScriptableObject.CreateInstance<ExplosionVfxPoolConfig>();
            _prefabObject = new GameObject("Explosion VFX PlayMode Test Prefab");
            _prefabObject.SetActive(false);
            var prefab = _prefabObject.AddComponent<ParticleSystem>();
            var main = prefab.main;
            main.duration = 0.1f;
            main.loop = false;
            main.startLifetime = 0.1f;
            main.startSpeed = 0f;
            var emission = prefab.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

            _pool = new ExplosionVfxPool(_config, null, null);
            _pool.Play(prefab, Vector3.zero);

            var pools = (IDictionary)typeof(ExplosionVfxPool)
                .GetField("_pools", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_pool);
            var prefabPool = pools[prefab];
            var active = (HashSet<ParticleSystem>)prefabPool.GetType().GetField("Active").GetValue(prefabPool);
            Assert.That(active.Count, Is.EqualTo(1));
            var instance = System.Linq.Enumerable.First(active);

            for (var frame = 0; frame < 120 && active.Count != 0; frame++)
                yield return null;

            Assert.That(active.Count, Is.Zero, "Unity should dispatch OnParticleSystemStopped when the effect ends.");
            Assert.That(instance, Is.Not.Null, "A returned pooled particle system must not be destroyed.");
            Assert.That(instance.gameObject.activeSelf, Is.False);

            _pool.Play(prefab, Vector3.zero);
            Assert.That(active.Count, Is.EqualTo(1));
            Assert.That(System.Linq.Enumerable.First(active), Is.SameAs(instance));
        }

        [UnityTest, Timeout(10000)]
        public IEnumerator PlayPlacement_NotifiesOwnerAfterReturnAndReusesInstance()
        {
            _config = ScriptableObject.CreateInstance<ExplosionVfxPoolConfig>();
            _prefabObject = new GameObject("Placement VFX PlayMode Test Prefab");
            _prefabObject.SetActive(false);
            var prefab = _prefabObject.AddComponent<ParticleSystem>();
            var main = prefab.main;
            main.duration = 0.1f;
            main.loop = false;
            main.startLifetime = 0.1f;
            main.startSpeed = 0f;
            var emission = prefab.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

            _pool = new ExplosionVfxPool(_config, null, null);
            var returnedInstances = new List<ParticleSystem>();
            var instance = _pool.PlayPlacement(prefab, Vector3.zero, returnedInstances.Add);
            var pools = (IDictionary)typeof(ExplosionVfxPool)
                .GetField("_pools", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_pool);
            var prefabPool = pools[prefab];
            var active = (HashSet<ParticleSystem>)prefabPool.GetType().GetField("Active").GetValue(prefabPool);
            Assert.That(active.Count, Is.EqualTo(1));

            for (var frame = 0; frame < 120 && active.Count != 0; frame++)
                yield return null;

            Assert.That(active.Count, Is.Zero);
            Assert.That(returnedInstances, Has.Count.EqualTo(1));
            Assert.That(returnedInstances[0], Is.SameAs(instance));
            Assert.That(instance.gameObject.activeSelf, Is.False);

            var reusedInstance = _pool.PlayPlacement(prefab, Vector3.zero, null);
            Assert.That(reusedInstance, Is.SameAs(instance));
            Assert.That(active.Count, Is.EqualTo(1));
        }

        [UnityTest, Timeout(10000)]
        public IEnumerator BlockView_DisposeClearsPendingPlacementReturnCallback()
        {
            _config = ScriptableObject.CreateInstance<ExplosionVfxPoolConfig>();
            _blockConfig = ScriptableObject.CreateInstance<BlockConfig>();
            _prefabObject = new GameObject("Placement VFX Owner Lifetime Test Prefab");
            _prefabObject.SetActive(false);
            var prefab = _prefabObject.AddComponent<ParticleSystem>();
            var main = prefab.main;
            main.duration = 0.1f;
            main.loop = false;
            main.startLifetime = 0.1f;
            main.startSpeed = 0f;
            var emission = prefab.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            typeof(BlockConfig).GetField("_placeVfx", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_blockConfig, new Dictionary<BlockColor, ParticleSystem>
                {
                    [BlockColor.Blue] = prefab
                });

            _pool = new ExplosionVfxPool(_config, null, null);
            var viewObject = new GameObject("Placement VFX Owner Lifetime Test Block");
            var blockView = viewObject.AddComponent<BlockView>();
            typeof(BlockView).GetField("_blockConfig", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(blockView, _blockConfig);
            typeof(BlockView).GetField("_blockColor", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(blockView, BlockColor.Blue);
            typeof(BlockView).GetField("_blockVfxPool", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(blockView, _pool);
            typeof(BlockView).GetMethod("PlayPlaceVFX", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(blockView, null);

            var instance = (ParticleSystem)typeof(BlockView)
                .GetField("_placingVfxInstance", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(blockView);
            var returnHandler = instance.GetComponent("ExplosionVfxPoolReturnHandler");
            var callbackField = returnHandler.GetType()
                .GetField("_onReturned", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(callbackField.GetValue(returnHandler), Is.Not.Null,
                "A live placement effect should initially hold its owner notification.");

            blockView.Dispose();

            Assert.That(callbackField.GetValue(returnHandler), Is.Null,
                "Disposing the block view must release the callback that would retain its owner.");
            Assert.That(typeof(BlockView).GetField("_placingVfxInstance", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(blockView), Is.Null);
            Object.Destroy(viewObject);
            yield return null;
        }
    }
}
