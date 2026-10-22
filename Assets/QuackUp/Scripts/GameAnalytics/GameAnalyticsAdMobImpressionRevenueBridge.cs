using System;
using GameAnalyticsSDK;
using GoogleMobileAds.Api;
using QuackUp.GoogleAdMob;

namespace FitMe.GameAnalytics
{
    /// <summary>
    /// AdMob-specific GameAnalytics ILRD integration.
    /// </summary>
    public sealed class GameAnalyticsAdMobImpressionRevenueBridge : IAdMobImpressionRevenueBridge
    {
        public void SubscribeAdMobImpressions(string unitId, BannerView banner)
        {
            if (banner == null) throw new ArgumentNullException(nameof(banner));
#if gameanalytics_admob_enabled
            GameAnalyticsILRD.SubscribeAdMobImpressions(unitId, banner);
#endif
        }

        public void SubscribeAdMobImpressions(string unitId, InterstitialAd interstitial)
        {
            if (interstitial == null) throw new ArgumentNullException(nameof(interstitial));
#if gameanalytics_admob_enabled
            GameAnalyticsILRD.SubscribeAdMobImpressions(unitId, interstitial);
#endif
        }

        public void SubscribeAdMobImpressions(string unitId, RewardedAd rewarded)
        {
            if (rewarded == null) throw new ArgumentNullException(nameof(rewarded));
#if gameanalytics_admob_enabled
            GameAnalyticsILRD.SubscribeAdMobImpressions(unitId, rewarded);
#endif
        }
    }
}