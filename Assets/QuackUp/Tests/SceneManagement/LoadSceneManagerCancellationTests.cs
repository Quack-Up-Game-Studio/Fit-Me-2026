using System;
using System.Collections;
using System.Collections.Generic;
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
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace QuackUp.SceneManagement.Tests
{
    public sealed class LoadSceneManagerCancellationTests
    {
        private LoadSceneManagerConfig _config;
        private LoadSceneManager _manager;
        private Backend _backend;
        private Transition _transition;
        private Stages _stages;
        private Subscriber _subscriber;
        private bool _returned;
        private Exception _error;

        [SetUp]
        public void Setup()
        {
            _returned = false; _error = null;
            var paths = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath).Take(2).ToArray();
            Assert.That(paths.Length, Is.EqualTo(2), "Need two valid SceneAssets; never load them.");
            _config = ScriptableObject.CreateInstance<LoadSceneManagerConfig>();
            SetConfig("SceneReferences", new Dictionary<SceneType, SceneReference>
            {
                { SceneType.MainMenu, new SceneReference { Path = paths[0] } },
                { SceneType.Gameplay, new SceneReference { Path = paths[1] } }
            });
            SetConfig("MinimumLoadingScreenDuration", false);
            _backend = new Backend(paths[0], paths[1]);
            _transition = new Transition();
            _stages = new Stages();
            _subscriber = new Subscriber();
            _manager = new LoadSceneManager(_config, new AudioManagerMock(), _transition,
                _subscriber, _stages, _backend);
            _manager.Start();
            _stages.Events.Clear();
        }

        [TearDown]
        public void Teardown()
        {
            _transition.Cover.TrySetResult();
            _transition.Reveal.TrySetResult();
            _backend.Operation.Complete.TrySetResult();
            _manager.Dispose();
            UnityEngine.Object.DestroyImmediate(_config);
        }

        [UnityTest]
        public IEnumerator CancelBeforeEngineStart_RestoresSourceWithoutLoading()
        {
            Begin();
            _manager.CancelLoadScene();
            yield return null;
            Assert.That(_transition.Reveals, Is.EqualTo(1), "Cancel during cover must restore the source.");
            Assert.That(_backend.Loads, Is.Zero);
            Assert.That(_transition.RevealToken.CanBeCanceled, Is.False);
            Assert.That(_returned, Is.False, "Recovery owns the public call until input is restored.");
            _transition.Reveal.TrySetResult();
            yield return null;
            Assert.That(_returned, Is.True, _error?.ToString());
            Assert.That(_manager.CurrentSceneType, Is.EqualTo(SceneType.MainMenu));
            Assert.That(_manager.FirstSceneLoaded, Is.False);
            Assert.That(_stages.Events.Last().Stage.ToString(), Is.EqualTo("CancelledBeforeLoad"));
            Assert.That(_stages.Events.Last().PreviousSceneType, Is.EqualTo(SceneType.MainMenu));
            Assert.That(_stages.Events.Last().NextSceneType, Is.EqualTo(SceneType.MainMenu));
            Assert.Throws<ObjectDisposedException>(() => { var handle = _transition.CoverToken.WaitHandle; });
        }

        [UnityTest]
        public IEnumerator CancelAfterEngineStart_BelowPointNine_ReleasesActivationAndWaitsForReveal()
        {
            Begin();
            _transition.Cover.TrySetResult();
            Assert.That(_backend.Loads, Is.EqualTo(1));
            Assert.That(_returned, Is.True, _error?.ToString());
            Assert.That(_backend.Operation.Progress, Is.LessThan(0.9f));
            _manager.CancelLoadScene();
            yield return null;
            Assert.That(_backend.Operation.AllowSceneActivation, Is.True,
                "Committed cancellation must release activation even below progress 0.9.");
            _manager.LoadScene(SceneType.MainMenu, LoadSceneMode.Single, false).Forget();
            Assert.That(_transition.Covers, Is.EqualTo(1), "Busy while engine is draining.");
            _backend.Loaded?.Invoke(2, LoadSceneMode.Single);
            _backend.Operation.Complete.TrySetResult();
            yield return null;
            Assert.That(_transition.Reveals, Is.EqualTo(1));
            Assert.That(_transition.RevealToken.CanBeCanceled, Is.False);
            _manager.LoadScene(SceneType.MainMenu, LoadSceneMode.Single, false).Forget();
            Assert.That(_transition.Covers, Is.EqualTo(1), "Busy through final reveal.");
            _transition.Reveal.TrySetResult();
            yield return null;
            Assert.That(_stages.Events.Last().Stage, Is.EqualTo(LoadSceneStage.FinishIn));
            Assert.That(_backend.Subscriptions, Is.Zero);
        }

        [UnityTest]
        public IEnumerator DisposeDuringCommittedLoad_DrainsWithoutDeadOwnerCalls()
        {
            Begin();
            _transition.Cover.TrySetResult();
            var token = _transition.CoverToken;
            var count = _stages.Events.Count;
            _manager.Dispose();
            _manager.Dispose();
            Assert.That(_backend.Operation.AllowSceneActivation, Is.True, "Disposal must never park native activation.");
            Assert.That(_backend.Subscriptions, Is.Zero);
            Assert.That(_subscriber.Count, Is.Zero);
            _backend.Operation.Complete.TrySetResult();
            yield return null;
            Assert.That(_transition.Reveals, Is.Zero, "Disposed owner must not access the borrowed view.");
            Assert.That(_stages.Events.Count, Is.EqualTo(count));
            Assert.Throws<ObjectDisposedException>(() => { var handle = token.WaitHandle; }, "Worker must dispose CTS after drain.");
        }

        [UnityTest]
        public IEnumerator PublisherFaultAfterCommit_ReleasesActivationAndReportsOnlyAfterDrain()
        {
            Exception observed = null;
            Action<Exception> handler = error => observed = error;
            UniTaskScheduler.UnobservedTaskException += handler;
            try
            {
                _stages.OnPublish = message =>
                {
                    if (message.Stage == LoadSceneStage.StartLoading) throw new InvalidOperationException("publisher probe");
                };
                Begin();
                _transition.Cover.TrySetResult();
                Assert.That(_backend.Operation.AllowSceneActivation, Is.True, "Fault must release committed activation.");
                Assert.That(observed, Is.Null, "Do not abandon a still-running engine operation.");
                _manager.LoadScene(SceneType.MainMenu, LoadSceneMode.Single, false).Forget();
                Assert.That(_transition.Covers, Is.EqualTo(1));
                _transition.Reveal.TrySetResult();
                _backend.Operation.Complete.TrySetResult();
                yield return null;
                Assert.That(_transition.Reveals, Is.EqualTo(1), "Live committed faults must still restore input with an uncancelled reveal.");
                Assert.That(observed?.Message, Is.EqualTo("publisher probe"));
                Assert.That(_backend.Subscriptions, Is.Zero);
                Assert.That(_stages.Events.Any(x => x.Stage == LoadSceneStage.FinishIn), Is.False);
                Assert.Throws<ObjectDisposedException>(() => { var handle = _transition.CoverToken.WaitHandle; });
            }
            finally { UniTaskScheduler.UnobservedTaskException -= handler; }
        }

        [UnityTest]
        public IEnumerator AdditivePublisherFaultAfterCommit_ActivatesDestinationAndUnloadsSourceBeforeReleasingOwner()
        {
            Exception observed = null;
            Action<Exception> handler = error => observed = error;
            UniTaskScheduler.UnobservedTaskException += handler;
            try
            {
                _stages.OnPublish = message =>
                {
                    if (message.Stage == LoadSceneStage.StartLoading)
                        throw new InvalidOperationException("additive publisher probe");
                };
                Observe(_manager.LoadScene(SceneType.Gameplay, LoadSceneMode.Additive, false)).Forget();
                _transition.Cover.TrySetResult();
                Assert.That(_backend.Operation.AllowSceneActivation, Is.True);
                _backend.Loaded?.Invoke(2, LoadSceneMode.Additive);
                _backend.Operation.Complete.TrySetResult();
                yield return null;
                Assert.That(_backend.Active, Is.EqualTo(2), "Committed Additive faults must still activate the destination.");
                Assert.That(_backend.Unloads, Is.EqualTo(1), "Unload the source before revealing the committed destination.");
                Assert.That(_manager.CurrentSceneType, Is.EqualTo(SceneType.Gameplay));
                Assert.That(_transition.Reveals, Is.EqualTo(1));
                Assert.That(observed, Is.Null, "Keep the fault with its owner until reveal is finished.");
                _manager.LoadScene(SceneType.MainMenu, LoadSceneMode.Single, false).Forget();
                Assert.That(_transition.Covers, Is.EqualTo(1), "Fault recovery retains busy ownership through reveal.");
                _transition.Reveal.TrySetResult();
                yield return null;
                Assert.That(observed?.Message, Is.EqualTo("additive publisher probe"));
                Assert.That(_backend.Subscriptions, Is.Zero);
                Assert.That(_stages.Events.Any(x => x.Stage == LoadSceneStage.FinishIn), Is.False);
                Assert.Throws<ObjectDisposedException>(() => { var handle = _transition.CoverToken.WaitHandle; });
            }
            finally { UniTaskScheduler.UnobservedTaskException -= handler; }
        }

        [UnityTest]
        public IEnumerator ActiveChangedCallbackFault_IsContainedAndRecoversCommittedAdditiveDestination()
        {
            Exception observed = null;
            Action<Exception> handler = error => observed = error;
            UniTaskScheduler.UnobservedTaskException += handler;
            try
            {
                Observe(_manager.LoadScene(SceneType.Gameplay, LoadSceneMode.Additive, false)).Forget();
                _transition.Cover.TrySetResult();
                _manager.CancelLoadScene();
                yield return null;
                // Unity can activate the destination before delivering its loaded callback.
                _backend.Active = 2;
                _backend.ThrowNextScenePath = true;
                Assert.DoesNotThrow(() => _backend.Changed?.Invoke(1, 2),
                    "Transfer native callback faults to the owning worker instead of escaping the engine event.");
                _backend.Loaded?.Invoke(2, LoadSceneMode.Additive);
                _backend.Operation.Complete.TrySetResult();
                yield return null;
                Assert.That(observed, Is.Null, "Report only after committed recovery has restored input.");
                Assert.That(_backend.Unloads, Is.EqualTo(1), "Fault recovery must finish Additive source cleanup.");
                Assert.That(_transition.Reveals, Is.EqualTo(1), "A missed activation callback must not strand the reveal wait.");
                _manager.LoadScene(SceneType.MainMenu, LoadSceneMode.Single, false).Forget();
                Assert.That(_transition.Covers, Is.EqualTo(1));
                _transition.Reveal.TrySetResult();
                yield return null;
                Assert.That(observed?.Message, Is.EqualTo("active path probe"));
                Assert.That(_backend.Subscriptions, Is.Zero);
                Assert.That(_stages.Events.Any(x => x.Stage == LoadSceneStage.FinishIn), Is.False);
                Assert.Throws<ObjectDisposedException>(() => { var handle = _transition.CoverToken.WaitHandle; });
            }
            finally { UniTaskScheduler.UnobservedTaskException -= handler; }
        }

        [UnityTest]
        public IEnumerator UnrelatedSceneCallbacks_CannotConsumeRequestHandlers()
        {
            Begin();
            _transition.Cover.TrySetResult();
            _manager.CancelLoadScene();
            yield return null;
            _backend.Changed?.Invoke(1, 99);
            _backend.Loaded?.Invoke(99, LoadSceneMode.Single);
            _backend.Loaded?.Invoke(2, LoadSceneMode.Additive);
            Assert.That(_transition.Reveals, Is.Zero, "Wrong scene/mode must not reveal or consume delegates.");
            Assert.That(_backend.Active, Is.EqualTo(1));
            Assert.That(_backend.Subscriptions, Is.EqualTo(2));
            _backend.Loaded?.Invoke(2, LoadSceneMode.Single);
            _backend.Operation.Complete.TrySetResult();
            _transition.Reveal.TrySetResult();
            yield return null;
            Assert.That(_backend.Active, Is.EqualTo(2));
            Assert.That(_backend.Subscriptions, Is.Zero);
        }

        [TestCase("cover")]
        [TestCase("null")]
        [TestCase("backend")]
        public void PrecommitFault_RestoresSourceAndReleasesOwner(string failure)
        {
            Exception observed = null;
            Action<Exception> handler = error => observed = error;
            UniTaskScheduler.UnobservedTaskException += handler;
            try
            {
                _transition.ThrowCover = failure == "cover";
                _backend.Failure = failure;
                Begin();
                _transition.Cover.TrySetResult();
                Assert.That(_transition.Reveals, Is.EqualTo(1), "Precommit fault must restore surviving source.");
                _transition.Reveal.TrySetResult();
                Assert.That(_error ?? observed, Is.Not.Null, "Never drop the original fault.");
                Assert.That(_backend.Subscriptions, Is.Zero);
                _manager.CancelLoadScene(); _manager.CancelLoadScene();
                Assert.That(_manager.FirstSceneLoaded, Is.False);
            }
            finally { UniTaskScheduler.UnobservedTaskException -= handler; }
        }

        [UnityTest]
        public IEnumerator LoadedCallbackFault_IsObservedAfterCompletionAndDoesNotEscapeEngineCallback()
        {
            Exception observed = null;
            Action<Exception> handler = error => observed = error;
            UniTaskScheduler.UnobservedTaskException += handler;
            try
            {
                Begin(); _transition.Cover.TrySetResult(); _manager.CancelLoadScene();
                yield return null;
                _stages.OnPublish = message =>
                {
                    if (message.Stage == LoadSceneStage.FinishLoading) throw new InvalidOperationException("loaded probe");
                };
                Assert.DoesNotThrow(() => _backend.Loaded?.Invoke(2, LoadSceneMode.Single),
                    "Native callbacks must transfer faults to the owning worker, not abandon it.");
                Assert.That(observed, Is.Null);
                _backend.Operation.Complete.TrySetResult();
                yield return null;
                _manager.LoadScene(SceneType.MainMenu, LoadSceneMode.Single, false).Forget();
                Assert.That(_transition.Covers, Is.EqualTo(1), "Faulted request still owns unfinished reveal.");
                _transition.Reveal.TrySetResult();
                yield return null;
                Assert.That(observed?.Message, Is.EqualTo("loaded probe"));
                Assert.That(_backend.Subscriptions, Is.Zero);
                Assert.That(_stages.Events.Any(x => x.Stage == LoadSceneStage.FinishIn), Is.False,
                    "A callback fault must not invent terminal success.");
            }
            finally { UniTaskScheduler.UnobservedTaskException -= handler; }
        }

        [Test]
        public void RecoveryFault_StillReportsOriginalFaultAndReleasesOwner()
        {
            var observed = new List<Exception>();
            Action<Exception> handler = error => observed.Add(error);
            UniTaskScheduler.UnobservedTaskException += handler;
            try
            {
                _transition.ThrowCover = true; _transition.ThrowReveal = true;
                Begin();
                Assert.That(observed.Select(x => x.Message).Concat(new[] { _error?.Message }),
                    Does.Contain("cover probe"), "Failing recovery must not drop the initiating fault.");
                Assert.That(_error?.Message, Is.EqualTo("reveal probe"));
                _manager.CancelLoadScene();
                _transition.ThrowCover = false; _transition.ThrowReveal = false;
                Begin();
                Assert.That(_transition.Covers, Is.EqualTo(2), "Recovery failure must release busy owner.");
            }
            finally { UniTaskScheduler.UnobservedTaskException -= handler; }
        }

        [UnityTest]
        public IEnumerator RequestIdentity_IsCapturedBeforeMutableConfigChanges()
        {
            Begin(); _transition.Cover.TrySetResult();
            _config.SceneReferences.Clear();
            _manager.CancelLoadScene();
            yield return null;
            Assert.That(_backend.Operation.AllowSceneActivation, Is.True);
            Assert.That(_manager.CurrentSceneType, Is.EqualTo(SceneType.Gameplay),
                "Committed identity must not be re-resolved from mutable config.");
            _backend.Loaded?.Invoke(2, LoadSceneMode.Single);
            _backend.Operation.Complete.TrySetResult(); _transition.Reveal.TrySetResult();
            yield return null;
            Assert.That(_backend.Subscriptions, Is.Zero);
            Assert.That(_stages.Events.Last().Stage, Is.EqualTo(LoadSceneStage.FinishIn));
        }

        [TestCase(LoadSceneStage.StartOut)]
        [TestCase(LoadSceneStage.FinishOut)]
        public void CancelFromSynchronousStage_NeverStartsEngine(LoadSceneStage stage)
        {
            _stages.OnPublish = message => { if (message.Stage == stage) _manager.CancelLoadScene(); };
            Begin(); _transition.Cover.TrySetResult();
            Assert.That(_backend.Loads, Is.Zero);
            Assert.That(_transition.Reveals, Is.EqualTo(1));
            _manager.LoadScene(SceneType.MainMenu, LoadSceneMode.Additive, false).Forget();
            Assert.That(_stages.Events.Count(x => x.Stage == LoadSceneStage.StartOut), Is.EqualTo(1), "Busy during recovery.");
            _transition.Reveal.TrySetResult();
            Assert.That(_returned, Is.True, _error?.ToString());
            Assert.That(_stages.Events.Last().Stage, Is.EqualTo(LoadSceneStage.CancelledBeforeLoad));
            _manager.CancelLoadScene(); _manager.CancelLoadScene();
        }

        [Test]
        public void DisposeDuringCover_IsReentrantAndSuppressesRecoveryViews()
        {
            _manager.CancelLoadScene(); _manager.CancelLoadScene();
            Begin();
            var token = _transition.CoverToken;
            using var registration = token.Register(() => _manager.Dispose());
            _manager.Dispose(); _manager.Dispose();
            Assert.That(_returned, Is.True, _error?.ToString());
            Assert.That(_transition.Reveals, Is.Zero);
            Assert.That(_backend.Loads, Is.Zero);
            Assert.That(_subscriber.Count, Is.Zero);
            Assert.Throws<ObjectDisposedException>(() => { var handle = token.WaitHandle; });
            _manager.LoadScene(SceneType.Gameplay, LoadSceneMode.Single, false).Forget();
            Assert.That(_transition.Covers, Is.EqualTo(1));
        }

        [Test]
        public void InvalidDestination_AndDisposedOwner_DoNotAcquireRequest()
        {
            _manager.LoadScene(SceneType.Tutorial, LoadSceneMode.Single, false).Forget();
            Assert.That(_transition.Covers, Is.Zero);
            Assert.That(_stages.Events, Is.Empty);
            Assert.That(_manager.NextScene, Is.Null);
            _manager.Dispose();
            _manager.LoadScene(SceneType.Gameplay, LoadSceneMode.Single, false).Forget();
            Assert.That(_backend.Loads, Is.Zero);
        }

        [UnityTest]
        public IEnumerator MinimumTimerCancellation_ActivatesWithoutWaitingForOptionalDelay()
        {
            SetConfig("MinimumLoadingScreenDuration", true); SetConfig("LoadingScreenDuration", 60f);
            _backend.Operation.Progress = 0.9f;
            Begin(); _transition.Cover.TrySetResult();
            yield return null;
            Assert.That(_backend.Operation.AllowSceneActivation, Is.False);
            _manager.CancelLoadScene();
            yield return null;
            Assert.That(_backend.Operation.AllowSceneActivation, Is.True);
            _backend.Loaded?.Invoke(2, LoadSceneMode.Single);
            _backend.Operation.Complete.TrySetResult(); _transition.Reveal.TrySetResult();
            yield return null;
            Assert.That(_stages.Events.Last().Stage, Is.EqualTo(LoadSceneStage.FinishIn));
            Assert.Throws<ObjectDisposedException>(() => { var handle = _transition.CoverToken.WaitHandle; });
        }

        [UnityTest]
        public IEnumerator NormalSuccess_RetainsBusyUntilEngineAndRevealAndDisposesToken()
        {
            _backend.Operation.Progress = 0.9f;
            Begin(); _transition.Cover.TrySetResult();
            yield return null;
            _backend.Loaded?.Invoke(2, LoadSceneMode.Single);
            _transition.Reveal.TrySetResult();
            yield return null;
            _manager.LoadScene(SceneType.MainMenu, LoadSceneMode.Single, false).Forget();
            Assert.That(_transition.Covers, Is.EqualTo(1), "Reveal alone does not mean engine completion.");
            _backend.Operation.Complete.TrySetResult();
            yield return null;
            Assert.That(_backend.Subscriptions, Is.Zero);
            Assert.Throws<ObjectDisposedException>(() => { var handle = _transition.CoverToken.WaitHandle; });
        }

        [UnityTest]
        public IEnumerator CommittedRevealFault_IsReportedWithoutTerminalSuccess()
        {
            Exception observed = null;
            Action<Exception> handler = error => observed = error;
            UniTaskScheduler.UnobservedTaskException += handler;
            try
            {
                Begin(); _transition.Cover.TrySetResult(); _manager.CancelLoadScene();
                yield return null;
                _transition.ThrowReveal = true;
                _backend.Loaded?.Invoke(2, LoadSceneMode.Single);
                Assert.That(observed, Is.Null, "Fault stays with engine owner until engine finishes.");
                _backend.Operation.Complete.TrySetResult();
                yield return null;
                Assert.That(observed?.Message, Is.EqualTo("reveal probe"));
                Assert.That(_stages.Events.Any(x => x.Stage == LoadSceneStage.FinishIn), Is.False);
                Assert.That(_backend.Subscriptions, Is.Zero);
            }
            finally { UniTaskScheduler.UnobservedTaskException -= handler; }
        }

        [UnityTest]
        public IEnumerator DisposeFromStartIn_DoesNotAccessTransitionViewAfterTeardown()
        {
            _stages.OnPublish = message =>
            {
                if (message.Stage == LoadSceneStage.StartIn) _manager.Dispose();
            };
            Begin();
            _transition.Cover.TrySetResult();
            var token = _transition.CoverToken;
            _manager.CancelLoadScene();
            yield return null;
            _backend.Loaded?.Invoke(2, LoadSceneMode.Single);
            Assert.That(_transition.Reveals, Is.Zero,
                "A StartIn subscriber may dispose the loader; do not access its borrowed view after that callback.");
            _backend.Operation.Complete.TrySetResult();
            yield return null;
            Assert.That(_backend.Subscriptions, Is.Zero);
            Assert.That(_subscriber.Count, Is.Zero);
            Assert.That(_stages.Events.Any(x => x.Stage == LoadSceneStage.FinishIn), Is.False);
            Assert.Throws<ObjectDisposedException>(() => { var handle = token.WaitHandle; });
            Assert.That(_error, Is.Null);
        }

        [UnityTest]
        public IEnumerator DisposeDuringReveal_DoesNotPublishAfterTeardown()
        {
            Begin(); _transition.Cover.TrySetResult(); _manager.CancelLoadScene();
            yield return null;
            _backend.Loaded?.Invoke(2, LoadSceneMode.Single);
            Assert.That(_transition.Reveals, Is.EqualTo(1));
            var count = _stages.Events.Count;
            _manager.Dispose(); _backend.Operation.Complete.TrySetResult();
            yield return null;
            Assert.Throws<ObjectDisposedException>(() => { var handle = _transition.CoverToken.WaitHandle; });
            _transition.Reveal.TrySetResult();
            yield return null;
            Assert.That(_stages.Events.Count, Is.EqualTo(count));
            Assert.That(_backend.Subscriptions, Is.Zero);
        }

        [UnityTest]
        public IEnumerator LoadingSceneFeature_StillLoadsOnlyItsEffectivePath()
        {
            _config.SceneReferences[SceneType.Loading] = _config.SceneReferences[SceneType.Gameplay];
            Observe(_manager.LoadScene(SceneType.Gameplay, LoadSceneMode.Single, true)).Forget();
            _transition.Cover.TrySetResult(); _manager.CancelLoadScene();
            yield return null;
            Assert.That(_manager.CurrentSceneType, Is.EqualTo(SceneType.Loading));
            Assert.That(_backend.Loads, Is.EqualTo(1));
            Assert.That(_manager.NextScene, Is.EqualTo(_config.SceneReferences[SceneType.Loading].Path));
            _backend.Loaded?.Invoke(2, LoadSceneMode.Single);
            _backend.Operation.Complete.TrySetResult(); _transition.Reveal.TrySetResult();
            yield return null;
            Assert.That(_backend.Loads, Is.EqualTo(1), "No loading-scene feature repair/second load in this slice.");
            Assert.That(_stages.Events.Last().NextSceneType, Is.EqualTo(SceneType.Gameplay));
        }

        [Test]
        public void DisposeDuringRecovery_SettlesWithoutWaitingForDestroyedView()
        {
            Begin(); _manager.CancelLoadScene();
            Assert.That(_transition.Reveals, Is.EqualTo(1));
            var token = _transition.CoverToken;
            _manager.Dispose();
            Assert.That(_returned, Is.True, "Disposed owner must detach its wait on the borrowed recovery view.");
            Assert.That(_error, Is.Null);
            Assert.Throws<ObjectDisposedException>(() => { var handle = token.WaitHandle; });
            Assert.That(_stages.Events.Any(x => x.Stage == LoadSceneStage.CancelledBeforeLoad), Is.False);
        }

        private void Begin() => Observe(_manager.LoadScene(SceneType.Gameplay, LoadSceneMode.Single, false)).Forget();
        private async UniTask Observe(UniTask task)
        {
            try { await task; _returned = true; }
            catch (Exception error) { _error = error; }
        }
        private void SetConfig<T>(string property, T value) => typeof(LoadSceneManagerConfig)
            .GetField("<" + property + ">k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(_config, value);

        private sealed class Transition : ITransitionable
        {
            public readonly UniTaskCompletionSource Cover = new UniTaskCompletionSource();
            public readonly UniTaskCompletionSource Reveal = new UniTaskCompletionSource();
            public int Covers, Reveals;
            public bool ThrowCover, ThrowReveal;
            public CancellationToken CoverToken, RevealToken;
            public async UniTask TransitionIn(CancellationToken cancellationToken = default)
            {
                Covers++; CoverToken = cancellationToken;
                if (ThrowCover) throw new InvalidOperationException("cover probe");
                await Cover.Task.AttachExternalCancellation(cancellationToken);
            }
            public async UniTask TransitionOut(CancellationToken cancellationToken = default)
            {
                Reveals++; RevealToken = cancellationToken;
                if (ThrowReveal) throw new InvalidOperationException("reveal probe");
                await Reveal.Task.AttachExternalCancellation(cancellationToken);
            }
        }
        private sealed class Operation : ISceneLoadOperation
        {
            public float Progress { get; set; }
            public bool IsDone => Complete.Task.Status == UniTaskStatus.Succeeded;
            public bool AllowSceneActivation { get; set; }
            public readonly UniTaskCompletionSource Complete = new UniTaskCompletionSource();
            public UniTask WaitForCompletion() => Complete.Task;
        }
        private sealed class Backend : ISceneLoadBackend
        {
            private readonly string _source, _destination;
            public Backend(string source, string destination) { _source = source; _destination = destination; }
            public readonly Operation Operation = new Operation();
            public int Loads, Active = 1, Unloads;
            public Action<int, LoadSceneMode> Loaded;
            public Action<int, int> Changed;
            public int Subscriptions;
            public int GetActiveSceneHandle() => Active;
            public bool ThrowNextScenePath;
            public string GetScenePath(int handle)
            {
                if (ThrowNextScenePath)
                {
                    ThrowNextScenePath = false;
                    throw new InvalidOperationException("active path probe");
                }
                return handle == 1 ? _source : handle == 2 ? _destination : "wrong";
            }
            public string Failure;
            public ISceneLoadOperation BeginLoad(string path, LoadSceneMode mode)
            {
                Loads++;
                if (Failure == "backend") throw new InvalidOperationException("backend probe");
                return Failure == "null" ? null : Operation;
            }
            public bool SetActiveScene(int handle) { var old = Active; Active = handle; if (old != handle) Changed?.Invoke(old, handle); return true; }
            public UniTask UnloadScene(int handle) { Unloads++; return UniTask.CompletedTask; }
            public IDisposable SubscribeLoaded(Action<int, LoadSceneMode> callback)
            {
                Loaded += callback; Subscriptions++;
                return new Subscription(() => { Loaded -= callback; Subscriptions--; });
            }
            public IDisposable SubscribeActiveChanged(Action<int, int> callback)
            {
                Changed += callback; Subscriptions++;
                return new Subscription(() => { Changed -= callback; Subscriptions--; });
            }
        }
        private sealed class Stages : IPublisher<LoadSceneStageEvent>
        {
            public readonly List<LoadSceneStageEvent> Events = new List<LoadSceneStageEvent>();
            public Action<LoadSceneStageEvent> OnPublish;
            public void Publish(LoadSceneStageEvent message) { Events.Add(message); OnPublish?.Invoke(message); }
        }
        private sealed class Subscriber : ISubscriber<LoadSceneEvent>
        {
            public int Count;
            public IDisposable Subscribe(IMessageHandler<LoadSceneEvent> handler, params MessageHandlerFilter<LoadSceneEvent>[] filters)
            { Count++; return new Subscription(() => Count--); }
        }
        private sealed class Subscription : IDisposable
        {
            private Action _dispose;
            public Subscription(Action dispose) => _dispose = dispose;
            public void Dispose() { var dispose = _dispose; _dispose = null; dispose?.Invoke(); }
        }
    }
}
