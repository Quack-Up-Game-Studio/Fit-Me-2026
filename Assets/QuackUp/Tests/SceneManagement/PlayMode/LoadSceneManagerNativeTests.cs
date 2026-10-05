#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using MessagePipe;
using NUnit.Framework;
using QuackUp.Audio;
using QuackUp.Utils;
using Redcode.Extensions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace QuackUp.SceneManagement.PlayModeTests
{
    // Native scene fixtures are created before entering PlayMode, never gameplay scenes.
    public sealed class EmptySceneFixtures : IPrebuildSetup, IPostBuildCleanup
    {
        public const string Folder = "Assets/QuackUp/Tests/SceneManagement/PlayMode/TemporaryScenes";
        public const string Source = Folder + "/Source.unity";
        public const string Destination = Folder + "/Destination.unity";
        private const string Backup = Folder + "/BuildSettingsBackup.json";

        [Serializable]
        private sealed class BuildSettingsBackup
        {
            public string[] paths;
            public bool[] enabled;
            public string[] preloadedAssets;
        }

        public void Setup()
        {
            if (Directory.Exists(Folder))
                throw new InvalidOperationException("Refusing existing temporary fixture folder: " + Folder);
            AssetDatabase.CreateFolder("Assets/QuackUp/Tests/SceneManagement/PlayMode", "TemporaryScenes");
            var scenes = EditorBuildSettings.scenes;
            File.WriteAllText(Backup, JsonUtility.ToJson(new BuildSettingsBackup
            {
                paths = scenes.Select(x => x.path).ToArray(),
                enabled = scenes.Select(x => x.enabled).ToArray(),
                preloadedAssets = PlayerSettings.GetPreloadedAssets().Select(x =>
                    x ? GlobalObjectId.GetGlobalObjectIdSlow(x).ToString() : null).ToArray()
            }));
            try
            {
                // These native fixtures must not bootstrap the game's ads,
                // analytics, IAP or audio services while entering PlayMode.
                PlayerSettings.SetPreloadedAssets(PlayerSettings.GetPreloadedAssets()
                    .Where(x => !(x is VContainer.Unity.VContainerSettings)).ToArray());
                CreateEmptyScene(Source);
                CreateEmptyScene(Destination);
                EnableFixtures();
            }
            catch
            {
                Cleanup();
                throw;
            }
        }

        private static void CreateEmptyScene(string path)
        {
            var previousActive = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                if (!EditorSceneManager.SaveScene(scene, path))
                    throw new InvalidOperationException("Cannot save native fixture " + path);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previousActive.IsValid()) SceneManager.SetActiveScene(previousActive);
            }
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Not.Null, path);
        }

        public static void EnableFixtures()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(Source, true),
                new EditorBuildSettingsScene(Destination, true)
            };
        }

        public static void RestoreBuildSettings()
        {
            if (!File.Exists(Backup)) return;
            var backup = JsonUtility.FromJson<BuildSettingsBackup>(File.ReadAllText(Backup));
            EditorBuildSettings.scenes = backup.paths.Select((path, i) =>
                new EditorBuildSettingsScene(path, backup.enabled[i])).ToArray();
        }

        public void Cleanup()
        {
            try
            {
                RestoreBuildSettings();
                // Restore preload assets only once PlayMode has ended. Doing it
                // in each test's finally can bootstrap the game between tests.
                if (File.Exists(Backup))
                {
                    var backup = JsonUtility.FromJson<BuildSettingsBackup>(File.ReadAllText(Backup));
                    if (backup.preloadedAssets != null)
                        PlayerSettings.SetPreloadedAssets(backup.preloadedAssets.Select(x =>
                            !string.IsNullOrEmpty(x) && GlobalObjectId.TryParse(x, out var id)
                                ? GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) : null).ToArray());
                }
            }
            finally
            {
                if (Directory.Exists(Folder)) AssetDatabase.DeleteAsset(Folder);
            }
        }
    }

    [PrebuildSetup(typeof(EmptySceneFixtures))]
    [PostBuildCleanup(typeof(EmptySceneFixtures))]
    public sealed class LoadSceneManagerNativeTests
    {
        [UnityTest]
        public IEnumerator Single_ReturnsAfterCover_ThenNativeSwitchAndRevealFinish()
            => Characterize(LoadSceneMode.Single);

        [UnityTest]
        public IEnumerator Additive_ReturnsAfterCover_ThenNativeSwitchAndRevealFinish()
            => Characterize(LoadSceneMode.Additive);

        [UnityTest]
        public IEnumerator CancelDuringNativeCover_RestoresSourceWithoutDestination() => Characterize(LoadSceneMode.Single, true);

        [UnityTest]
        public IEnumerator CancelImmediatelyAfterNativeCommit_Single_BypassesMinimumTimer() => Characterize(LoadSceneMode.Single, false, true);

        [UnityTest]
        public IEnumerator CancelImmediatelyAfterNativeCommit_Additive_BypassesMinimumTimer() => Characterize(LoadSceneMode.Additive, false, true);

        private static IEnumerator Characterize(LoadSceneMode mode, bool cancelBefore = false, bool cancelCommitted = false)
        {
            // Preserve the actual runner roots across BOTH source and destination Single loads.
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                Object.DontDestroyOnLoad(root);

            var transition = new ControlledTransition();
            var stages = new StageRecorder();
            var subscriber = new LoadSubscriber();
            LoadSceneManager manager = null;
            LoadSceneManagerConfig config = null;
            var originalTimeScale = Time.timeScale;
            var returned = false;
            Exception returnError = null;
            try
            {
                EmptySceneFixtures.EnableFixtures();
                var sourceLoad = SceneManager.LoadSceneAsync(EmptySceneFixtures.Source, LoadSceneMode.Single);
                Assert.That(sourceLoad, Is.Not.Null);
                yield return WaitFor(() => sourceLoad.isDone, "native source load");
                Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(EmptySceneFixtures.Source));
                config = ScriptableObject.CreateInstance<LoadSceneManagerConfig>();
                SetConfig(config, "SceneReferences", new Dictionary<SceneType, SceneReference>
                {
                    { SceneType.MainMenu, new SceneReference { Path = EmptySceneFixtures.Source } },
                    { SceneType.Gameplay, new SceneReference { Path = EmptySceneFixtures.Destination } }
                });
                SetConfig(config, "MinimumLoadingScreenDuration", cancelCommitted);
                SetConfig(config, "LoadingScreenDuration", 60f);
                Assert.That(config.SceneReferences[SceneType.Gameplay].Path, Is.EqualTo(EmptySceneFixtures.Destination));
                var backend = new ObservedBackend();
                manager = CreateManager(config, transition, subscriber, stages, backend);
                if (cancelCommitted)
                    stages.OnPublish = message =>
                    {
                        if (message.Stage != LoadSceneStage.StartLoading) return;
                        Assert.That(backend.Operation.Progress, Is.LessThan(0.9f), "Real native cancellation before activation-ready progress.");
                        TestContext.Out.WriteLine("NATIVE_COMMIT_CANCEL_PROGRESS=" + backend.Operation.Progress);
                        manager.CancelLoadScene();
                    };
                manager.Start();
                CollectionAssert.AreEqual(new[] { LoadSceneStage.FinishLoading, LoadSceneStage.FinishIn }, stages.Stages);
                stages.Clear();

                ObserveReturn(manager.LoadScene(SceneType.Gameplay, mode, false),
                    () => { returned = true; stages.Timeline.Add("LoadSceneReturned"); },
                    error => returnError = error).Forget();
                Assert.That(transition.CoverStarted, Is.True);
                Assert.That(returned, Is.False, "LoadScene must still await the controlled cover.");
                CollectionAssert.AreEqual(new[] { LoadSceneStage.StartOut }, stages.Stages);
                Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(EmptySceneFixtures.Source));
                manager.LoadScene(SceneType.Gameplay, mode, false).Forget();
                Assert.That(stages.Stages.Count(x => x == LoadSceneStage.StartOut), Is.EqualTo(1),
                    "Second request during cover must be rejected.");
                if (cancelBefore)
                {
                    manager.CancelLoadScene();
                    yield return WaitFor(() => transition.RevealStarted, "source recovery reveal");
                    Assert.That(returned, Is.False);
                    Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(EmptySceneFixtures.Source));
                    Assert.That(SceneManager.GetSceneByPath(EmptySceneFixtures.Destination).isLoaded, Is.False);
                    transition.Reveal.TrySetResult();
                    yield return WaitFor(() => returned || returnError != null, "source recovery return");
                    Assert.That(returnError, Is.Null, returnError?.ToString());
                    CollectionAssert.AreEqual(new[] { LoadSceneStage.StartOut, LoadSceneStage.CancelledBeforeLoad }, stages.Stages);
                    Assert.That(stages.Events.Last().NextSceneType, Is.EqualTo(SceneType.MainMenu));
                    Assert.That(manager.CurrentSceneType, Is.EqualTo(SceneType.MainMenu));
                    Assert.That(manager.FirstSceneLoaded, Is.False);
                    TestContext.Out.WriteLine("NATIVE_CANCEL_BEFORE source=" + SceneManager.GetActiveScene().path);
                    yield break;
                }
                transition.Cover.TrySetResult();
                yield return WaitFor(() => returned || returnError != null, "LoadScene return boundary");
                Assert.That(returnError, Is.Null, returnError?.ToString());
                Assert.That(transition.RevealCompleted, Is.False, "Public return is not full-switch completion.");
                Assert.That(stages.Stages.Contains(LoadSceneStage.FinishIn), Is.False);
                CollectionAssert.AreEqual(new[] { LoadSceneStage.StartOut, LoadSceneStage.FinishOut, LoadSceneStage.StartLoading },
                    stages.Stages.Take(3).ToArray());

                if (cancelCommitted)
                {
                    manager.CancelLoadScene();
                    TestContext.Out.WriteLine("NATIVE_CANCEL_COMMITTED mode=" + mode + " minimumSeconds=60");
                }
                yield return WaitFor(() => transition.RevealStarted &&
                    stages.Stages.Contains(LoadSceneStage.FinishLoading), "native destination activation and reveal entry");
                var destination = SceneManager.GetSceneByPath(EmptySceneFixtures.Destination);
                Assert.That(destination.IsValid() && destination.isLoaded, Is.True, "Real engine destination must be loaded.");
                Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(EmptySceneFixtures.Destination));
                Assert.That(SceneManager.GetSceneByPath(EmptySceneFixtures.Source).isLoaded, Is.False,
                    "Source must be unloaded, including Additive's explicit unload.");
                Assert.That(manager.CurrentSceneType, Is.EqualTo(SceneType.Gameplay));
                Assert.That(manager.PreviousSceneType, Is.EqualTo(SceneType.MainMenu));
                Assert.That(manager.FirstSceneLoaded, Is.True);
                Assert.That(transition.CoverCompleted, Is.True);
                Assert.That(transition.RevealCompleted, Is.False);
                Assert.That(stages.Stages.Contains(LoadSceneStage.FinishIn), Is.False);

                transition.Reveal.TrySetResult();
                yield return WaitFor(() => stages.Stages.Contains(LoadSceneStage.FinishIn), "overlay reveal completion");
                Assert.That(transition.RevealCompleted, Is.True);
                var expected = mode == LoadSceneMode.Single
                    ? new[] { LoadSceneStage.StartOut, LoadSceneStage.FinishOut, LoadSceneStage.StartLoading,
                        LoadSceneStage.StartIn, LoadSceneStage.FinishLoading, LoadSceneStage.FinishIn }
                    : new[] { LoadSceneStage.StartOut, LoadSceneStage.FinishOut, LoadSceneStage.StartLoading,
                        LoadSceneStage.FinishLoading, LoadSceneStage.StartIn, LoadSceneStage.FinishIn };
                CollectionAssert.AreEqual(expected, stages.Stages,
                    "Unity native callback order differs between Single and Additive.");
                Assert.That(stages.Timeline.IndexOf("LoadSceneReturned"), Is.EqualTo(3),
                    "Public return occurs after StartLoading and before native activation/reveal callbacks.");
                Assert.That(stages.Events.All(x => x.PreviousSceneType == SceneType.MainMenu &&
                    x.NextSceneType == SceneType.Gameplay), Is.True);
                Assert.That(subscriber.SubscriptionCount, Is.EqualTo(1));
                TestContext.Out.WriteLine("NATIVE_CHARACTERIZATION mode=" + mode + " timeline=" + string.Join(" -> ", stages.Timeline));
                TestContext.Out.WriteLine("NATIVE_SCENE active=" + destination.path + " sourceLoaded=false revealCompleted=true");
            }
            finally
            {
                // Gates cannot leave the native worker parked if an assertion fails.
                transition.Cover.TrySetResult();
                transition.Reveal.TrySetResult();
                manager?.Dispose();
                if (config) Object.DestroyImmediate(config);
                Time.timeScale = originalTimeScale;
                EmptySceneFixtures.RestoreBuildSettings();
            }
            Assert.That(subscriber.SubscriptionCount, Is.Zero, "Manager must release MessagePipe subscription.");
        }

        // Deliberately isolated construction point for the parent's later backend injection.
        private static LoadSceneManager CreateManager(LoadSceneManagerConfig config, ITransitionable transition,
            ISubscriber<LoadSceneEvent> subscriber, IPublisher<LoadSceneStageEvent> publisher, ISceneLoadBackend backend)
            => new LoadSceneManager(config, new AudioManagerMock(), transition, subscriber, publisher, backend);

        private static void SetConfig<T>(LoadSceneManagerConfig config, string property, T value)
        {
            var field = typeof(LoadSceneManagerConfig).GetField("<" + property + ">k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Config backing field for " + property);
            field.SetValue(config, value);
        }

        private static IEnumerator WaitFor(Func<bool> predicate, string boundary)
        {
            var deadline = Time.realtimeSinceStartup + 30f;
            while (!predicate())
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Timed out waiting for " + boundary);
                yield return null;
            }
        }

        private static async UniTask ObserveReturn(UniTask task, Action returned, Action<Exception> failed)
        {
            try { await task; returned(); }
            catch (Exception error) { failed(error); }
        }

        private sealed class ControlledTransition : ITransitionable
        {
            public readonly UniTaskCompletionSource Cover = new UniTaskCompletionSource();
            public readonly UniTaskCompletionSource Reveal = new UniTaskCompletionSource();
            public bool CoverStarted, CoverCompleted, RevealStarted, RevealCompleted;
            public async UniTask TransitionIn(CancellationToken cancellationToken = default)
            {
                CoverStarted = true;
                await Cover.Task.AttachExternalCancellation(cancellationToken);
                CoverCompleted = true;
            }
            public async UniTask TransitionOut(CancellationToken cancellationToken = default)
            {
                RevealStarted = true;
                await Reveal.Task;
                RevealCompleted = true;
            }
        }

        private sealed class StageRecorder : IPublisher<LoadSceneStageEvent>
        {
            public readonly List<LoadSceneStageEvent> Events = new List<LoadSceneStageEvent>();
            public readonly List<string> Timeline = new List<string>();
            public Action<LoadSceneStageEvent> OnPublish;
            public LoadSceneStage[] Stages => Events.Select(x => x.Stage).ToArray();
            public void Publish(LoadSceneStageEvent message)
            {
                Events.Add(message);
                Timeline.Add(message.Stage.ToString());
                OnPublish?.Invoke(message);
            }
            public void Clear() { Events.Clear(); Timeline.Clear(); }
        }

        private sealed class ObservedBackend : ISceneLoadBackend
        {
            private readonly UnitySceneLoadBackend _native = new UnitySceneLoadBackend();
            public ISceneLoadOperation Operation;
            public int GetActiveSceneHandle() => _native.GetActiveSceneHandle();
            public string GetScenePath(int handle) => _native.GetScenePath(handle);
            public ISceneLoadOperation BeginLoad(string path, LoadSceneMode mode) => Operation = _native.BeginLoad(path, mode);
            public bool SetActiveScene(int handle) => _native.SetActiveScene(handle);
            public UniTask UnloadScene(int handle) => _native.UnloadScene(handle);
            public IDisposable SubscribeLoaded(Action<int, LoadSceneMode> callback) => _native.SubscribeLoaded(callback);
            public IDisposable SubscribeActiveChanged(Action<int, int> callback) => _native.SubscribeActiveChanged(callback);
        }

        private sealed class LoadSubscriber : ISubscriber<LoadSceneEvent>
        {
            private readonly List<IMessageHandler<LoadSceneEvent>> _handlers =
                new List<IMessageHandler<LoadSceneEvent>>();
            public int SubscriptionCount => _handlers.Count;
            public IDisposable Subscribe(IMessageHandler<LoadSceneEvent> handler,
                params MessageHandlerFilter<LoadSceneEvent>[] filters)
            {
                if (filters.Length != 0) throw new NotSupportedException("Fixture has no filter pipeline.");
                _handlers.Add(handler);
                return new Subscription(() => _handlers.Remove(handler));
            }
            public void Publish(LoadSceneEvent message)
            {
                foreach (var handler in _handlers.ToArray()) handler.Handle(message);
            }
        }

        private sealed class Subscription : IDisposable
        {
            private Action _dispose;
            public Subscription(Action dispose) { _dispose = dispose; }
            public void Dispose() { var dispose = _dispose; _dispose = null; dispose?.Invoke(); }
        }
    }
}
#endif
