using System;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace QuackUp.Samples
{
    public class HealthUIView : MonoBehaviour, IDisposable
    {
        #region Inspector
        [SerializeField] private Slider healthSlider;
        [SerializeField] private TMP_InputField healthInputField;
        [SerializeField] private Button applyButton;
        [SerializeField] private Button stepUpButton;
        [SerializeField] private Button stepDownButton;
        [SerializeField] private TMP_Text stepUpButtonText;
        [SerializeField] private TMP_Text stepDownButtonText;
        [SerializeField] private TMP_Text healthDeltaText;
        [SerializeField] private TMP_Text healthLabelText;
        #endregion
        
        private HealthUIViewModel _viewModel;
        private HealthUIConfig _config;
        private IDisposable _bindings;
        
        [Inject]
        public void Construct(
            HealthUIViewModel viewModel,
            HealthUIConfig config)
        {
            _viewModel = viewModel;
            _config = config;
            healthSlider.minValue = _config.HealthRange.x;
            healthSlider.maxValue = _config.HealthRange.y;
            
            Bind();
        }

        #region Lifecycle
        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            _viewModel.Health
                .Prepend(_viewModel.Health.CurrentValue)
                .Pairwise() // จับคู่ค่าก่อนหน้าและค่าปัจจุบัน
                .Subscribe(x => OnHealthChanged(x.Previous, x.Current))
                .AddTo(ref disposableBuilder);
            applyButton.OnClickAsObservable()
                .Subscribe(_ => OnApplyButtonClicked())
                .AddTo(ref disposableBuilder);
            stepUpButton.OnClickAsObservable()
                .Subscribe(_ => OnStepButtonClicked(+1))
                .AddTo(ref disposableBuilder);
            stepDownButton.OnClickAsObservable()
                .Subscribe(_ => OnStepButtonClicked(-1))
                .AddTo(ref disposableBuilder);
            Observable
                .EveryValueChanged(_config, x => x.HealthChangeStep)
                .Subscribe(OnStepUpChanged)
                .AddTo(ref disposableBuilder);
            Observable
                .EveryValueChanged(_config, x => x.HealthRange)
                .Subscribe(OnHealthRangeChanged)
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }

        private void OnDestroy()
        {
            Dispose();
        }

        public void Dispose()
        {
            _bindings?.Dispose();
        }
        #endregion
        
        #region Bindings
        private void OnHealthChanged(float oldValue, float newValue)
        {
            var delta = newValue - oldValue;
            healthSlider.value = newValue;
            healthLabelText.SetText($"{newValue}/{_config.HealthRange.y}");
            healthDeltaText.SetText(delta >= 0 ? $"Delta: +{delta:F2}" : $"Delta: {delta:F2}");
        }

        private void OnStepButtonClicked(int sign)
        {
            var step = sign * _config.HealthChangeStep;
            _viewModel.ChangeHealthCommand.Execute(step);
        }
        
        private void OnApplyButtonClicked()
        {
            if (!float.TryParse(healthInputField.text, out var newHealth)) return;
            healthInputField.text = string.Empty;
            _viewModel.SetHealthCommand.Execute(newHealth);
        }

        private void OnStepUpChanged(float value)
        {
            stepUpButtonText.SetText($"+{value}");
            stepDownButtonText.SetText($"-{value}");
        }

        private void OnHealthRangeChanged(Vector2 range)
        {
            healthSlider.minValue = range.x;
            healthSlider.maxValue = range.y;
            healthLabelText.SetText($"{healthSlider.value}/{range.y}");
        }
        #endregion
    }
}