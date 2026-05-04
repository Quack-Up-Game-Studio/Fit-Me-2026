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
        [SerializeField] private RectMask2D energyMask;
        [SerializeField] private Image infiniteImage;
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
            infiniteImage.gameObject.SetActive(infinite);
            energyMask.gameObject.SetActive(!infinite);
            if (infinite)
            {
                untilNextRechargeText.text = "Infinite";
                energyText.text = "Infinite";
                energyBar.value = 1f;
                energyMask.padding = Vector4.zero;
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
            watchAdButton.interactable = _viewModel.AllowWatchAd.CurrentValue && currentEnergy < maxEnergy;
            if (currentEnergy >= maxEnergy)
            {
                untilNextRechargeText.text = "Full";
            }
            energyBar.value = (float)currentEnergy / maxEnergy;
            energyMask.padding = new Vector4(0, 0, energyMask.rectTransform.rect.width * (1 - energyBar.value), 0);
            energyText.text = $"{currentEnergy} / {maxEnergy}";
        }
        
        private void OnAllowWatchAdChanged(bool allow)
        {
            var maxEnergy = _viewModel.Config.MaxEnergy;
            var currentEnergy = _viewModel.CurrentEnergy.CurrentValue;
            watchAdButton.interactable = !_viewModel.InfiniteEnergy.CurrentValue && allow && currentEnergy < maxEnergy;
        }
        
        private void OnTimeUntilNextRechargeChanged(TimeSpan time)
        {
            if (_viewModel.InfiniteEnergy.CurrentValue)
            {
                untilNextRechargeText.text = "Infinite";
                return;
            }
            if (_viewModel.CurrentEnergy.CurrentValue >= _viewModel.Config.MaxEnergy)
            {
                untilNextRechargeText.text = "Full";
                return;
            }
            //round up to the nearest second for display purposes
            var roundedTime = TimeSpan.FromSeconds(Mathf.Ceil((float)time.TotalSeconds));
            untilNextRechargeText.text = $"{roundedTime:mm\\:ss}";
        }
    }
}