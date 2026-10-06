using System;
using System.Collections.Generic;
using GameAnalyticsSDK;
using QuackUp.Analytics;

namespace FitMe.GameAnalytics
{
    /// <summary>
    /// GameAnalytics provider adapter. Game code depends only on IAnalyticsService.
    /// </summary>
    public sealed class GameAnalyticsAnalyticsService : IAnalyticsService
    {
        public void TrackProgression(ProgressionStatus status, string progression01)
        {
            GameAnalyticsSDK.GameAnalytics.NewProgressionEvent(ToProviderStatus(status), progression01);
        }

        public void TrackProgression(ProgressionStatus status, string progression01, string progression02)
        {
            GameAnalyticsSDK.GameAnalytics.NewProgressionEvent(ToProviderStatus(status), progression01, progression02);
        }

        public void TrackProgression(ProgressionStatus status, string progression01, string progression02, int score,
            Dictionary<string, object> customFields)
        {
            GameAnalyticsSDK.GameAnalytics.NewProgressionEvent(ToProviderStatus(status), progression01, progression02,
                score, customFields);
        }

        public void TrackResourceFlow(QuackUp.Analytics.ResourceFlowType flowType, string currency, float amount,
            string itemType, string itemId)
        {
            var providerFlow = flowType switch
            {
                QuackUp.Analytics.ResourceFlowType.Source => GAResourceFlowType.Source,
                QuackUp.Analytics.ResourceFlowType.Sink => GAResourceFlowType.Sink,
                _ => throw new ArgumentOutOfRangeException(nameof(flowType), flowType, null)
            };
            GameAnalyticsSDK.GameAnalytics.NewResourceEvent(providerFlow, currency, amount, itemType, itemId);
        }

        public void TrackBusinessEvent(string currency, int amount, string itemType, string itemId, string cartType)
        {
            GameAnalyticsSDK.GameAnalytics.NewBusinessEvent(currency, amount, itemType, itemId, cartType);
        }

        public void SetPlayerTier(string tier)
        {
            GameAnalyticsSDK.GameAnalytics.SetCustomDimension01(tier);
        }

        public void TrackAdEvent(QuackUp.Analytics.AdAction action, QuackUp.Analytics.AdType type,
            string unitId, string placement)
        {
            var customFields = new Dictionary<string, object> { { "AdContext", placement } };
            GameAnalyticsSDK.GameAnalytics.NewAdEvent(ToProviderAction(action), ToProviderAdType(type), "admob", unitId,
                customFields: customFields);
        }

        private static GAProgressionStatus ToProviderStatus(ProgressionStatus status) => status switch
        {
            ProgressionStatus.Start => GAProgressionStatus.Start,
            ProgressionStatus.Complete => GAProgressionStatus.Complete,
            ProgressionStatus.Fail => GAProgressionStatus.Fail,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };

        private static GAAdAction ToProviderAction(QuackUp.Analytics.AdAction action) => action switch
        {
            QuackUp.Analytics.AdAction.Loaded => GAAdAction.Loaded,
            QuackUp.Analytics.AdAction.FailedShow => GAAdAction.FailedShow,
            QuackUp.Analytics.AdAction.Clicked => GAAdAction.Clicked,
            QuackUp.Analytics.AdAction.RewardReceived => GAAdAction.RewardReceived,
            QuackUp.Analytics.AdAction.Show => GAAdAction.Show,
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
        };

        private static GAAdType ToProviderAdType(QuackUp.Analytics.AdType type) => type switch
        {
            QuackUp.Analytics.AdType.Banner => GAAdType.Banner,
            QuackUp.Analytics.AdType.Interstitial => GAAdType.Interstitial,
            QuackUp.Analytics.AdType.RewardedVideo => GAAdType.RewardedVideo,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }
}