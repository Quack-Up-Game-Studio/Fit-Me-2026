#if UNITY_EDITOR || FITME_PERFORMANCE_DIAGNOSTICS
using System;

namespace QuackUp.Core
{
    public sealed class PerformanceResumeFrameWindow
    {
        private readonly int _sampleCount;
        private double _resumeRealtimeSeconds;
        private int _nextSampleIndex;

        public PerformanceResumeFrameWindow(int sampleCount)
        {
            if (sampleCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(sampleCount), "The sample count must be positive.");

            _sampleCount = sampleCount;
        }

        public void Begin(double resumeRealtimeSeconds)
        {
            _resumeRealtimeSeconds = resumeRealtimeSeconds;
            _nextSampleIndex = 1;
        }

        public bool TryCapture(double realtimeSeconds, out int sampleIndex, out double elapsedMilliseconds)
        {
            if (_nextSampleIndex == 0)
            {
                sampleIndex = 0;
                elapsedMilliseconds = 0d;
                return false;
            }

            sampleIndex = _nextSampleIndex++;
            elapsedMilliseconds = (realtimeSeconds - _resumeRealtimeSeconds) * 1000d;
            if (_nextSampleIndex > _sampleCount)
                _nextSampleIndex = 0;

            return true;
        }
    }
}
#endif
