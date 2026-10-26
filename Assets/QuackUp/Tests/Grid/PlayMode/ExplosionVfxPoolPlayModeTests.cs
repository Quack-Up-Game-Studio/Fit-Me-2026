using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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
        private GameObject _prefabObject;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _pool?.Dispose();
            if (_prefabObject) Object.Destroy(_prefabObject);
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
    }
}
