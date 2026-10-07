using System;
using System.Collections.Generic;
using System.Threading;
using QuackUp.Analytics;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using R3;
using Sirenix.Utilities;
using VContainer;
using VContainer.Unity;
using UnityEngine.Scripting.APIUpdating;
using QuackUp.Utils;

namespace QuackUp.GoogleAdMob
{
    public sealed class RewardedAdShowAttempt : IDisposable
    {
        private readonly Action _onReward;
        private IDisposable _subscription;
        private bool _isCompleted;
        public bool IsCompleted => _isCompleted;

        public RewardedAdShowAttempt(
            Observable<Unit> rewards,
            Observable<Unit> closed,
            Observable<Unit> failed,
            Action onReward)
        {
            _onReward = onReward ?? throw new ArgumentNullException(nameof(onReward));
            var builder = Disposable.CreateBuilder();
            rewards.Subscribe(_ => CompleteWithReward()).AddTo(ref builder);
            closed.Subscribe(_ => Dispose()).AddTo(ref builder);
            failed.Subscribe(_ => Dispose()).AddTo(ref builder);
            _subscription = builder.Build();
        }

        public bool TryShow(Func<bool> show)
        {
            if (_isCompleted) return false;
            var shown = show?.Invoke() == true;
            if (!shown) Dispose();
            return shown;
        }

        public void Dispose()
        {
            if (_isCompleted) return;
            _isCompleted = true;
            _subscription?.Dispose();
            _subscription = null;
        }

        private void CompleteWithReward()
        {
            if (_isCompleted) return;
            Dispose();
            _onReward();
        }
    }

    public abstract class AdsInstance : IDisposable
    {
        private const int InitialLoadRetryDelaySeconds = 2;
        private const int MaximumLoadRetryDelaySeconds = 60;
        private const int MaximumLoadRetryExponent = 5;
        // Event บอกว่าโฆษณาปิดแล้ว (ไม่ว่าจะได้รางวัลหรือไม่)
        protected readonly AdsSettings _adsSettings;
        protected readonly IAnalyticsService _analyticsService;
        protected readonly IAdMobImpressionRevenueBridge _adMobImpressionRevenueBridge;
        public Observable<Unit> OnAdClosed => _onAdClosed;
        protected readonly Subject<Unit> _onAdClosed = new();
        public Observable<Unit> OnAdFailed => _onAdFailed;
        protected readonly Subject<Unit> _onAdFailed = new();
        
        protected IDisposable _adsRefreshTimer;
        protected IDisposable _adsEventSubscription;
        protected CancellationTokenSource _timerCts = new();
        private IDisposable _loadRetryTimer;
        private CancellationTokenSource _loadRetryCts;
        private int _loadRetryAttempt;
        private bool _isDisposed;
        
        [Inject]
        public AdsInstance(AdsSettings adsSettings, IAnalyticsService analyticsService,
            IAdMobImpressionRevenueBridge adMobImpressionRevenueBridge)
        {
            _adsSettings = adsSettings;
            _analyticsService = analyticsService;
            _adMobImpressionRevenueBridge = adMobImpressionRevenueBridge;
        }
        
        /// <summary>
        /// Additional context for analytics, such as placement or reason for showing the ad. Must be set before calling <see cref="TryShow"/>.
        /// </summary>
        public string AdContext { get; set; } = "Unknown";
        
        /// <summary>
        /// ใช้สำหรับเปิดปิดการแสดงโฆษณา (เช่น ผู้เล่นซื้อการลบโฆษณา หรือ ปิดโฆษณาชั่วคราวเพื่อทดสอบ)
        /// </summary>
        public virtual bool Enabled { get; set; } = true;

        public abstract bool CanShowAd();

        public virtual void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            DisposeAd();
            CancelAdsSessionTimer();
            CancelPendingLoadRetry();
            _onAdClosed.Dispose();
            _onAdFailed.Dispose();
        }
        
        public abstract void Load();
        public abstract bool TryShow();
        protected abstract void DisposeAd();
        protected abstract void RegisterAdEvents();

        protected virtual void CountdownAdSession()
        {
            CancelAdsSessionTimer();
            _timerCts = new CancellationTokenSource();
            var cancellationToken = _timerCts.Token;
            _adsRefreshTimer = Observable.Timer(TimeSpan.FromHours(1), TimeProvider.System, cancellationToken)
                .Subscribe(_ => ExecuteOnUnityMainThread(() =>
                {
                    if (!cancellationToken.IsCancellationRequested && !_isDisposed) Load();
                }));
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

        protected static void ExecuteOnUnityMainThread(Action action)
        {
            if (action != null) MobileAdsEventExecutor.ExecuteInUpdate(action);
        }

        protected bool IsDisposed => _isDisposed;

        protected void ScheduleLoadRetry(Action retry)
        {
            if (_isDisposed || retry == null) return;

            CancelPendingLoadRetry();
            var exponent = Math.Min(_loadRetryAttempt, MaximumLoadRetryExponent);
            var delaySeconds = Math.Min(InitialLoadRetryDelaySeconds * (1 << exponent), MaximumLoadRetryDelaySeconds);
            _loadRetryAttempt = Math.Min(_loadRetryAttempt + 1, MaximumLoadRetryExponent);

            _loadRetryCts = new CancellationTokenSource();
            var cancellationToken = _loadRetryCts.Token;
            _loadRetryTimer = Observable.Timer(TimeSpan.FromSeconds(delaySeconds), TimeProvider.System, cancellationToken)
                .Subscribe(_ => ExecuteOnUnityMainThread(() =>
                {
                    if (cancellationToken.IsCancellationRequested || _isDisposed) return;
                    CancelPendingLoadRetry();
                    retry();
                }));
        }

        protected void CancelPendingLoadRetry()
        {
            _loadRetryTimer?.Dispose();
            _loadRetryTimer = null;
            var retryCts = _loadRetryCts;
            _loadRetryCts = null;
            if (retryCts == null) return;
            if (!retryCts.IsCancellationRequested) retryCts.Cancel();
            retryCts.Dispose();
        }

        protected void ResetLoadRetry()
        {
            _loadRetryAttempt = 0;
            CancelPendingLoadRetry();
        }

        protected virtual void ReportAdEvent(AdAction adAction, AdType adType, string unitId)
        {
            _analyticsService.TrackAdEvent(adAction, adType, unitId, AdContext);
        }
    }

    public class BannerAdInstance : AdsInstance
    {
        public BannerAdInstance(AdsSettings adsSettings, IAnalyticsService analyticsService,
            IAdMobImpressionRevenueBridge adMobImpressionRevenueBridge)
            : base(adsSettings, analyticsService, adMobImpressionRevenueBridge) {}

        public string UnitId => _adsSettings.BannerUnitId;
        public string AdaptiveUnitId => _adsSettings.AdaptiveBannerUnitId;
        
        private BannerView _bannerView;
        private bool _wasVisible;
        private int _loadedWidth;
        public override bool CanShowAd() => Enabled && AllowShow && _bannerView is {IsDestroyed:  false};
        private bool _enabled = true;
        private bool _allowShow;

        public override bool Enabled
        {
            get => _enabled;
            set
            {
                if (!AllowShow)
                {
                    TryHide();
                    _enabled = value;
                    return;
                }
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

        /// <summary>
        /// Indicates whether the banner ad is allowed to be shown. This has to be set manually from the class that handle ads and scene loading.
        /// </summary>
        public bool AllowShow
        {
            get => _allowShow;
            set
            {
                if (!Enabled)
                {
                    TryHide();
                    _allowShow = value;
                    return;
                }
                if (!value)
                {
                    TryHide();
                    _allowShow = false;
                }
                else
                {
                    _allowShow = true;
                }
            }
        }

        public override void Load()
        {
            CancelPendingLoadRetry();
            DisposeAd();
            // Get the device safe width in density-independent pixels.
            var deviceWidth = MobileAds.Utils.GetDeviceSafeWidth();
            _loadedWidth = deviceWidth;
            // Define the anchored adaptive ad size.
            var adaptiveSize =
                AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(deviceWidth);
            _bannerView = new BannerView(AdaptiveUnitId, adaptiveSize, AdPosition.Bottom);
            _adMobImpressionRevenueBridge.SubscribeAdMobImpressions(AdaptiveUnitId, _bannerView);
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
                _onAdFailed?.OnNext(Unit.Default);
                return false;
            }
            if (!CanShowAd())
            {
                _onAdFailed?.OnNext(Unit.Default);
                return false;
            }

            var currentDeviceWidth = MobileAds.Utils.GetDeviceSafeWidth();
            if (_loadedWidth <= 0 && currentDeviceWidth > 0)
            {
                DebugUtils.Log($"BannerAdInstance: Loaded width was {_loadedWidth}, current width is {currentDeviceWidth}. Reloading banner with valid width.");
                _wasVisible = true;
                Load();
                _onAdFailed?.OnNext(Unit.Default);
                return false;
            }

#if UNITY_EDITOR
            // Editor dummy client workaround - Hide() to Show() bug
            if (!_wasVisible)
            {
                DebugUtils.Log("BannerAdInstance: Editor dummy client workaround - reloading banner to show it.");
                _wasVisible = true;
                Load();
                _onAdFailed?.OnNext(Unit.Default);
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
            _bannerView?.Hide();
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
                .Subscribe(_ =>
                {
                    ReportAdEvent(AdAction.Loaded, AdType.Banner, AdaptiveUnitId);
                    ExecuteOnUnityMainThread(HandleAdLoaded);
                })
                .AddTo(ref builder);
            Observable.FromEvent<LoadAdError>(
                    h => _bannerView.OnBannerAdLoadFailed += h,
                    h => _bannerView.OnBannerAdLoadFailed -= h)
                .Subscribe(adError =>
                {
                    ReportAdEvent(AdAction.FailedShow, AdType.Banner, AdaptiveUnitId);
                    ExecuteOnUnityMainThread(() => HandleAdLoadFailed(adError));
                })
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
            if (IsDisposed) return;
            ResetLoadRetry();
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
        }
        
        private void HandleAdLoadFailed(LoadAdError adError)
        {
            if (IsDisposed) return;
            DebugUtils.LogError($"Failed to load banner ad: {adError.GetMessage()}");
            _onAdFailed?.OnNext(Unit.Default);
            ScheduleLoadRetry(Load);
        }

        private void HandleOnAdClicked()
        {
            ReportAdEvent(AdAction.Clicked, AdType.Banner, AdaptiveUnitId);
        }
    }
    
    public class RewardedAdInstance : AdsInstance
    {
        public RewardedAdInstance(AdsSettings adsSettings, IAnalyticsService analyticsService,
            IAdMobImpressionRevenueBridge adMobImpressionRevenueBridge)
            : base(adsSettings, analyticsService, adMobImpressionRevenueBridge)
        {
            var builder = Disposable.CreateBuilder();
            OnAdClosed.Subscribe(_ => FinishActiveShow()).AddTo(ref builder);
            OnAdFailed.Subscribe(_ => FinishActiveShow()).AddTo(ref builder);
            _showLifecycleSubscription = builder.Build();
        }

        public string UnitId => _adsSettings.RewardedUnitId;
        
        // Event เพื่อบอกภายนอกว่า "ได้รางวัลแล้วนะ"
        public Observable<Unit> OnUserEarnedReward => _onUserEarnedReward;
        private readonly Subject<Unit> _onUserEarnedReward = new();
        
        private RewardedAd _rewardedAd;
        private int _loadGeneration;
        private RewardedAdShowAttempt _activeShowAttempt;
        private readonly IDisposable _showLifecycleSubscription;
        private bool _showInProgress;

        public override bool CanShowAd() => !IsDisposed && !_showInProgress && Enabled && _rewardedAd != null && _rewardedAd.CanShowAd();

        public bool TryShow(string adContext, Action onReward, out RewardedAdShowAttempt attempt)
        {
            attempt = null;
            // Reject before installing listeners, changing placement, or touching the active SDK ad.
            if (IsDisposed || _showInProgress || _activeShowAttempt != null) return false;
            var ownedAttempt = new RewardedAdShowAttempt(OnUserEarnedReward, OnAdClosed, OnAdFailed, onReward);
            _activeShowAttempt = ownedAttempt;
            attempt = ownedAttempt;
            AdContext = adContext;
            try
            {
                if (ownedAttempt.TryShow(TryShow)) return true;
                if (ReferenceEquals(_activeShowAttempt, ownedAttempt)) FinishActiveShow();
                return false;
            }
            catch
            {
                if (ReferenceEquals(_activeShowAttempt, ownedAttempt)) FinishActiveShow();
                ownedAttempt.Dispose();
                throw;
            }
        }

        protected int BeginLoadGeneration() => ++_loadGeneration;

        protected bool CanAcceptLoadedAd(int generation) =>
            !IsDisposed && generation == _loadGeneration && !_showInProgress;

        protected void MarkShowStarted() => _showInProgress = true;

        private void FinishActiveShow()
        {
            var attempt = _activeShowAttempt;
            _activeShowAttempt = null;
            _showInProgress = false;
            attempt?.Dispose();
        }

        public override void Load()
        {
            if (IsDisposed || _showInProgress) return;
            DisposeAd();
            var loadGeneration = BeginLoadGeneration();
            RewardedAd.Load(UnitId, new AdRequest(), (ad, error) =>
            {
                if (error != null || ad == null)
                {
                    ExecuteOnUnityMainThread(() =>
                    {
                        if (!IsDisposed) DebugUtils.LogError($"Failed to load rewarded ad: {error?.GetMessage()}");
                    });
                    return;
                }
                _adMobImpressionRevenueBridge.SubscribeAdMobImpressions(UnitId, ad);
                ExecuteOnUnityMainThread(() =>
                {
                    if (!CanAcceptLoadedAd(loadGeneration))
                    {
                        ad.Destroy();
                        return;
                    }
                    _rewardedAd = ad;
                    RegisterAdEvents();
                    CountdownAdSession();
                });
            });
        }

        public override bool TryShow()
        {
            if (IsDisposed || _showInProgress) return false;
#if UNITY_EDITOR || UNITY_ANDROID || UNITY_IOS
            if (!Enabled)
            {
                DebugUtils.LogWarning("Ads is disabled");
                _onAdFailed?.OnNext(Unit.Default);
                return false;
            }
            if (CanShowAd())
            {
                var rewardCallbackHandled = 0;
                var ad = _rewardedAd;
                var owner = _activeShowAttempt;
                MarkShowStarted();
                CancelAdsSessionTimer();
                // Reward delivery is tied to the SDK's earned-reward callback, not ad-close ordering.
                ad.Show(_ =>
                {
                    if (Interlocked.Exchange(ref rewardCallbackHandled, 1) != 0) return;
                    ReportAdEvent(AdAction.RewardReceived, AdType.RewardedVideo, UnitId);
                    ExecuteOnUnityMainThread(() =>
                    {
                        if (!IsDisposed && ReferenceEquals(_rewardedAd, ad) && ReferenceEquals(_activeShowAttempt, owner))
                            _onUserEarnedReward.OnNext(Unit.Default);
                    });
                });
                ReportAdEvent(AdAction.Show, AdType.RewardedVideo, UnitId);
                return true;
            }
        
            DebugUtils.Log("Ads is not ready yet.");
            Load();
            ReportAdEvent(AdAction.FailedShow, AdType.RewardedVideo, UnitId);
            _onAdFailed?.OnNext(Unit.Default);
            return false;
            
#else // Simulate ad success in non-supported platforms
            _onUserEarnedReward.OnNext(Unit.Default);
            _onAdClosed.OnNext(Unit.Default);
            return true;
#endif
        }

        protected override void DisposeAd()
        {
            _loadGeneration++;
            if (_rewardedAd == null) return;
            _adsEventSubscription?.Dispose();
            _rewardedAd.Destroy();
            _rewardedAd = null;
        }

        public override void Dispose()
        {
            FinishActiveShow();
            _showLifecycleSubscription.Dispose();
            base.Dispose();
            _onUserEarnedReward.Dispose();
        }

        protected override void RegisterAdEvents()
        {
            var ad = _rewardedAd;
            var builder = Disposable.CreateBuilder();
            Observable.FromEvent(
                h => ad.OnAdFullScreenContentClosed += h,
                h => ad.OnAdFullScreenContentClosed -= h)
                .Subscribe(_ => ExecuteOnUnityMainThread(() =>
                {
                    if (ReferenceEquals(_rewardedAd, ad)) HandleAdClosed();
                }))
                .AddTo(ref builder);
            Observable.FromEvent(
                h => ad.OnAdClicked += h,
                h => ad.OnAdClicked -= h)
                .Subscribe(_ => HandleAdClicked())
                .AddTo(ref builder);
            Observable.FromEvent<AdError>(
                    h => ad.OnAdFullScreenContentFailed += h,
                    h => ad.OnAdFullScreenContentFailed -= h)
                .Subscribe(error =>
                {
                    ReportAdEvent(AdAction.FailedShow, AdType.RewardedVideo, UnitId);
                    ExecuteOnUnityMainThread(() =>
                    {
                        if (ReferenceEquals(_rewardedAd, ad)) OnAdFullScreenContentFailed(error);
                    });
                })
                .AddTo(ref builder);
            _adsEventSubscription = builder.Build();
        }
        
        private void HandleAdClosed()
        {
            if (IsDisposed) return;
            _onAdClosed.OnNext(Unit.Default);
            if (_activeShowAttempt == null && !_showInProgress) Load();
        }
        
        private void HandleAdClicked()
        {
            ReportAdEvent(AdAction.Clicked, AdType.RewardedVideo, UnitId);
        }
        
        private void OnAdFullScreenContentFailed(AdError error)
        {
            if (IsDisposed) return;
            DebugUtils.LogError($"Rewarded ad failed to show: {error.GetMessage()}");
            _onAdFailed?.OnNext(Unit.Default);
            if (_activeShowAttempt == null && !_showInProgress) Load();
        }
    }
    
    public class InterstitialAdInstance : AdsInstance
    {
        public InterstitialAdInstance(AdsSettings adsSettings, IAnalyticsService analyticsService,
            IAdMobImpressionRevenueBridge adMobImpressionRevenueBridge)
            : base(adsSettings, analyticsService, adMobImpressionRevenueBridge) {}

        public string UnitId => _adsSettings.InterstitialUnitId;
        
        private InterstitialAd _interstitialAd;
        public override bool CanShowAd() => Enabled && _interstitialAd != null && _interstitialAd.CanShowAd();
        public override void Load()
        {
            DisposeAd();
            InterstitialAd.Load(UnitId, new AdRequest(), (ad, error) =>
            {
                if (error != null || ad == null)
                {
                    ExecuteOnUnityMainThread(() =>
                    {
                        if (!IsDisposed) DebugUtils.LogError($"Failed to load interstitial ad: {error?.GetMessage()}");
                    });
                    return;
                }
                _adMobImpressionRevenueBridge.SubscribeAdMobImpressions(UnitId, ad);
                ExecuteOnUnityMainThread(() =>
                {
                    if (IsDisposed)
                    {
                        ad.Destroy();
                        return;
                    }
                    _interstitialAd = ad;
                    RegisterAdEvents();
                    CountdownAdSession();
                });
            });
        }
        
        public override bool TryShow()
        {
#if UNITY_EDITOR || UNITY_ANDROID || UNITY_IOS
            if (!Enabled)
            {
                DebugUtils.LogWarning("Ads is disabled");
                _onAdFailed?.OnNext(Unit.Default);
                return false;
            }
            if (CanShowAd())
            {
                _interstitialAd.Show();
                ReportAdEvent(AdAction.Show, AdType.Interstitial, UnitId);
                return true;
            }
            DebugUtils.LogWarning("Ads is not ready yet.");
            Load();
            ReportAdEvent(AdAction.FailedShow, AdType.Interstitial, UnitId);
            _onAdFailed?.OnNext(Unit.Default);
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
                .Subscribe(_ => ExecuteOnUnityMainThread(HandleAdClosed))
                .AddTo(ref builder);
            Observable.FromEvent(
                    h => _interstitialAd.OnAdClicked += h,
                    h => _interstitialAd.OnAdClicked -= h)
                .Subscribe(_ => HandleAdClicked())
                .AddTo(ref builder);
            Observable.FromEvent<AdError>(
                    h => _interstitialAd.OnAdFullScreenContentFailed += h,
                    h => _interstitialAd.OnAdFullScreenContentFailed -= h)
                .Subscribe(error =>
                {
                    ReportAdEvent(AdAction.FailedShow, AdType.Interstitial, UnitId);
                    ExecuteOnUnityMainThread(() => OnAdFullScreenContentFailed(error));
                })
                .AddTo(ref builder);
            _adsEventSubscription = builder.Build();
        }
        
        private void HandleAdClosed()
        {
            if (IsDisposed) return;
            _onAdClosed.OnNext(Unit.Default);
            Load();
        }
        
        private void HandleAdClicked()
        {
            ReportAdEvent(AdAction.Clicked, AdType.Interstitial, UnitId);
        }
        
        private void OnAdFullScreenContentFailed(AdError error)
        {
            if (IsDisposed) return;
            DebugUtils.LogError($"Interstitial ad failed to show: {error.GetMessage()}");
            _onAdFailed?.OnNext(Unit.Default);
            Load();
        }
    }
    
    [MovedFrom("QuackUp.Utils")]
    public class AdsService : IPostInitializable, IDisposable
    {
        public ReadOnlyReactiveProperty<bool> AdsEnabled => _adsEnabled;
        private readonly ReactiveProperty<bool> _adsEnabled = new(true);
        private readonly Dictionary<Type, AdsInstance> _adsInstances = new();
        private readonly AdsSettings _adsSettings;
        private readonly IAnalyticsService _analyticsService;
        private readonly IAdMobImpressionRevenueBridge _adMobImpressionRevenueBridge;
        private bool _isDisposed;

        [Inject]
        public AdsService(AdsSettings adsSettings, IAnalyticsService analyticsService,
            IAdMobImpressionRevenueBridge adMobImpressionRevenueBridge)
        {
            _adsSettings = adsSettings;
            _analyticsService = analyticsService;
            _adMobImpressionRevenueBridge = adMobImpressionRevenueBridge;
        }

        public void PostInitialize()
        {
            if (_isDisposed) return;
#if UNITY_EDITOR || UNITY_ANDROID || UNITY_IOS
            var requestConfiguration = new RequestConfiguration
            {
                TagForChildDirectedTreatment = TagForChildDirectedTreatment.True,
                MaxAdContentRating = MaxAdContentRating.G
            };
            MobileAds.SetRequestConfiguration(requestConfiguration);
            MobileAds.Initialize(status => MobileAdsEventExecutor.ExecuteInUpdate(InitializeAd));
#else
                InitializeAd();
#endif
        }

        private void InitializeAd()
        {
            if (_isDisposed) return;
            var rewardedAdInstance = new RewardedAdInstance(_adsSettings, _analyticsService, _adMobImpressionRevenueBridge);
            rewardedAdInstance.Enabled = _adsEnabled.Value;
            rewardedAdInstance.Load();
            _adsInstances[typeof(RewardedAdInstance)] = rewardedAdInstance;
            var interstitialAdInstance = new InterstitialAdInstance(_adsSettings, _analyticsService, _adMobImpressionRevenueBridge);
            interstitialAdInstance.Enabled = _adsEnabled.Value;
            interstitialAdInstance.Load();
            _adsInstances[typeof(InterstitialAdInstance)] = interstitialAdInstance;
            var bannerAdInstance = new BannerAdInstance(_adsSettings, _analyticsService, _adMobImpressionRevenueBridge);
            bannerAdInstance.Enabled = _adsEnabled.Value;
            bannerAdInstance.Load();
            _adsInstances[typeof(BannerAdInstance)] = bannerAdInstance;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _adsInstances.Values.ForEach(instance => instance?.Dispose());
            _adsInstances.Clear();
            _adsEnabled.Dispose();
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
            _adsEnabled.Value = state;
            foreach (var instance in _adsInstances.Values)
            {
                instance.Enabled = state;
            }
        }
    }
}