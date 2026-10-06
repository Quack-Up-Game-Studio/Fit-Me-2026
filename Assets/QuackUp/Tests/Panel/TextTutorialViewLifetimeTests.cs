using System;
using System.Reflection;
using System.Threading;
using FitMe.Panel.Tutorial;
using NUnit.Framework;
using R3;
using UnityEngine;

namespace FitMe.Panel.Tests
{
    public sealed class TextTutorialViewLifetimeTests
    {
        [Test]
        public void Dispose_CancelsAndDisposesOwnedTokenSources()
        {
            var gameObject = new GameObject(nameof(TextTutorialViewLifetimeTests));
            var view = gameObject.AddComponent<TextTutorialView>();
            var displaySource = new CancellationTokenSource();
            SetField(view, "_bindings", new DisposableBag());
            SetField(view, "_displayDataTokenSource", displaySource);
            var transitionSource = (CancellationTokenSource)GetField(view, "_cancellationTokenSource");
            var displayCancellationObserved = false;
            using var registration = displaySource.Token.Register(() => displayCancellationObserved = true);

            try
            {
                view.Dispose();

                Assert.That(displaySource.IsCancellationRequested, Is.True);
                Assert.That(displayCancellationObserved, Is.True);
                Assert.That(transitionSource.IsCancellationRequested, Is.True);
                Assert.Throws<ObjectDisposedException>(() =>
                {
                    var waitHandle = displaySource.Token.WaitHandle;
                });
                Assert.Throws<ObjectDisposedException>(() =>
                {
                    var waitHandle = transitionSource.Token.WaitHandle;
                });
                Assert.DoesNotThrow(view.Dispose, "Dispose must be safe when OnDestroy calls it again.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ClickingNextToSkipWriter_CancelsAndDisposesDisplaySource()
        {
            var gameObject = new GameObject(nameof(TextTutorialViewLifetimeTests));
            var view = gameObject.AddComponent<TextTutorialView>();
            var displaySource = new CancellationTokenSource();
            SetField(view, "_bindings", new DisposableBag());
            SetField(view, "_displayDataTokenSource", displaySource);
            var callbackInvoked = false;
            using var registration = displaySource.Token.Register(() => callbackInvoked = true);

            try
            {
                typeof(TextTutorialView)
                    .GetMethod("OnNextButtonClicked", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(view, null);

                Assert.That(callbackInvoked, Is.True);
                Assert.Throws<ObjectDisposedException>(() =>
                {
                    var waitHandle = displaySource.Token.WaitHandle;
                });
                Assert.That(GetField(view, "_displayDataTokenSource"), Is.Null);
            }
            finally
            {
                displaySource.Dispose();
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private static object GetField(object target, string name)
        {
            return target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }
    }
}
