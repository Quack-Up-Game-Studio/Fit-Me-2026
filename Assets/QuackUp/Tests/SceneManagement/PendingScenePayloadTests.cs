using FitMe.Grid;
using FitMe.Scene;
using FitMe.Shared;
using NUnit.Framework;
using UnityEngine;

namespace QuackUp.SceneManagement.Tests
{
    public sealed class PendingScenePayloadTests
    {
        [Test]
        public void TryTakeReturnsPayloadExactlyOnce()
        {
            var payload = new PendingScenePayload();
            payload.Set("gameplay");

            Assert.That(payload.TryTake<string>(out var first), Is.True);
            Assert.That(first, Is.EqualTo("gameplay"));
            Assert.That(payload.TryTake<string>(out _), Is.False);
        }

        [Test]
        public void TryTakeRejectsWrongTypeWithoutConsumingPayload()
        {
            var payload = new PendingScenePayload();
            payload.Set("gameplay");

            Assert.That(payload.TryTake<int>(out _), Is.False);
            Assert.That(payload.TryTake<string>(out var value), Is.True);
            Assert.That(value, Is.EqualTo("gameplay"));
        }

        [Test]
        public void ActivePayloadSurvivesOneShotScenePayloadConsumption()
        {
            var payload = new PendingScenePayload();
            var request = new LevelSessionRequest(GameMode.LevelShape, null);
            payload.SetActive(request);
            payload.Set("Gameplay entry");

            Assert.That(payload.TryTake<string>(out var entry), Is.True);
            Assert.That(entry, Is.EqualTo("Gameplay entry"));
            Assert.That(payload.TryTake<string>(out _), Is.False);
            Assert.That(payload.TryGetActive<LevelSessionRequest>(out var active), Is.True);
            Assert.That(active.GameMode, Is.EqualTo(GameMode.LevelShape));
        }

        [Test]
        public void RetrySessionPayload_PreservesGridPreset()
        {
            var payload = new PendingScenePayload();
            var preset = CreateGridPreset();
            var request = new LevelSessionRequest(GameMode.LevelShape, preset);
            payload.Set(request);

            Assert.That(payload.TryTake<LevelSessionRequest>(out var firstEntry), Is.True);
            payload.SetActive(firstEntry);
            Assert.That(payload.TryGetActive<LevelSessionRequest>(out var activeRequest), Is.True);
            Assert.That(activeRequest.GameMode, Is.EqualTo(firstEntry.GameMode));
            Assert.That(activeRequest.GridPreset, Is.SameAs(preset));

            Assert.That(payload.TryRequeueActive(), Is.True);
            Assert.That(payload.TryTake<LevelSessionRequest>(out var retryRequest), Is.True);
            Assert.That(retryRequest.GameMode, Is.EqualTo(GameMode.LevelShape));
            Assert.That(retryRequest.GridPreset, Is.SameAs(preset));
        }

        private static GridPreset CreateGridPreset()
        {
            return ScriptableObject.CreateInstance<GridPreset>();
        }

        [Test]
        public void RetrySessionPayload_ClassicRemainsClassic()
        {
            var payload = new PendingScenePayload();
            var request = new LevelSessionRequest(GameMode.Classic, null);
            payload.Set(request);
            Assert.That(payload.TryTake<LevelSessionRequest>(out var entry), Is.True);
            payload.SetActive(entry);

            Assert.That(payload.TryRequeueActive(), Is.True);
            Assert.That(payload.TryTake<LevelSessionRequest>(out var retryRequest), Is.True);

            Assert.That(retryRequest.GameMode, Is.EqualTo(GameMode.Classic));
        }
    }
}
