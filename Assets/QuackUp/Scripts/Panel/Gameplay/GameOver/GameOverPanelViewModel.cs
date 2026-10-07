using System;
using FitMe.Grid;
using QuackUp.GoogleAdMob;
using FitMe.GameData;
using FitMe.Shared;
using QuackUp.IAP;
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
        
        public Observable<Unit> OnReturnToGameplay => _onReturnToGameplay;
        public ReadOnlyReactiveProperty<int> RemainingContinueCount => _remainingContinueCount;
        public ReadOnlyReactiveProperty<float> CountdownTimePercent => _countdownTimePercent;
        public ReadOnlyReactiveProperty<bool> IsAdsDisabledFromVip => _isAdsDisabledFromVip;
        
        private readonly Subject<Unit> _onReturnToGameplay = new();
        private readonly ReactiveProperty<int> _remainingContinueCount = new();
        private readonly ReactiveProperty<float> _countdownTimePercent = new();
        private readonly ReactiveProperty<bool> _isAdsDisabledFromVip = new();
        private readonly AdsService _adsService;
        private readonly InAppPurchaseManager _inAppPurchaseManager;
        private readonly int _maxContinueCount;
        private readonly bool _enableAds = true;
        private readonly IMessageHub _gridManagerMessageHub;
        private readonly IMessageHub _blockManagerMessageHub;
        
        private float _maxCountdownTime;
        private float _countdownTime;
        private IDisposable _bindings;
        private IDisposable _countdownTimer;
        private IDisposable _adSubscription;
        
        public const string MaxContinueCountId = "MaxContinueCountId";
        public const string CountdownTimeId = "CountdownTimeId";
        
        [Inject]
        public GameOverPanelViewModel(
            PanelManager panelManager,
            AdsService adsService,
            InAppPurchaseManager inAppPurchaseManager,
            [Key(MaxContinueCountId)] int maxContinueCount,
            [Key(CountdownTimeId)] float maxCountdownTime,
            [Key(GridManagerMessageHub.GridManagerMessageHubKey)] IMessageHub gridManagerMessageHub,
            [Key(BlockManagerMessageHub.MessageHubKey)] IMessageHub blockManagerMessageHub) : base(panelManager)
        {
            _adsService = adsService;
            _inAppPurchaseManager = inAppPurchaseManager;
            
            _maxContinueCount = maxContinueCount;
            _remainingContinueCount.Value = _maxContinueCount;
            
            _maxCountdownTime = maxCountdownTime;
            _countdownTime = _maxCountdownTime;
            _countdownTimePercent.Value = _maxCountdownTime;
            
            _gridManagerMessageHub = gridManagerMessageHub;
            _blockManagerMessageHub = blockManagerMessageHub;
            
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
            
            _isAdsDisabledFromVip.Value = _inAppPurchaseManager.HasActiveSubscription();

            _inAppPurchaseManager.OnPurchaseSuccess
                .Subscribe(_ => _isAdsDisabledFromVip.Value = _inAppPurchaseManager.HasActiveSubscription())
                .AddTo(ref disposableBuilder);

            _bindings = disposableBuilder.Build();
        }
        
        public override void Dispose()
        {
            base.Dispose();
            _bindings?.Dispose();
            _adSubscription?.Dispose();
            _countdownTimer?.Dispose();
        }
        
        private void OnAdsButtonClicked()
        {
            if (_remainingContinueCount.CurrentValue < 0 || !_enableAds) return;
            if (_inAppPurchaseManager.HasActiveSubscription())
            {
                OnAdSuccess();
                return;
            }

            if (!_adsService.TryGetAdsInstance<RewardedAdInstance>(out var rewardedAd)) return;
            if (!rewardedAd.Enabled) 
            {
                OnAdSuccess();
                return;
            }
            _adSubscription?.Dispose();
            var subscriptionBuilder = Disposable.CreateBuilder();
            rewardedAd.OnUserEarnedReward
                .Subscribe(_ => OnAdSuccess())
                .AddTo(ref subscriptionBuilder);
            rewardedAd.OnAdClosed
                .Subscribe(_ => DisposeAdSubscription())
                .AddTo(ref subscriptionBuilder);
            rewardedAd.OnAdFailed
                .Subscribe(_ => DisposeAdSubscription())
                .AddTo(ref subscriptionBuilder);
            _adSubscription = subscriptionBuilder.Build();
            rewardedAd.AdContext = GAAdContext.Revive;
            if (!rewardedAd.TryShow()) DisposeAdSubscription();
        }
        
        private void OnAdSuccess()
        {
            _remainingContinueCount.Value--;
            DisposeAdSubscription();
            ReturnToGameplay();
        }

        private void DisposeAdSubscription()
        {
            _adSubscription?.Dispose();
            _adSubscription = null;
        }
        
        protected override void OnVisible()
        {
            base.OnVisible();
            UpdateCountdown();
        }

        private void ReturnToGameplay()
        {
            _onReturnToGameplay.OnNext(Unit.Default);
            _blockManagerMessageHub.Publish(new ContinueEvent());
            _gridManagerMessageHub.Publish(new ClearGridEvent(shouldClearGrid: true, shouldDestroyObstacles: false));
            _countdownTimer.Dispose();
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
