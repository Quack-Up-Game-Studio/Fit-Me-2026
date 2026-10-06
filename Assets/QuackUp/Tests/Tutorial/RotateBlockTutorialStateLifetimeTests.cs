using System;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using FitMe.Panel;
using FitMe.Tutorial;
using NUnit.Framework;
using R3;
using UnityEngine;

namespace FitMe.Tutorial.Tests
{
    public sealed class TutorialHintStateLifetimeTests
    {
        [Test]
        public async System.Threading.Tasks.Task Exit_HideTaskOwnsTokenUntilTransitionFinishes()
        {
            var gameObject = new GameObject(nameof(TutorialHintStateLifetimeTests));
            var hint = gameObject.AddComponent<ControlledFloatingHint>();
            var state = new RotateBlockTutorialState();
            SetField(state, "rotateBlockHint", hint);
            SetField(state, "_blockRotateSubscription", new DisposableBag());

            try
            {
                await state.Exit();

                var token = hint.CapturedToken;
                Assert.That(token.IsCancellationRequested, Is.False,
                    "Disposing the state must not cancel its fire-and-forget hide transition.");

                hint.CompleteTransition();
                await hint.TransitionCompletion;
                Assert.Throws<ObjectDisposedException>(() =>
                {
                    var waitHandle = token.WaitHandle;
                });
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public async System.Threading.Tasks.Task PlaceBlockExit_StartsNonBlockingHideTransition()
        {
            var gameObject = new GameObject(nameof(PlaceBlockExit_StartsNonBlockingHideTransition));
            var hint = gameObject.AddComponent<ControlledFloatingHint>();
            var state = new PlaceBlockTutorialState();
            SetField(state, "placeBlockHint", hint);
            SetField(state, "_blockPlacedSubscription", new DisposableBag());

            try
            {
                await state.Exit();

                Assert.That(hint.TransitionStarted, Is.True,
                    "PlaceBlock exit should start the same non-blocking hide transition as the other hint states.");
                var token = hint.CapturedToken;
                Assert.That(token.IsCancellationRequested, Is.False);
                hint.CompleteTransition();
                await hint.TransitionCompletion;
                Assert.Throws<ObjectDisposedException>(() =>
                {
                    var waitHandle = token.WaitHandle;
                });
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public async System.Threading.Tasks.Task SwapHideTask_DisposesItsTokenAfterItCompletes()
        {
            var gameObject = new GameObject(nameof(SwapHideTask_DisposesItsTokenAfterItCompletes));
            var hint = gameObject.AddComponent<ControlledFloatingHint>();
            var state = new SwapBlockTutorialState();
            SetField(state, "swapBlockHint", hint);

            try
            {
                var hideTask = (UniTask)typeof(SwapBlockTutorialState)
                    .GetMethod("HideHint", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(state, null);
                var token = hint.CapturedToken;
                state.Dispose();

                Assert.That(hint.TransitionStarted, Is.True);
                Assert.That(token.IsCancellationRequested, Is.False,
                    "Disposing the state must leave the active hide task in control of its token.");

                hint.CompleteTransition();
                await hideTask;
                Assert.Throws<ObjectDisposedException>(() =>
                {
                    var waitHandle = token.WaitHandle;
                });
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }
    }

    public sealed class ControlledFloatingHint : GeneralFloatingUIElement
    {
        private readonly UniTaskCompletionSource<bool> _completion = new();
        public CancellationToken CapturedToken { get; private set; }
        public bool TransitionStarted { get; private set; }
        public UniTask TransitionCompletion => _completion.Task;

        public override UniTask Animate(CancellationToken cancellationToken = default) => UniTask.CompletedTask;
        public override UniTask TransitionIn(CancellationToken cancellationToken = default) => UniTask.CompletedTask;

        public override UniTask TransitionOut(CancellationToken cancellationToken = default)
        {
            CapturedToken = cancellationToken;
            TransitionStarted = true;
            return _completion.Task;
        }

        public override void Reset() { }
        public void CompleteTransition() => _completion.TrySetResult(true);
    }
}
