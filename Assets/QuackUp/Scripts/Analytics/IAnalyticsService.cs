using System.Collections.Generic;

namespace QuackUp.Analytics
{
    public enum ProgressionStatus
    {
        Start,
        Complete,
        Fail
    }

    public enum ResourceFlowType
    {
        Source,
        Sink
    }

    public enum AdAction
    {
        Loaded,
        FailedShow,
        Clicked,
        RewardReceived,
        Show
    }

    public enum AdType
    {
        Banner,
        Interstitial,
        RewardedVideo
    }

    /// <summary>
    /// Provider-neutral analytics operations used by game and service code.
    /// </summary>
    public interface IAnalyticsService
    {
        void TrackProgression(ProgressionStatus status, string progression01);

        void TrackProgression(ProgressionStatus status, string progression01, string progression02);

        void TrackProgression(ProgressionStatus status, string progression01, string progression02, int score,
            Dictionary<string, object> customFields);

        void TrackResourceFlow(ResourceFlowType flowType, string currency, float amount, string itemType, string itemId);

        void TrackBusinessEvent(string currency, int amount, string itemType, string itemId, string cartType);

        void SetPlayerTier(string tier);

        void TrackAdEvent(AdAction action, AdType type, string unitId, string placement);

    }
}