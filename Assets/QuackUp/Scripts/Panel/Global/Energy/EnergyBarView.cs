using System;
using QuackUp.Utils;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace FitMe.Panel
{
    public class EnergyBarView : MonoBehaviour, IDisposable
    {
        [SerializeField] private Slider energyBar;
        [SerializeField] private TMP_Text energyText;
        [SerializeField] private TMP_Text untilNextRechargeText;
        [SerializeField] private Button watchAdButton;
        
        private EnergyBarViewModel _viewModel;
        private IDisposable _bindings;
        
        [Inject]
        public void Construct(EnergyBarViewModel viewModel)
        {
            _viewModel = viewModel;
            Bind();
        }

        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            
            watchAdButton.OnClickAsObservable()
                .Subscribe(_ => OnWatchAdButtonClicked())
                .AddTo(ref disposableBuilder);
            
            _viewModel.CurrentEnergy
                .Subscribe(OnEnergyChanged)
                .AddTo(ref disposableBuilder);
            
            _viewModel.InfiniteEnergy
                .Subscribe(OnInfiniteEnergyChanged)
                .AddTo(ref disposableBuilder);
            
            _viewModel.AllowWatchAd
                .Subscribe(OnAllowWatchAdChanged)
                .AddTo(ref disposableBuilder);
            
            _viewModel.TimeUntilNextRecharge
                .Subscribe(OnTimeUntilNextRechargeChanged)
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
        
        private void OnWatchAdButtonClicked()
        {
            _viewModel.WatchAdCommand.Execute(Unit.Default);
        }

        private void OnInfiniteEnergyChanged(bool infinite)
        {
            if (infinite)
            {
                untilNextRechargeText.text = string.Empty;
                energyText.text = "Infinite";
                energyBar.value = 1f;
                OnAllowWatchAdChanged(false);
            }
            else
            {
                OnEnergyChanged(_viewModel.CurrentEnergy.CurrentValue);
                OnAllowWatchAdChanged(_viewModel.AllowWatchAd.CurrentValue);
            }
        }

        private void OnEnergyChanged(int currentEnergy)
        {
            var maxEnergy = _viewModel.Config.MaxEnergy;
            watchAdButton.interactable = currentEnergy < maxEnergy;
            if (currentEnergy >= maxEnergy)
            {
                untilNextRechargeText.text = "Full";
            }
            energyBar.value = (float)currentEnergy / maxEnergy;
            energyText.text = $"{currentEnergy} / {maxEnergy}";
        }
        
        private void OnAllowWatchAdChanged(bool allow)
        {
            watchAdButton.gameObject.SetActive(!_viewModel.InfiniteEnergy.CurrentValue && allow);
        }
        
        private void OnTimeUntilNextRechargeChanged(TimeSpan time)
        {
            if (_viewModel.InfiniteEnergy.CurrentValue)
            {
                untilNextRechargeText.text = string.Empty;
                return;
            }
            if (_viewModel.CurrentEnergy.CurrentValue >= _viewModel.Config.MaxEnergy)
            {
                untilNextRechargeText.text = "Full";
                return;
            }
            //round up to the nearest second for display purposes
            var roundedTime = TimeSpan.FromSeconds(Mathf.Ceil((float)time.TotalSeconds));
            untilNextRechargeText.text = $"Next In {roundedTime:mm\\:ss}";
        }
    }
}