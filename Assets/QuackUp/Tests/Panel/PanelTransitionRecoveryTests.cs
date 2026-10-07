using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using R3;
using UnityEngine;
using UnityEngine.TestTools;
using QuackUp.Utils;

namespace FitMe.Panel.Tests
{
    public sealed class PanelTransitionRecoveryTests
    {
        [UnityTest]
        public IEnumerator PanelView_ReentrantCompletion_DoesNotClearSuccessorState()
        {
            var viewObject = new GameObject("PanelViewTest");
            var view = viewObject.AddComponent<TestPanelView>();
            view.Prepare();
            var viewModel = new TestPanelViewModel();
            view.Construct(viewModel);

            using var incoming = new Promise<Unit>();
            view.TriggerIn(incoming, "in");
            view.CompleteIncoming();
            yield return null;
            Assert.That(incoming.Task.Status, Is.EqualTo(UniTaskStatus.Succeeded));

            using var outgoing = new Promise<Unit>();
            view.TriggerOut(outgoing, "out");
            Assert.That(viewModel.TransitionState.Value, Is.EqualTo(TransitionState.Out));
            view.CompleteOutgoing();
            yield return null;

            Assert.That(viewModel.TransitionState.Value, Is.EqualTo(TransitionState.None));
            view.Dispose();
            UnityEngine.Object.DestroyImmediate(viewObject);
        }

        [UnityTest]
        public IEnumerator PanelView_TransitionFault_FaultsPromiseAndFinalizesState()
        {
            var viewObject = new GameObject("PanelViewFaultTest");
            var view = viewObject.AddComponent<TestPanelView>();
            view.Prepare();
            var viewModel = new TestPanelViewModel();
            view.Construct(viewModel);

            using var promise = new Promise<Unit>();
            view.ThrowOnTransition = true;
            view.TriggerIn(promise, "fault");
            yield return null;

            Assert.That(promise.Task.Status, Is.EqualTo(UniTaskStatus.Faulted));
            Assert.That(viewModel.TransitionState.Value, Is.EqualTo(TransitionState.None));
            view.Dispose();
            UnityEngine.Object.DestroyImmediate(viewObject);
        }

        [UnityTest]
        public IEnumerator PanelManager_ParallelIncomingFault_DrainsOutgoingBeforePropagating()
        {
            var from = new TestPanelViewModel();
            var to = new TestPanelViewModel { ThrowOnIncoming = true };
            var manager = CreateManager(from, to);
            var crossfade = manager.Crossfade("from", "to", new CrossfadeSettings
            {
                crossFadeType = CrossfadeType.Parallel,
                customOffset = 0f
            });
            yield return null;

            Assert.That(crossfade.Status, Is.EqualTo(UniTaskStatus.Pending),
                "Crossfade must observe the pending outgoing operation before settling.");
            Assert.That(from.OutgoingStarted, Is.True);
            from.CompleteOutgoing();
            yield return null;

            Assert.That(crossfade.Status, Is.EqualTo(UniTaskStatus.Faulted));
            from.Dispose();
            to.Dispose();
        }

        [UnityTest]
        public IEnumerator PanelManager_NestedTransitionDuringVisibilityPublication_OlderOperationDoesNotSupersede()
        {
            return RunNestedTransitionCase();

            static IEnumerator RunNestedTransitionCase()
            {
                var from = new TestPanelViewModel("from");
                var to = new TestPanelViewModel("to");
                var nestedTarget = new TestPanelViewModel("nested");
                var manager = CreateManager(from, to);
                AddPanel(manager, "nested", nestedTarget);
                var nestedStarted = false;
                UniTask nested = default;
                to.VisibilityState.Subscribe(state =>
                {
                    if (state != Panel.VisibilityState.Visible || nestedStarted) return;
                    nestedStarted = true;
                    nested = manager.Crossfade("to", "nested", new CrossfadeSettings { crossFadeType = CrossfadeType.None });
                });

                var outer = manager.Crossfade("from", "to", new CrossfadeSettings { crossFadeType = CrossfadeType.None });
                yield return outer.ToCoroutine();
                yield return nested.ToCoroutine();
                yield return null;

                Assert.That(nestedStarted, Is.True);
                Assert.That(to.VisibilityState.Value, Is.EqualTo(Panel.VisibilityState.Hidden));
                Assert.That(nestedTarget.VisibilityState.Value, Is.EqualTo(Panel.VisibilityState.Visible));
                from.Dispose();
                to.Dispose();
                nestedTarget.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator PanelView_CanceledAfterPromiseDisposal_DoesNotThrow()
        {
            return RunLateCancellationCase();

            static IEnumerator RunLateCancellationCase()
            {
                var viewObject = new GameObject("PanelViewLateCancelTest");
                var view = viewObject.AddComponent<TestPanelView>();
                view.Prepare();
                var viewModel = new TestPanelViewModel();
                view.Construct(viewModel);

                var promise = new Promise<Unit>();
                view.TriggerIn(promise, "in");
                promise.Dispose();
                view.CancelIncoming();
                yield return null;

                Assert.That(viewModel.TransitionState.Value, Is.EqualTo(TransitionState.None));
                view.Dispose();
                UnityEngine.Object.DestroyImmediate(viewObject);
            }
        }

        [UnityTest]
        public IEnumerator PanelView_CompletionContinuation_ObservesFinalTransitionState()
        {
            return RunCompletionOrderingCase();

            static IEnumerator RunCompletionOrderingCase()
            {
                var viewObject = new GameObject("PanelViewCompletionOrderingTest");
                var view = viewObject.AddComponent<TestPanelView>();
                view.Prepare();
                var viewModel = new TestPanelViewModel();
                view.Construct(viewModel);

                using var promise = new Promise<Unit>();
                var stateWhenCompleted = TransitionState.In;
                var sawNoneBeforeCompletion = false;
                var observer = viewModel.TransitionState.Subscribe(state =>
                {
                    if (state == TransitionState.None)
                        sawNoneBeforeCompletion = true;
                });
                view.TriggerIn(promise, "in");
                var observation = ObserveCompletion(promise.Task, () => stateWhenCompleted = viewModel.TransitionState.Value);
                view.CompleteIncoming();
                yield return observation.ToCoroutine();

                Assert.That(stateWhenCompleted, Is.EqualTo(TransitionState.None));
                Assert.That(sawNoneBeforeCompletion, Is.True);
                observer.Dispose();
                view.Dispose();
                UnityEngine.Object.DestroyImmediate(viewObject);
            }
        }

        private static async UniTask ObserveCompletion(UniTask<Unit> task, Action onCompleted)
        {
            await task;
            onCompleted();
        }

        private static void AddPanel(PanelManager manager, string panelId, IPanelViewModel panel)
        {
            panel.PanelId = panelId;
            var panels = (Dictionary<string, IPanelViewModel>)typeof(PanelManager)
                .GetField("_panels", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(manager);
            panels.Add(panelId, panel);
        }

        private static PanelManager CreateManager(TestPanelViewModel from, TestPanelViewModel to)
        {
            from.PanelId = "from";
            to.PanelId = "to";
            var manager = new PanelManager(new Dictionary<string, PanelLifetimeScope>(), "from");
            var panels = (Dictionary<string, IPanelViewModel>)typeof(PanelManager)
                .GetField("_panels", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(manager);
            panels.Add("from", from);
            panels.Add("to", to);
            return manager;
        }

        private sealed class TestPanelView : PanelView
        {
            private readonly UniTaskCompletionSource _incoming = new();
            private readonly UniTaskCompletionSource _outgoing = new();
            public bool ThrowOnTransition;

            public void Prepare() => canvasGroup = gameObject.AddComponent<CanvasGroup>();

            public void TriggerIn(Promise<Unit> promise, string key) =>
                typeof(PanelView).GetMethod("OnTransitionIn", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(this, new object[] { new TransitionCommandData(promise, key) });

            public void TriggerOut(Promise<Unit> promise, string key) =>
                typeof(PanelView).GetMethod("OnTransitionOut", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(this, new object[] { new TransitionCommandData(promise, key) });

            public void CompleteIncoming() => _incoming.TrySetResult();
            public void CompleteOutgoing() => _outgoing.TrySetResult();
            public void CancelIncoming() => _incoming.TrySetCanceled();

            protected override UniTask Transition(string transitionKey, bool direction, CancellationToken cancellationToken = default)
            {
                if (ThrowOnTransition)
                    return UniTask.FromException(new InvalidOperationException("transition fault"));
                return (direction ? _incoming : _outgoing).Task.AttachExternalCancellation(cancellationToken);
            }
        }

        private sealed class TestPanelViewModel : IPanelViewModel
        {
            public string PanelId { get; set; }
            public ReactiveProperty<VisibilityState> VisibilityState { get; } = new(Panel.VisibilityState.Hidden);
            public ReactiveProperty<TransitionState> TransitionState { get; set; } = new(Panel.TransitionState.None);
            public ReactiveProperty<InputState> InputState { get; } = new(Panel.InputState.Inactive);
            public ReactiveCommand<TransitionCommandData> TransitionInCommand { get; } = new();
            public ReactiveCommand<TransitionCommandData> TransitionOutCommand { get; } = new();
            public ReactiveCommand<CrossfadeCommandData> CrossfadeCommand { get; } = new();
            public bool ThrowOnIncoming;
            public bool OutgoingStarted { get; private set; }
            private Promise<Unit> _outgoingPromise;

            public TestPanelViewModel()
                : this(null)
            {
            }

            public TestPanelViewModel(string panelId)
            {
                PanelId = panelId;
                TransitionInCommand.Subscribe(data =>
                {
                    if (ThrowOnIncoming)
                        data.Promise.TrySetException(new InvalidOperationException("incoming fault"));
                    else
                        data.Promise.TrySetResult(Unit.Default);
                });
                TransitionOutCommand.Subscribe(data =>
                {
                    OutgoingStarted = true;
                    _outgoingPromise = data.Promise;
                });
            }

            public void CompleteOutgoing()
            {
                _outgoingPromise?.TrySetResult(Unit.Default);
            }

            public void Dispose()
            {
                VisibilityState.Dispose();
                TransitionState.Dispose();
                InputState.Dispose();
                TransitionInCommand.Dispose();
                TransitionOutCommand.Dispose();
                CrossfadeCommand.Dispose();
            }
        }
    }
}
