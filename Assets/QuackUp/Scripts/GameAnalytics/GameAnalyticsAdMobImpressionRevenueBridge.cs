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
            GameAnalyticsILRD.SubscribeAdMobImpressions(unitId, banner);
        }

        public void SubscribeAdMobImpressions(string unitId, InterstitialAd interstitial)
        {
            if (interstitial == null) throw new ArgumentNullException(nameof(interstitial));
            GameAnalyticsILRD.SubscribeAdMobImpressions(unitId, interstitial);
        }

        public void SubscribeAdMobImpressions(string unitId, RewardedAd rewarded)
        {
            if (rewarded == null) throw new ArgumentNullException(nameof(rewarded));
            GameAnalyticsILRD.SubscribeAdMobImpressions(unitId, rewarded);
        }
    }
}