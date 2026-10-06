using System.Reflection;
using System.Threading;
using NUnit.Framework;
using QuackUp.Input;

namespace QuackUp.Input.Tests
{
    public sealed class InputButtonLifetimeTests
    {
        [Test]
        public void CancelPendingPulse_CancelsAndDisposesItsCancellationSource()
        {
            var button = new InputButton(null);
            var startPulse = typeof(InputButton).GetMethod("StartButtonPressTask", BindingFlags.Instance | BindingFlags.NonPublic);
            var pulseSourceField = typeof(InputButton).GetField("_pulseCts", BindingFlags.Instance | BindingFlags.NonPublic);

            startPulse.Invoke(button, null);
            var source = (CancellationTokenSource)pulseSourceField.GetValue(button);
            var cancellationObserved = false;
            source.Token.Register(() => cancellationObserved = true);

            button.CancelPendingPulse();

            Assert.That(cancellationObserved, Is.True);
            Assert.That(pulseSourceField.GetValue(button), Is.Null);
            Assert.Throws<System.ObjectDisposedException>(() =>
            {
                var token = source.Token;
            });
        }
    }
}
