using NUnit.Framework;

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
    }
}
