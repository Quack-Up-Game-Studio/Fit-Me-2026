using System;
using System.Collections.Generic;
using System.Threading;
using GameAnalyticsSDK;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using R3;
using Sirenix.Utilities;
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
        /// Additional context for analytics, such as placement or reason for showing the ad. Must be set before calling <see cref="TryShow"/>.
        /// </summary>
        public string AdContext { get; set; } = "Unknown";
        
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
            if (_timerCts is { IsCancellationRequested: false })
            {
                _timerCts.Cancel();
            }
            _timerCts?.Dispose();
            _timerCts = null;
        }

        protected virtual void ReportAdEvent(GAAdAction adAction, GAAdType adType, string unitId)
        {
            var customField = new Dictionary<string, object>()
            {
                { "AdContext", AdContext },
            };
            GameAnalytics.NewAdEvent(adAction, adType,"admob", unitId, customFields: customField);
        }
    }

    public class BannerAdInstance : AdsInstance
    {
        public static string UnitId
        {
            get
            {
#if UNITY_ANDROID
                return "ca-app-pub-1737894207840657/4242862875";
#elif UNITY_IOS
                return "ca-app-pub-3940256099942544/2934735716";
#else
                return "ca-app-pub-3940256099942544/6300978111";
#endif
            }
        }
        
        public static string AdaptiveUnitId
        {
            get
            {
#if UNITY_ANDROID
                return "ca-app-pub-1737894207840657/4242862875";
#elif UNITY_IOS
                return "ca-app-pub-3940256099942544/2435281174";
#else
                return "ca-app-pub-3940256099942544/9214589741";
#endif
            }
        }
        
        private BannerView _bannerView;
        private bool _wasVisible;
        private int _loadedWidth;
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
            // Get the device safe width in density-independent pixels.
            var deviceWidth = MobileAds.Utils.GetDeviceSafeWidth();
            _loadedWidth = deviceWidth;
            // Define the anchored adaptive ad size.
            var adaptiveSize =
                AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(deviceWidth);
            _bannerView = new BannerView(AdaptiveUnitId, adaptiveSize, AdPosition.Bottom);
            GameAnalyticsILRD.SubscribeAdMobImpressions(AdaptiveUnitId, _bannerView);
            RegisterAdEvents();
            _bannerView.LoadAd(new AdRequest());
        }

        public override bool TryShow()
        {
            DebugUtils.Log($"BannerAdInstance: TryShow called. _bannerView is null: {_bannerView == null}, CanShowAd: {CanShowAd()}, Enabled: {Enabled}");
            if (_bannerView == null || _bannerView.IsDestroyed)
            {
                DebugUtils.LogWarning("Banner ad is not loaded yet.");
                _wasVisible = true;
                Load();
                return false;
            }
            if (!CanShowAd()) return false;

            var currentDeviceWidth = MobileAds.Utils.GetDeviceSafeWidth();
            if (_loadedWidth <= 0 && currentDeviceWidth > 0)
            {
                DebugUtils.Log($"BannerAdInstance: Loaded width was {_loadedWidth}, current width is {currentDeviceWidth}. Reloading banner with valid width.");
                _wasVisible = true;
                Load();
                return false;
            }

#if UNITY_EDITOR
            // Editor dummy client workaround - Hide() to Show() bug
            if (!_wasVisible)
            {
                DebugUtils.Log("BannerAdInstance: Editor dummy client workaround - reloading banner to show it.");
                _wasVisible = true;
                Load();
                return false;
            }
#endif

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
            DebugUtils.Log($"BannerAdInstance: TryHide called. _bannerView is null: {_bannerView == null}, CanShowAd: {CanShowAd()}, Enabled: {Enabled}");
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
            Observable.FromEvent(
                    h => _bannerView.OnAdClicked += h,
                    h => _bannerView.OnAdClicked -= h)
                .Subscribe(_ => HandleOnAdClicked())
                .AddTo(ref builder);
            _adsEventSubscription = builder.Build();
        }

        private void HandleAdLoaded()
        {
            DebugUtils.Log($"BannerAdInstance: HandleAdLoaded called. _wasVisible is: {_wasVisible}");
            if (!_wasVisible)
            {
                DebugUtils.Log("BannerAdInstance: _wasVisible is false, calling Hide()");
                _bannerView.Hide();
            }
            else
            {
                DebugUtils.Log("BannerAdInstance: _wasVisible is true, calling Show()");
                _bannerView.Show();
            }
            CountdownAdSession();
            ReportAdEvent(GAAdAction.Loaded, GAAdType.Banner, AdaptiveUnitId);
        }
        
        private void HandleAdLoadFailed(LoadAdError adError)
        {
            DebugUtils.LogError($"Failed to load banner ad: {adError.GetMessage()}");
            ReportAdEvent(GAAdAction.FailedShow, GAAdType.Banner, AdaptiveUnitId);
        }

        private void HandleOnAdClicked()
        {
            ReportAdEvent(GAAdAction.Clicked, GAAdType.Banner, AdaptiveUnitId);
        }
    }
    
    public class RewardedAdInstance : AdsInstance
    {
        public static string UnitId
        {
            get
            {
#if UNITY_ANDROID
                return "ca-app-pub-1737894207840657/9545846120";
#elif UNITY_IOS
                return "ca-app-pub-3940256099942544/1712485313";
#else
                return "ca-app-pub-3940256099942544/5224354917";
#endif
            }
        }
        
        // Event เพื่อบอกภายนอกว่า "ได้รางวัลแล้วนะ"
        public Observable<Unit> OnUserEarnedReward => _onUserEarnedReward;
        private readonly Subject<Unit> _onUserEarnedReward = new();
        
        private RewardedAd _rewardedAd;
        private bool _isRewardEarned;

        public override bool CanShowAd() => Enabled && _rewardedAd != null && _rewardedAd.CanShowAd();

        public override void Load()
        {
            DisposeAd();
            RewardedAd.Load(UnitId, new AdRequest(), (ad, error) =>
            {
                if (error != null || ad == null)
                {
                    DebugUtils.LogError($"Failed to load rewarded ad: {error?.GetMessage()}");
                    return;
                }
                _rewardedAd = ad;
                GameAnalyticsILRD.SubscribeAdMobImpressions(UnitId, _rewardedAd);
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
                ReportAdEvent(GAAdAction.Show, GAAdType.RewardedVideo, UnitId);
                return true;
            }
        
            DebugUtils.Log("Ads is not ready yet.");
            Load();
            ReportAdEvent(GAAdAction.FailedShow, GAAdType.RewardedVideo, UnitId);
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
            Observable.FromEvent(
                h => _rewardedAd.OnAdClicked += h,
                h => _rewardedAd.OnAdClicked -= h)
                .Subscribe(_ => HandleAdClicked())
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
                    ReportAdEvent(GAAdAction.RewardReceived, GAAdType.RewardedVideo, UnitId);
                }
                _onAdClosed.OnNext(Unit.Default);
            });
            Load();
        }
        
        private void HandleAdClicked()
        {
            ReportAdEvent(GAAdAction.Clicked, GAAdType.RewardedVideo, UnitId);
        }
    }
    
    public class InterstitialAdInstance : AdsInstance
    {
        public static string UnitId
        {
            get
            {
#if UNITY_ANDROID
                return "ca-app-pub-3940256099942544/1033173712";
#elif UNITY_IOS
                return "ca-app-pub-3940256099942544/4411468910";
#else
                return "ca-app-pub-3940256099942544/1033173712";
#endif
            }
        }
        
        private InterstitialAd _interstitialAd;
        public override bool CanShowAd() => Enabled && _interstitialAd != null && _interstitialAd.CanShowAd();
        public override void Load()
        {
            DisposeAd();
            InterstitialAd.Load(UnitId, new AdRequest(), (ad, error) =>
            {
                if (error != null || ad == null)
                {
                    DebugUtils.LogError($"Failed to load interstitial ad: {error?.GetMessage()}");
                    return;
                }

                _interstitialAd = ad;
                GameAnalyticsILRD.SubscribeAdMobImpressions(UnitId, _interstitialAd);
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
                ReportAdEvent(GAAdAction.Show, GAAdType.Interstitial, UnitId);
                return true;
            }
            DebugUtils.LogWarning("Ads is not ready yet.");
            Load();
            ReportAdEvent(GAAdAction.FailedShow, GAAdType.Interstitial, UnitId);
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
            Observable.FromEvent(
                    h => _interstitialAd.OnAdClicked += h,
                    h => _interstitialAd.OnAdClicked -= h)
                .Subscribe(_ => HandleAdClicked())
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
        
        private void HandleAdClicked()
        {
            ReportAdEvent(GAAdAction.Clicked, GAAdType.Interstitial, UnitId);
        }
    }
    
    public class AdsService : IStartable, IDisposable
    {
        private readonly Dictionary<Type, AdsInstance> _adsInstances = new();

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