using R3;
using UnityEngine;
using VContainer;

namespace QuackUp.Samples
{
    public class HealthUIModel
    {
        private float _health;
        public float Health
        {
            get => _health;
            set => _health = Mathf.Clamp(value, _config.HealthRange.x, _config.HealthRange.y);
        }

        private readonly HealthUIConfig _config;

        [Inject]
        public HealthUIModel(HealthUIConfig config)
        {
            _config = config;
        }
    }
}