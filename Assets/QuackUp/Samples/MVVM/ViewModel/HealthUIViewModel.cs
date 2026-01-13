using System;
using R3;
using VContainer;

namespace QuackUp.Samples
{
    public class HealthUIViewModel : IDisposable
    {
        public ReadOnlyReactiveProperty<float> Health { get; }
        public ReactiveCommand<float> SetHealthCommand { get; } = new();
        public ReactiveCommand<float> ChangeHealthCommand { get; } = new();
        
        private readonly HealthUIModel _model;
        private IDisposable _bindings;
        
        [Inject]
        public HealthUIViewModel(HealthUIModel model)
        {
            _model = model;
            Health = Observable
                .EveryValueChanged(_model, x => x.Health)
                .ToReadOnlyReactiveProperty();
            Bind();
        }

        #region Lifecycle
        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            SetHealthCommand
                .Subscribe(OnSetHealth)
                .AddTo(ref disposableBuilder);
            ChangeHealthCommand
                .Subscribe(OnChangeHealth)
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }
        
        public void Dispose()
        {
            _bindings.Dispose();
        }
        #endregion
        
        private void OnSetHealth(float newHealth)
        {
            _model.Health = newHealth;
        }
        
        private void OnChangeHealth(float delta)
        {
            var newHealth = _model.Health + delta;
            _model.Health = newHealth;
        }
    }
}