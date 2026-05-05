using System;
using System.Collections.Generic;
using System.Threading;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using GoogleMobileAds.Unity;
using R3;
using Sirenix.Utilities;
using UnityEngine;
using VContainer;
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
        
        /// <summary>
        /// ใช้สำหรับเปิดปิดการแสดงโฆษณา (เช่น ผู้เล่นซื้อการลบโฆษณา หรือ ปิดโฆษณาชั่วคราวเพื่อทดสอบ)
        /// </summary>
        public virtual bool Enabled { get; set; } = true;

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

    public class BannerAdInstance : AdsInstance
    {
        private BannerView _bannerView;
        private bool _wasVisible;
        public override bool CanShowAd() => Enabled && _bannerView is {IsDestroyed:  false};
        private bool _enabled = true;

        public override bool Enabled
        {
            get => _enabled;
            set
            {
                if (!value)
                {
                    TryHide();
                    _enabled = false;
                }
                else
                {
                    _enabled = true;
                    TryShow();
                }
            }
        }

        public override void Load()
        {
            DisposeAd();
            _bannerView = new BannerView(AdsService.UnitId, AdSize.Banner, AdPosition.Bottom);
            RegisterAdEvents();
            _bannerView.LoadAd(new AdRequest());
        }

        public override bool TryShow()
        {
            if (_bannerView == null || _bannerView.IsDestroyed)
            {
                DebugUtils.LogWarning("Banner ad is not loaded yet.");
                _wasVisible = true;
                Load();
                return false;
            }
            if (!CanShowAd()) return false;
            // if (_bannerView == null || _bannerView.IsDestroyed)
            // {
            //     Load();
            // }
            _bannerView.Show();
            _wasVisible = true;
            return true;
        }

        public void DestroyView()
        {
            TryHide();
            DisposeAd();
        }

        public bool TryHide()
        {
            if (!CanShowAd()) return false;
            _bannerView.Hide();
            _wasVisible = false;
            return true;
        }

        protected override void DisposeAd()
        {
            //if (_bannerView == null) return;
            _adsEventSubscription?.Dispose();
            _bannerView?.Destroy();
            _bannerView = null;
        }

        protected override void RegisterAdEvents()
        {
            var builder = Disposable.CreateBuilder();
            Observable.FromEvent(
                    h => _bannerView.OnBannerAdLoaded += h,
                    h => _bannerView.OnBannerAdLoaded -= h)
                .Subscribe(_ => HandleAdLoaded())
                .AddTo(ref builder);
            Observable.FromEvent<LoadAdError>(
                    h => _bannerView.OnBannerAdLoadFailed += h,
                    h => _bannerView.OnBannerAdLoadFailed -= h)
                .Subscribe(HandleAdLoadFailed)
                .AddTo(ref builder);
            _adsEventSubscription = builder.Build();
        }

        private void HandleAdLoaded()
        {
            DebugUtils.Log("Banner ad loaded successfully.");
            if (!_wasVisible)
            {
                _bannerView.Hide();
            }
            else
            {
                _bannerView.Show();
            }
            CountdownAdSession();
        }
        
        private void HandleAdLoadFailed(LoadAdError adError)
        {
            DebugUtils.LogError($"Failed to load banner ad: {adError.GetMessage()}");
        }
    }
    
    public class RewardedAdInstance : AdsInstance
    {
        // Event เพื่อบอกภายนอกว่า "ได้รางวัลแล้วนะ"
        public Observable<Unit> OnUserEarnedReward => _onUserEarnedReward;
        private readonly Subject<Unit> _onUserEarnedReward = new();
        
        private RewardedAd _rewardedAd;
        private bool _isRewardEarned;

        public override bool CanShowAd() => Enabled && _rewardedAd != null && _rewardedAd.CanShowAd();

        public override void Load()
        {
            DisposeAd();
            RewardedAd.Load(AdsService.UnitId, new AdRequest(), (ad, error) =>
            {
                if (error != null || ad == null)
                {
                    DebugUtils.LogError($"Failed to load rewarded ad: {error?.GetMessage()}");
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
            if (!Enabled)
            {
                DebugUtils.LogWarning("Ads is disabled");
                return false;
            }
            if (CanShowAd())
            {
                _isRewardEarned = false;
                _rewardedAd.Show(reward =>
                {
                    _isRewardEarned = true;
                });
                return true;
            }
        
            DebugUtils.Log("Ads is not ready yet.");
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
        public override bool CanShowAd() => Enabled && _interstitialAd != null && _interstitialAd.CanShowAd();
        public override void Load()
        {
            DisposeAd();
            InterstitialAd.Load(AdsService.UnitId, new AdRequest(), (ad, error) =>
            {
                if (error != null || ad == null)
                {
                    DebugUtils.LogError($"Failed to load interstitial ad: {error?.GetMessage()}");
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
            if (!Enabled)
            {
                DebugUtils.LogWarning("Ads is disabled");
                return false;
            }
            if (CanShowAd())
            {
                _interstitialAd.Show();
                return true;
            }
            DebugUtils.LogWarning("Ads is not ready yet.");
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
            var bannerAdInstance = new BannerAdInstance();
            bannerAdInstance.Load();
            _adsInstances[typeof(BannerAdInstance)] = bannerAdInstance;
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

        public void SetEnableStateAll(bool state)
        {
            foreach (var instance in _adsInstances.Values)
            {
                instance.Enabled = state;
            }
        }
    }
}