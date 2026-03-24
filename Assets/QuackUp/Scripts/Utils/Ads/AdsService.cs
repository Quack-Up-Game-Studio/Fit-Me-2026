using System;
using System.Collections.Generic;
using System.Threading;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using R3;
using Sirenix.Utilities;
using UnityEngine;
using VContainer.Unity;

namespace QuackUp.Utils
{
    public abstract class AdsInstance : IDisposable
    {
        // Event บอกว่าโฆษณาปิดแล้ว (ไม่ว่าจะได้รางวัลหรือไม่)
        public Observable<Unit> OnAdClosed => _onAdClosed;
        protected readonly Subject<Unit> _onAdClosed = new();
        
        protected IDisposable _adsRefreshTimer;
        protected IDisposable _adsEventSubscription;
        protected CancellationTokenSource _timerCts = new();

        public abstract bool CanShowAd();

        public void Dispose()
        {
            DisposeAd();
            CancelAdsSessionTimer();
            _onAdClosed.Dispose();
        }
        
        public abstract void Load();
        public abstract bool TryShow();
        protected abstract void DisposeAd();
        protected abstract void RegisterAdEvents();

        protected virtual void CountdownAdSession()
        {
            CancelAdsSessionTimer();
            _timerCts = new CancellationTokenSource();
            _adsRefreshTimer = Observable.Timer(TimeSpan.FromHours(1), TimeProvider.System, _timerCts.Token)
                .Subscribe(_ => 
                {
                    Load();
                });
        }

        protected virtual void CancelAdsSessionTimer()
        {
            _adsRefreshTimer?.Dispose();
            _timerCts?.Cancel();
            _timerCts?.Dispose();
        }
    }
    
    public class RewardedAdInstance : AdsInstance
    {
        // Event เพื่อบอกภายนอกว่า "ได้รางวัลแล้วนะ"
        public Observable<Unit> OnUserEarnedReward => _onUserEarnedReward;
        private readonly Subject<Unit> _onUserEarnedReward = new();
        
        private RewardedAd _rewardedAd;
        private bool _isRewardEarned;

        public override bool CanShowAd() => _rewardedAd != null && _rewardedAd.CanShowAd();

        public override void Load()
        {
            DisposeAd();
            RewardedAd.Load(AdsService.UnitId, new AdRequest(), (ad, error) =>
            {
                if (error != null || ad == null)
                {
                    Debug.LogError($"Failed to load rewarded ad: {error?.GetMessage()}");
                    return;
                }
                _rewardedAd = ad;
                RegisterAdEvents();
                CountdownAdSession();
            });
        }

        public override bool TryShow()
        {
#if UNITY_EDITOR || UNITY_ANDROID || UNITY_IOS
            if (CanShowAd())
            {
                _isRewardEarned = false;
                _rewardedAd.Show(reward =>
                {
                    _isRewardEarned = true;
                });
                return true;
            }
        
            Debug.Log("Ad not ready yet.");
            Load();
            return false;
            
#else // Simulate ad success in non-supported platforms
            _onUserEarnedReward.OnNext(Unit.Default);
            _onAdClosed.OnNext(Unit.Default);
            return true;
#endif
        }

        protected override void DisposeAd()
        {
            if (_rewardedAd == null) return;
            _adsEventSubscription?.Dispose();
            _rewardedAd.Destroy();
            _rewardedAd = null;
        }

        protected override void RegisterAdEvents()
        {
            var builder = Disposable.CreateBuilder();
            Observable.FromEvent(
                h => _rewardedAd.OnAdFullScreenContentClosed += h,
                h => _rewardedAd.OnAdFullScreenContentClosed -= h)
                .Subscribe(_ => HandleAdClosed())
                .AddTo(ref builder);
            _adsEventSubscription = builder.Build();
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
            });
            Load();
        }
    }
    
    public class InterstitialAdInstance : AdsInstance
    {
        private InterstitialAd _interstitialAd;
        public override bool CanShowAd() => _interstitialAd != null && _interstitialAd.CanShowAd();
        public override void Load()
        {
            DisposeAd();
            InterstitialAd.Load(AdsService.UnitId, new AdRequest(), (ad, error) =>
            {
                if (error != null || ad == null)
                {
                    Debug.LogError($"Failed to load interstitial ad: {error?.GetMessage()}");
                    return;
                }

                _interstitialAd = ad;
                RegisterAdEvents();
                CountdownAdSession();
            });
        }
        
        public override bool TryShow()
        {
#if UNITY_EDITOR || UNITY_ANDROID || UNITY_IOS
            if (CanShowAd())
            {
                _interstitialAd.Show();
                return true;
            }
            Debug.Log("Ad not ready yet.");
            Load();
            return false;
            
#else // Simulate ad success in non-supported platforms
            _onAdClosed.OnNext(Unit.Default);
            return true;
#endif
        }

        protected override void DisposeAd()
        {
            if (_interstitialAd == null) return;
            _adsEventSubscription?.Dispose();
            _interstitialAd.Destroy();
            _interstitialAd = null;
        }

        protected override void RegisterAdEvents()
        {
            var builder = Disposable.CreateBuilder();
            Observable.FromEvent(
                    h => _interstitialAd.OnAdFullScreenContentClosed += h,
                    h => _interstitialAd.OnAdFullScreenContentClosed -= h)
                .Subscribe(_ => HandleAdClosed())
                .AddTo(ref builder);
            _adsEventSubscription = builder.Build();
        }
        
        private void HandleAdClosed()
        {
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                _onAdClosed.OnNext(Unit.Default);
            });
            Load();
        }
    }
    
    public class AdsService : IStartable, IDisposable
    {
        private readonly Dictionary<Type, AdsInstance> _adsInstances = new();

        public static string UnitId
        {
            get
            {
#if UNITY_ANDROID
                return "ca-app-pub-3940256099942544/5224354917";
#elif UNITY_IOS
                return "ca-app-pub-3940256099942544/2934735716";
#else
                return "ca-app-pub-3940256099942544/5224354917";
#endif
            }
        }

        public void Start()
        {
#if UNITY_EDITOR || UNITY_ANDROID || UNITY_IOS
            //MobileAds.RaiseAdEventsOnUnityMainThread = true;
            var requestConfiguration = new RequestConfiguration
            {
                TagForChildDirectedTreatment = TagForChildDirectedTreatment.True,
                MaxAdContentRating = MaxAdContentRating.G
            };
            MobileAds.SetRequestConfiguration(requestConfiguration);
            MobileAds.Initialize(status => 
            {
                InitializeAd();
            });
#else
                InitializeAd();
#endif
        }

        private void InitializeAd()
        {
            var rewardedAdInstance = new RewardedAdInstance();
            rewardedAdInstance.Load();
            _adsInstances[typeof(RewardedAdInstance)] = rewardedAdInstance;
            var interstitialAdInstance = new InterstitialAdInstance();
            interstitialAdInstance.Load();
            _adsInstances[typeof(InterstitialAdInstance)] = interstitialAdInstance;
        }

        public void Dispose()
        {
            _adsInstances.Values.ForEach(instance => instance?.Dispose());
        }
        
        public bool TryGetAdsInstance<T>(out T adsInstance) where T : AdsInstance
        {
            if (_adsInstances.TryGetValue(typeof(T), out var instance) && instance is T typedInstance)
            {
                adsInstance = typedInstance;
                return true;
            }
            adsInstance = null;
            return false;
        }
    }
}