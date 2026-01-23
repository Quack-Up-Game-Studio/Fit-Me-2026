using System;
using R3;
using TMPro;
using UnityEngine;
using VContainer;

namespace FitMe.Entity
{
    public class PlayerHealthUIView : MonoBehaviour, IDisposable
    {
        [SerializeField] private TMP_Text healthText;
        
        private PlayerHealthViewModel _viewModel;
        private IDisposable _bindings;

        [Inject]
        public void Construct(PlayerHealthViewModel viewModel)
        {
            _viewModel = viewModel;
            Bind();
        }

        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            _viewModel.CurrentHealth
                .Prepend(_viewModel.CurrentHealth.CurrentValue)
                .Pairwise()
                .Subscribe(x => OnHealthChanged(x.Previous, x.Current))
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }
        
        public void Dispose()
        {
            _bindings?.Dispose();
        }

        private void OnDestroy()
        {
            Dispose();
        }
        
        private void OnHealthChanged(float oldValue, float newValue)
        {
            var delta = newValue - oldValue;
            healthText.text = $"Health: {newValue}/{_viewModel.MaxHealth} ({(delta >= 0 ? "+" : "")}{delta})";
        }
    }
}