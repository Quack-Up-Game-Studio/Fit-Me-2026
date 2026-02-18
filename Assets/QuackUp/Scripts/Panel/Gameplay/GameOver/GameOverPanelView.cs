using System;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace FitMe.Panel
{
    public class GameOverPanelView : PanelView
    {
        [SerializeField] private Button adsButton;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button skipButton;
        [SerializeField] private string gameplayPanelId = "Gameplay";
        [SerializeField] private string resultPanelId = "Result";
        
        [SerializeField] private TMP_Text continueCountText;
        [SerializeField] private Image clockImage;
        
        private GameOverPanelViewModel ViewModel => (GameOverPanelViewModel)BaseViewModel;
        private IDisposable _bindings;

        [Inject]
        public override void Construct(IPanelViewModel viewModel)
        {
            base.Construct(viewModel);
            Bind();
        }
        
        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            
            adsButton.OnClickAsObservable()
                .Subscribe(_ => OnAdsContinue())
                .AddTo(ref disposableBuilder);
            
            retryButton.OnClickAsObservable()
                .Subscribe(_ => OnRetry())
                .AddTo(ref disposableBuilder);

            skipButton.OnClickAsObservable()    
                .Subscribe(_ => OnSkip())
                .AddTo(ref disposableBuilder);
            
            ViewModel.CurrentEnergy
                .Subscribe(OnEnergyChanged)
                .AddTo(ref disposableBuilder);
            
            ViewModel.RemainingContinueCount
                .Subscribe(count =>
                {
                    continueCountText.text = $"Remaining: {count}";
                })
                .AddTo(ref disposableBuilder);
            
            ViewModel.CountdownTimePercent
                .Subscribe(percent =>
                {
                    clockImage.fillAmount = percent;
                    if (percent <= 0)
                    {
                        OnSkip();
                    }
                })
                .AddTo(ref disposableBuilder);
            
            ViewModel.OnReturnToGameplay
                .Subscribe(_ => OnAdsCompleted())
                .AddTo(ref disposableBuilder);
            
            _bindings = disposableBuilder.Build();
        }
        
        public override void Dispose()
        {
            base.Dispose();
            _bindings?.Dispose();
        }
        
        private void OnAdsContinue()
        {
            ViewModel.AdsContinueCommand.Execute(Unit.Default);
        }

        private void OnRetry()
        {
            ViewModel.RetryCommand.Execute(Unit.Default);
        }

        private void OnEnergyChanged(int current)
        {
            var hasEnergy = current > 0;
            adsButton.gameObject.SetActive(!hasEnergy);
            retryButton.gameObject.SetActive(hasEnergy);
        }

        private void OnAdsCompleted()
        {
            if (!TryGetCrossfadeRule(gameplayPanelId, out var rule)) return;
            ViewModel.CrossfadeCommand.Execute(new CrossfadeCommandData(gameplayPanelId, rule.crossfadeSettings));
        }
        
        private void OnSkip()
        {
            ViewModel.SkipCommand.Execute(Unit.Default);
            if (!TryGetCrossfadeRule(resultPanelId, out var rule)) return;
            ViewModel.CrossfadeCommand.Execute(new CrossfadeCommandData(resultPanelId, rule.crossfadeSettings));
        }
    }
}
