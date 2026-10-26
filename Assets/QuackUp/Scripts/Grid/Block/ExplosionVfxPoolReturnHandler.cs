using System;
using UnityEngine;

namespace FitMe.Grid
{
    [DisallowMultipleComponent]
    internal sealed class ExplosionVfxPoolReturnHandler : MonoBehaviour
    {
        private Action<ParticleSystem> _onStopped;
        private Action<ParticleSystem> _onReturned;
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
            _onReturned = null;
        }

        public void SetOnReturned(Action<ParticleSystem> onReturned)
        {
            _onReturned = onReturned;
        }

        public void NotifyReturned(ParticleSystem instance)
        {
            var onReturned = _onReturned;
            _onReturned = null;
            onReturned?.Invoke(instance);
        }

        public void ClearOnReturned(Action<ParticleSystem> onReturned)
        {
            if (_onReturned == onReturned) _onReturned = null;
        }

        private void OnParticleSystemStopped()
        {
            if (_returned || !_particleSystem) return;
            _returned = true;
            _onStopped?.Invoke(_particleSystem);
        }
    }
}
