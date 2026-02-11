using System;
using FitMe.Shared;
using MessagePipe;
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
        public Subject<Unit> OnAdsCompleted { get; } = new();
        public ReadOnlyReactiveProperty<int> RemainingContinueCount => _remainingContinueCount;
        public ReadOnlyReactiveProperty<float> CountdownTimePercent => _countdownTimePercent;
        
        private readonly ReactiveProperty<int> _remainingContinueCount = new();
        private readonly ReactiveProperty<float> _countdownTimePercent = new();
        private readonly AdsService _adsService;
        private readonly int _maxContinueCount;
        private readonly bool _enableAds = true;
        private readonly IPublisher<ClearGridEvent> _clearGridEventPublisher;
        
        private float _maxCountdownTime;
        private float _countdownTime;
        private IDisposable _bindings;
        private IDisposable _countdownTimer;
        
        public const string MaxContinueCountId = "MaxContinueCountId";
        public const string CountdownTimeId = "CountdownTimeId";
        
        [Inject]
        public GameOverPanelViewModel(
            PanelManager panelManager,
            AdsService adsService,
            [Key(MaxContinueCountId)] int maxContinueCount,
            [Key(CountdownTimeId)] float maxCountdownTime,
            IPublisher<ClearGridEvent> clearGridEventPublisher) : base(panelManager)
        {
            _adsService = adsService;
            
            _maxContinueCount = maxContinueCount;
            _remainingContinueCount.Value = _maxContinueCount;
            
            _maxCountdownTime = maxCountdownTime;
            _countdownTime = _maxCountdownTime;
            _countdownTimePercent.Value = _maxCountdownTime;
            
            _clearGridEventPublisher = clearGridEventPublisher;
            
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
            if (_remainingContinueCount.CurrentValue >= 0  && _enableAds)
            {
#if UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR
                bool isAdShown = _adsService.TryShowRewardedAd();
                if (!isAdShown)
                {
                    Debug.Log("Ads not ready");
                }
#else
                OnAdSuccess(); // Simulate ad success in non-mobile platforms
#endif
            }
        }
        
        private void OnAdSuccess()
        {
            _remainingContinueCount.Value--;
            _clearGridEventPublisher.Publish(new ClearGridEvent());
            OnAdsCompleted.OnNext(Unit.Default);
            _countdownTimer.Dispose();
        }

        protected override void OnVisible()
        {
            base.OnVisible();
            UpdateCountdown();
        }

        private void UpdateCountdown()
        {
            _countdownTime = _maxCountdownTime;
            _countdownTimer = 
            Observable.EveryUpdate()
                .Subscribe(_ =>
                {
                    _countdownTime -= Time.deltaTime;
                    _countdownTimePercent.Value = Mathf.Clamp01(_countdownTime / _maxCountdownTime);
                    if (_countdownTime <= 0f)
                    {
                        _countdownTimePercent.Value = 0;
                        _countdownTimer.Dispose();
                    }
                });
        }
        
        private void OnSkip()
        { 
            _countdownTimer.Dispose();
        }
    }
}
