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
            LogAssert.Expect(LogType.Exception, "InvalidOperationException: transition fault");
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
            LogAssert.Expect(LogType.Exception, "InvalidOperationException: incoming fault");

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

        private static PanelManager CreateManager(TestPanelViewModel from, TestPanelViewModel to)
        {
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
            {
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
