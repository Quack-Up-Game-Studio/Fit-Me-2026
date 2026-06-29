using Sirenix.OdinInspector;
using UnityEngine;

namespace QuackUp.Utils
{
    [CreateAssetMenu(fileName = "AdsSettings", menuName = "QuackUp/Ads/AdsSettings")]
    public class AdsSettings : ScriptableObject
    {
        [SerializeField]
        [ReadOnly]
        [InfoBox("Currently using TEST Ads", InfoMessageType.Warning, "isTestMode")]
        [InfoBox("Currently using PRODUCTION Ads", InfoMessageType.Info, "@!isTestMode")]
        private bool isTestMode = true;

        public bool IsTestMode => isTestMode;

        [Button(ButtonSizes.Medium)]
        [LabelText("Switch to Test Ads")]
        [ShowIf("@!isTestMode")]
        public void SwitchToTest()
        {
            isTestMode = true;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        [Button(ButtonSizes.Medium)]
        [LabelText("Switch to Production Ads")]
        [ShowIf("isTestMode")]
        public void SwitchToProduction()
        {
            isTestMode = false;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        [Title("Banner Ads")]
        [TabGroup("Banner", "Android")]
        [SerializeField] private string androidBannerTestId = "ca-app-pub-3940256099942544/6300978111";
        [TabGroup("Banner", "Android")]
        [SerializeField] private string androidBannerProdId = "ca-app-pub-1737894207840657/4242862875";
        
        [TabGroup("Banner", "iOS")]
        [SerializeField] private string iosBannerTestId = "ca-app-pub-3940256099942544/2934735716";
        [TabGroup("Banner", "iOS")]
        [SerializeField] private string iosBannerProdId = "ca-app-pub-3940256099942544/2934735716";

        [Title("Adaptive Banner Ads")]
        [TabGroup("Adaptive Banner", "Android")]
        [SerializeField] private string androidAdaptiveBannerTestId = "ca-app-pub-3940256099942544/6300978111";
        [TabGroup("Adaptive Banner", "Android")]
        [SerializeField] private string androidAdaptiveBannerProdId = "ca-app-pub-1737894207840657/4242862875";
        
        [TabGroup("Adaptive Banner", "iOS")]
        [SerializeField] private string iosAdaptiveBannerTestId = "ca-app-pub-3940256099942544/2435281174";
        [TabGroup("Adaptive Banner", "iOS")]
        [SerializeField] private string iosAdaptiveBannerProdId = "ca-app-pub-3940256099942544/2435281174";

        [Title("Rewarded Ads")]
        [TabGroup("Rewarded", "Android")]
        [SerializeField] private string androidRewardedTestId = "ca-app-pub-3940256099942544/5224354917";
        [TabGroup("Rewarded", "Android")]
        [SerializeField] private string androidRewardedProdId = "ca-app-pub-1737894207840657/9545846120";
        
        [TabGroup("Rewarded", "iOS")]
        [SerializeField] private string iosRewardedTestId = "ca-app-pub-3940256099942544/1712485313";
        [TabGroup("Rewarded", "iOS")]
        [SerializeField] private string iosRewardedProdId = "ca-app-pub-3940256099942544/1712485313";

        [Title("Interstitial Ads")]
        [TabGroup("Interstitial", "Android")]
        [SerializeField] private string androidInterstitialTestId = "ca-app-pub-3940256099942544/1033173712";
        [TabGroup("Interstitial", "Android")]
        [SerializeField] private string androidInterstitialProdId = "ca-app-pub-3940256099942544/1033173712";
        
        [TabGroup("Interstitial", "iOS")]
        [SerializeField] private string iosInterstitialTestId = "ca-app-pub-3940256099942544/4411468910";
        [TabGroup("Interstitial", "iOS")]
        [SerializeField] private string iosInterstitialProdId = "ca-app-pub-3940256099942544/4411468910";

        // Getters
        public string BannerUnitId => GetUnitId(androidBannerTestId, androidBannerProdId, iosBannerTestId, iosBannerProdId);
        public string AdaptiveBannerUnitId => GetUnitId(androidAdaptiveBannerTestId, androidAdaptiveBannerProdId, iosAdaptiveBannerTestId, iosAdaptiveBannerProdId);
        public string RewardedUnitId => GetUnitId(androidRewardedTestId, androidRewardedProdId, iosRewardedTestId, iosRewardedProdId);
        public string InterstitialUnitId => GetUnitId(androidInterstitialTestId, androidInterstitialProdId, iosInterstitialTestId, iosInterstitialProdId);

        private string GetUnitId(string androidTest, string androidProd, string iosTest, string iosProd)
        {
            if (isTestMode)
            {
#if UNITY_ANDROID
                return androidTest;
#elif UNITY_IOS
                return iosTest;
#else
                return androidTest;
#endif
            }
            else
            {
#if UNITY_ANDROID
                return androidProd;
#elif UNITY_IOS
                return iosProd;
#else
                return androidProd;
#endif
            }
        }
    }
}
