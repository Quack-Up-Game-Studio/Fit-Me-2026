using System;
using System.Threading;
using GoogleMobileAds.Api;
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
        private CancellationTokenSource _timerCts;
        private bool _isRewardEarned;

        public void Start()
        {
            MobileAds.Initialize(status => 
            {
                MobileAds.RaiseAdEventsOnUnityMainThread = true;
                LoadRewardedAd();
            });
        }

        public void Dispose()
        {
            CancelAdsSessionTimer();
            DisposeAd();
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
#elif UNITY_IPHONE
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

        public bool TryShowRewardedAd()
        {
            if (_rewardedAd != null && _rewardedAd.CanShowAd())
            {
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
        }

        private void HandleAdClosed()
        {
            if (_isRewardEarned)
            {
                _onUserEarnedReward.OnNext(Unit.Default);
            }
            _onAdClosed.OnNext(Unit.Default);

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
            if (_rewardedAd != null)
            {
                _rewardedAd.OnAdFullScreenContentClosed -= HandleAdClosed;
                _rewardedAd.Destroy();
                _rewardedAd = null;
            }
        }
    }
}