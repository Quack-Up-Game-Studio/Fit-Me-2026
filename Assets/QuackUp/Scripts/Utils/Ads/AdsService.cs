using System;
using System.Threading;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using R3;
using UnityEngine;
using VContainer.Unity;

namespace QuackUp.Utils
{
    public class AdsService : IStartable, IDisposable
    {
        // Event เพื่อบอกภายนอกว่า "ได้รางวัลแล้วนะ"
        public Observable<Unit> OnUserEarnedReward => _onUserEarnedReward;
        private readonly Subject<Unit> _onUserEarnedReward = new();

        // Event บอกว่าโฆษณาปิดแล้ว (ไม่ว่าจะได้รางวัลหรือไม่)
        public Observable<Unit> OnAdClosed => _onAdClosed;
        private readonly Subject<Unit> _onAdClosed = new();

        private RewardedAd _rewardedAd;
        private IDisposable _adsRefreshTimer;
        private IDisposable _callbackSubscription;
        private CancellationTokenSource _timerCts;
        private bool _isRewardEarned;

        public void Start()
        {
            //MobileAds.RaiseAdEventsOnUnityMainThread = true;
            var requestConfiguration = new RequestConfiguration
            {
                TagForChildDirectedTreatment = TagForChildDirectedTreatment.True,
                MaxAdContentRating = MaxAdContentRating.G
            };
            
            MobileAds.SetRequestConfiguration(requestConfiguration);
            
#if UNITY_EDITOR || UNITY_ANDROID || UNITY_IOS
            MobileAds.Initialize(status => 
            {
                LoadRewardedAd();
            });
#endif
        }

        public void Dispose()
        {
            CancelAdsSessionTimer();
            DisposeAd();
            _callbackSubscription?.Dispose();
            _onUserEarnedReward.Dispose();
            _onAdClosed.Dispose();
        }

        private void LoadRewardedAd()
        {
            DisposeAd();

            AdRequest request = new AdRequest();
            string adUnitId;

#if UNITY_ANDROID
        adUnitId = "ca-app-pub-3940256099942544/5224354917";
#elif UNITY_IOS
        adUnitId = "ca-app-pub-3940256099942544/2934735716";
#else
            adUnitId = "ca-app-pub-3940256099942544/5224354917";
#endif

            RewardedAd.Load(adUnitId, request, (ad, error) =>
            {
                if (error != null || ad == null)
                {
                    Debug.LogError($"Failed to load ad: {error?.GetMessage()}");
                    return;
                }

                _rewardedAd = ad;
                RegisterAdEvents(_rewardedAd);
            
                CountdownAdSession();
            });
        }

        private void RegisterAdEvents(RewardedAd ad)
        {
            ad.OnAdFullScreenContentClosed += HandleAdClosed;
        }

        public bool TryShowRewardedAd(Action earnRewardCallback = null, Action adClosedCallback = null)
        {
#if UNITY_EDITOR || UNITY_ANDROID || UNITY_IOS
            if (_rewardedAd != null && _rewardedAd.CanShowAd())
            {
                _callbackSubscription?.Dispose();
                var disposableBuilder = Disposable.CreateBuilder();
                OnUserEarnedReward.Subscribe(_ => earnRewardCallback?.Invoke())
                    .AddTo(ref disposableBuilder);
                OnAdClosed.Subscribe(_ => adClosedCallback?.Invoke())
                    .AddTo(ref disposableBuilder);
                _callbackSubscription = disposableBuilder.Build();
                _isRewardEarned = false;
                _rewardedAd.Show(reward =>
                {
                    _isRewardEarned = true;
                });
                return true;
            }
        
            Debug.Log("Ad not ready yet.");
            LoadRewardedAd();
            return false;
            
#else // Simulate ad success in non-supported platforms
            earnRewardCallback?.Invoke();
            adClosedCallback?.Invoke();
            return true;
#endif
        }

        private void HandleAdClosed()
        {
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                if (_isRewardEarned)
                {
                    _onUserEarnedReward.OnNext(Unit.Default);
                }
                _onAdClosed.OnNext(Unit.Default);
                _callbackSubscription?.Dispose();
            });

            LoadRewardedAd();
        }
    
        private void CountdownAdSession()
        {
            CancelAdsSessionTimer();
            _timerCts = new CancellationTokenSource();
        
            _adsRefreshTimer = Observable.Timer(TimeSpan.FromHours(1), TimeProvider.System, _timerCts.Token)
                .Subscribe(_ => 
                {
                    LoadRewardedAd();
                });
        }

        private void CancelAdsSessionTimer()
        {
            _timerCts?.Cancel();
            _timerCts?.Dispose();
            _adsRefreshTimer?.Dispose();
        }

        private void DisposeAd()
        {
            if (_rewardedAd == null) return;
            _rewardedAd.OnAdFullScreenContentClosed -= HandleAdClosed;
            _rewardedAd.Destroy();
            _rewardedAd = null;
        }
    }
}