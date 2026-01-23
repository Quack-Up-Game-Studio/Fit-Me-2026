using R3;
using VContainer;

namespace FitMe.Entity
{
    public class PlayerHealthViewModel
    {
        public ReadOnlyReactiveProperty<float> CurrentHealth => _healthComponent.CurrentHealth.ToReadOnlyReactiveProperty();
        public float MaxHealth => _healthComponent.HealthPreset.HealthRange.y;
            
        private readonly HealthComponent _healthComponent;
        
        [Inject]
        public PlayerHealthViewModel(HealthComponent healthComponent)
        {
            _healthComponent = healthComponent;
        }
    }
}