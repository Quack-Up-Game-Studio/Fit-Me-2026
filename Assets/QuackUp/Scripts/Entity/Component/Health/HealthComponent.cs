using R3;
using UnityEngine;

namespace FitMe.Entity
{
    public class HealthComponent : Component
    {
        public readonly ReactiveProperty<float> CurrentHealth = new();
        public HealthComponentPreset HealthPreset => (HealthComponentPreset)Preset;
        
        public HealthComponent(HealthComponentPreset preset) : base(preset)
        {
            SetHealth(preset.InitialHealth);
        }

        public void ChangeHealth(float delta)
        {
            var newHealth = CurrentHealth.Value + delta;
            SetHealth(newHealth);
        }
        
        public void SetHealth(float health)
        {
            CurrentHealth.Value = health;
            CurrentHealth.Value = Mathf.Clamp(CurrentHealth.Value, HealthPreset.HealthRange.x, HealthPreset.HealthRange.y);
        }
    }
}