#if FITME_PERFORMANCE_DIAGNOSTICS
using System;
using UnityEngine;

namespace QuackUp.Core
{
    public sealed class PerformanceDiagnosticsPauseHandler : MonoBehaviour
    {
        public event Action<bool> PauseChanged;

        private void OnApplicationPause(bool isPaused)
        {
            PauseChanged?.Invoke(isPaused);
        }
    }
}
#endif
