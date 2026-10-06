using GoogleMobileAds.Api;

namespace QuackUp.GoogleAdMob
{
    /// <summary>
    /// Connects AdMob ad objects to impression-level revenue analytics.
    /// </summary>
    public interface IAdMobImpressionRevenueBridge
    {
        void SubscribeAdMobImpressions(string unitId, BannerView banner);
        void SubscribeAdMobImpressions(string unitId, InterstitialAd interstitial);
        void SubscribeAdMobImpressions(string unitId, RewardedAd rewarded);
    }
}