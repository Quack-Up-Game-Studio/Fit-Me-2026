using System;
using UnityEngine;

namespace FitMe.Grid
{
    [DisallowMultipleComponent]
    internal sealed class ExplosionVfxPoolReturnHandler : MonoBehaviour
    {
        private Action<ParticleSystem> _onStopped;
        private ParticleSystem _particleSystem;
        private bool _returned;

        public void Initialize(ParticleSystem particleSystem, Action<ParticleSystem> onStopped)
        {
            _particleSystem = particleSystem;
            _onStopped = onStopped;
        }

        public void BeginPlayback()
        {
            _returned = false;
        }

        private void OnParticleSystemStopped()
        {
            if (_returned || !_particleSystem) return;
            _returned = true;
            _onStopped?.Invoke(_particleSystem);
        }
    }
}
