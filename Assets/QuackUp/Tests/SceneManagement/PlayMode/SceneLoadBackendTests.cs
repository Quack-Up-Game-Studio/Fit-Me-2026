#if UNITY_EDITOR
using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace QuackUp.SceneManagement.PlayModeTests
{
    [PrebuildSetup(typeof(EmptySceneFixtures))]
    [PostBuildCleanup(typeof(EmptySceneFixtures))]
    public sealed class SceneLoadBackendTests
    {
        [UnityTest]
        public IEnumerator NativeAdapter_RetainsCompletionAndRemovesOnlyOwnedDelegates()
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects()) UnityEngine.Object.DontDestroyOnLoad(root);
            EmptySceneFixtures.EnableFixtures();
            var sourceLoad = SceneManager.LoadSceneAsync(EmptySceneFixtures.Source, LoadSceneMode.Single);
            yield return WaitFor(() => sourceLoad.isDone);
            var backend = new UnitySceneLoadBackend();
            var source = backend.GetActiveSceneHandle();
            var removedLoaded = 0; var liveLoaded = 0;
            var removedActive = 0; var liveActive = 0;
            var loadedHandle = 0;
            var removedL = backend.SubscribeLoaded((_, _) => removedLoaded++);
            var removedA = backend.SubscribeActiveChanged((_, _) => removedActive++);
            using var liveL = backend.SubscribeLoaded((handle, mode) =>
            {
                Assert.That(mode, Is.EqualTo(LoadSceneMode.Additive));
                loadedHandle = handle; liveLoaded++;
            });
            using var liveA = backend.SubscribeActiveChanged((previous, _) =>
            {
                Assert.That(previous, Is.EqualTo(source)); liveActive++;
            });
            removedL.Dispose(); removedL.Dispose(); removedA.Dispose(); removedA.Dispose();
            ISceneLoadOperation operation = null;
            try
            {
                operation = backend.BeginLoad(EmptySceneFixtures.Destination, LoadSceneMode.Additive);
                Assert.That(operation, Is.Not.Null);
                operation.AllowSceneActivation = false;
                var completed = false;
                Exception error = null;
                Observe(operation.WaitForCompletion(), () => completed = true, e => error = e).Forget();
                yield return WaitFor(() => operation.Progress >= 0.9f);
                Assert.That(operation.IsDone, Is.False, "Activation-gated native operation is not completed.");
                Assert.That(completed, Is.False);
                Assert.That(liveLoaded, Is.Zero);
                operation.AllowSceneActivation = true;
                yield return WaitFor(() => completed || error != null);
                Assert.That(error, Is.Null, error?.ToString());
                Assert.That(operation.IsDone, Is.True, "Wrapper must retain the actual engine completion reference.");
                Assert.That(liveLoaded, Is.EqualTo(1));
                Assert.That(backend.GetScenePath(loadedHandle), Is.EqualTo(EmptySceneFixtures.Destination));
                Assert.That(backend.SetActiveScene(loadedHandle), Is.True);
                Assert.That(liveActive, Is.EqualTo(1));
                Assert.That(removedLoaded, Is.Zero);
                Assert.That(removedActive, Is.Zero);
                var unloaded = false;
                Observe(backend.UnloadScene(source), () => unloaded = true, e => error = e).Forget();
                yield return WaitFor(() => unloaded || error != null);
                Assert.That(error, Is.Null, error?.ToString());
                Assert.That(SceneManager.GetSceneByPath(EmptySceneFixtures.Source).isLoaded, Is.False);
                TestContext.Out.WriteLine("NATIVE_BACKEND gatedIsDone=false completionObserved=true removedDelegates=0 liveLoaded=1 liveActive=1 sourceUnloaded=true");
            }
            finally
            {
                if (operation != null) operation.AllowSceneActivation = true;
                removedL.Dispose(); removedA.Dispose(); EmptySceneFixtures.RestoreBuildSettings();
            }
        }
        private static IEnumerator WaitFor(Func<bool> predicate)
        {
            var deadline = Time.realtimeSinceStartup + 30f;
            while (!predicate()) { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); yield return null; }
        }
        private static async UniTask Observe(UniTask task, Action completed, Action<Exception> failed)
        { try { await task; completed(); } catch (Exception error) { failed(error); } }
    }
}
#endif
