using System;
using System.Linq;
using NUnit.Framework;

namespace FitMe.Grid.Tests
{
    public class PerformanceResumeFrameWindowTests
    {
        [Test]
        public void Begin_AnnotatesTheConfiguredPostResumeFramesWithMonotonicElapsedTime()
        {
            var coreAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .Single(assembly => assembly.GetName().Name == "QuackUp.Core");
            var windowType = coreAssembly.GetType("QuackUp.Core.PerformanceResumeFrameWindow");
            Assert.That(windowType, Is.Not.Null, "The diagnostics resume window should be available to the recorder.");
            if (windowType == null) return;

            var window = Activator.CreateInstance(windowType, 3);
            var begin = windowType.GetMethod("Begin");
            var tryCapture = windowType.GetMethod("TryCapture");
            Assert.That(begin, Is.Not.Null);
            Assert.That(tryCapture, Is.Not.Null);
            if (begin == null || tryCapture == null) return;

            begin.Invoke(window, new object[] { 8.0 });

            var first = new object[] { 8.045, 0, 0d };
            Assert.That(tryCapture.Invoke(window, first), Is.True);
            Assert.That(first[1], Is.EqualTo(1));
            Assert.That((double)first[2], Is.EqualTo(45.0).Within(0.001));

            var second = new object[] { 8.062, 0, 0d };
            Assert.That(tryCapture.Invoke(window, second), Is.True);
            Assert.That(second[1], Is.EqualTo(2));

            var third = new object[] { 8.079, 0, 0d };
            Assert.That(tryCapture.Invoke(window, third), Is.True);
            Assert.That(third[1], Is.EqualTo(3));

            var afterWindow = new object[] { 8.096, 0, 0d };
            Assert.That(tryCapture.Invoke(window, afterWindow), Is.False);
        }
    }
}
