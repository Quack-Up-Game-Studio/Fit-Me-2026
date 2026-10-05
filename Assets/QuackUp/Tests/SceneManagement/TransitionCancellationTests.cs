using System;
using System.Collections;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using PrimeTween;
using UnityEngine;
using UnityEngine.TestTools;

namespace QuackUp.SceneManagement.Tests
{
    public sealed class TransitionCancellationTests
    {
        [UnityTest]
        public IEnumerator Fade_CompletedTokenCannotCancelNextSequence()
        {
            var go = new GameObject("Fade token fixture");
            var group = go.AddComponent<CanvasGroup>();
            var fade = go.AddComponent<FadeToBlackTransitionPanel>();
            Set(fade, "canvasGroup", group);
            Set(fade, "tweenSettings", new TweenSettings<float>(0f, 1f, 30f));
            using var old = new CancellationTokenSource();
            using var next = new CancellationTokenSource();
            Exception error = null;
            var completed = false;
            try
            {
                Observe(fade.TransitionIn(old.Token), () => completed = true, e => error = e).Forget();
                SequenceOf(fade, "_transitionSequence").Complete();
                for (var i = 0; i < 10 && !completed; i++) yield return null;
                Assert.That(completed, Is.True, error?.ToString());
                Observe(fade.TransitionOut(next.Token), () => { }, e => error = e).Forget();
                Assert.That(SequenceOf(fade, "_transitionSequence").isAlive, Is.True);
                old.Cancel();
                Assert.That(SequenceOf(fade, "_transitionSequence").isAlive, Is.True,
                    "Old completed token must not complete the new Fade sequence.");
                Assert.That(group.blocksRaycasts, Is.True);
                next.Cancel();
                yield return null;
                Assert.That(group.blocksRaycasts, Is.False);
                Assert.That(group.alpha, Is.EqualTo(0f), "Fade cancellation retains Complete semantics.");
            }
            finally { SequenceOf(fade, "_transitionSequence").Stop(); UnityEngine.Object.DestroyImmediate(go); }
        }
        [UnityTest]
        public IEnumerator Cascade_CompletedTokenCannotStopNextSequence()
        {
            var go = new GameObject("Cascade token fixture", typeof(RectTransform));
            go.SetActive(false); // Configure serialized references before Awake.
            var group = go.AddComponent<CanvasGroup>();
            var image = go.AddComponent<UnityEngine.UI.Image>();
            var cascade = go.AddComponent<BlockCascadeScreen>();
            Set(cascade, "canvasGroup", group); Set(cascade, "background", image);
            Set(cascade, "backgroundFadeInSettings", new TweenSettings<float>(0f, 1f, 30f));
            Set(cascade, "backgroundFadeOutSettings", new TweenSettings<float>(1f, 0f, 30f));
            var field = typeof(BlockCascadeScreen).GetField("blockTweens", BindingFlags.Instance | BindingFlags.NonPublic);
            var list = (IList)Activator.CreateInstance(field.FieldType);
            var blockType = field.FieldType.GetGenericArguments()[0];
            var block = Activator.CreateInstance(blockType);
            blockType.GetField("block").SetValue(block, go.GetComponent<RectTransform>());
            blockType.GetField("positionTweenSettings").SetValue(block, new TweenSettings<Vector2>(Vector2.zero, Vector2.one, 30f));
            list.Add(block); field.SetValue(cascade, list);
            using var old = new CancellationTokenSource();
            using var next = new CancellationTokenSource();
            var completed = false;
            Exception error = null;
            try
            {
                Observe(cascade.TransitionIn(old.Token), () => completed = true, e => error = e).Forget();
                SequenceOf(cascade, "_blockSequence").Complete();
                for (var i = 0; i < 10 && !completed; i++) yield return null;
                Assert.That(completed, Is.True, error?.ToString());
                Observe(cascade.TransitionOut(next.Token), () => { }, e => error = e).Forget();
                old.Cancel();
                Assert.That(SequenceOf(cascade, "_blockSequence").isAlive, Is.True,
                    "Old completed token must not stop the next Cascade sequence.");
                Assert.That(group.blocksRaycasts, Is.True);
                var alpha = image.color.a;
                next.Cancel();
                yield return null;
                Assert.That(image.color.a, Is.EqualTo(alpha), "Cascade cancellation retains Stop (not Complete) semantics.");
                Assert.That(group.blocksRaycasts, Is.False);
                Assert.That(group.interactable, Is.False);
            }
            finally { SequenceOf(cascade, "_blockSequence").Stop(); UnityEngine.Object.DestroyImmediate(go); }
        }

        private static Sequence SequenceOf(object target, string name) => (Sequence)target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static async UniTask Observe(UniTask task, Action completed, Action<Exception> failed)
        { try { await task; completed(); } catch (Exception error) { failed(error); } }
    }
}
