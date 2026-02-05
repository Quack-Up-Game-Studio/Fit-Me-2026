using System;
using QuackUp.Utils;
using R3;
using UnityEngine;
using VContainer;

namespace FitMe.Panel
{
    public class GameOverPanelViewModel : PanelViewModel
    {
        public ReactiveCommand AdsContinueCommand { get; } = new();
        public ReactiveCommand SkipCommand { get; } = new();
        public readonly ReactiveProperty<int> RemainingContinueCount = new ReactiveProperty<int>();
        public readonly ReactiveProperty<float> CountdownTimePercent = new ReactiveProperty<float>();
        
        private readonly int _maxContinueCount;
        private readonly bool _enableAds = true;
        private float _maxCountdownTime;
        private float _countdownTime;
        
        private readonly AdsService _adsService;
        private IDisposable _bindings;
        private IDisposable _countdownTimer;
        
        public const string MaxContinueCountId = "MaxContinueCountId";
        public const string CountdownTimeId = "CountdownTimeId";
        
        [Inject]
        public GameOverPanelViewModel(
            PanelManager panelManager,
            AdsService adsService,
            [Key(MaxContinueCountId)] int maxContinueCount,
            [Key(CountdownTimeId)] float maxCountdownTime) : base(panelManager)
        {
            _adsService = adsService;
            
            _maxContinueCount = maxContinueCount;
            RemainingContinueCount.Value = _maxContinueCount;
            
            _maxCountdownTime = maxCountdownTime;
            _countdownTime = _maxCountdownTime;
            CountdownTimePercent.Value = _maxCountdownTime;
            
            Bind();
        }
        
        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            AdsContinueCommand
                .Subscribe(_ => OnAdsButtonClicked())
                .AddTo(ref disposableBuilder);

            SkipCommand
                .Subscribe(_ => OnSkip())
                .AddTo(ref disposableBuilder);

            _adsService.OnUserEarnedReward
                .Subscribe(_ => OnAdSuccess())
                .AddTo(ref disposableBuilder);
            
            _bindings = disposableBuilder.Build();
        }
        
        public override void Dispose()
        {
            base.Dispose();
            _bindings?.Dispose();
        }
        
        private void OnAdsButtonClicked()
        {
            if (RemainingContinueCount.CurrentValue >= 0  && _enableAds)
            {
                bool isAdShown = _adsService.TryShowRewardedAd();
                if (!isAdShown)
                {
                    Debug.Log("Ads not ready");
                }
            }
        }
        
        private void OnAdSuccess()
        {
            RemainingContinueCount.Value --;
        }

        protected override void OnVisible()
        {
            base.OnVisible();
            UpdateCountdown();
        }

        private void UpdateCountdown()
        {
            _countdownTimer = 
            Observable.EveryUpdate()
                .Subscribe(_ =>
                {
                    _countdownTime -= Time.deltaTime;
                    CountdownTimePercent.Value = Mathf.Clamp01(_countdownTime / _maxCountdownTime);
                    if (_countdownTime <= 0f)
                    {
                        CountdownTimePercent.Value = 0;
                        _countdownTimer.Dispose();
                    }
                });
        }
        
        private void OnSkip()
        { 
            
        }
    }
}
