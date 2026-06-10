using System;
using System.Collections.Generic;

namespace FitMe.Shared
{
    public static class GACurrency
    {
        public const string Energy = "Energy";
    }
    
    public static class GAItemType
    {
        public const string IAP = "IAP";
        public const string Play = "Play";
        public const string Recharge = "Recharge";
        public const string Ads = "Ads";
    }
    
    public static class GAItemId
    {
        /* For IAP, just use ProductID as GAItemID */
        
        /* Play */
        [Obsolete("This is legacy as the mode selection is no longer available")]
        public const string ModeSelectPlay = "ModeSelectPlay";
        public const string MainMenuPlay = "MainMenuPlay";
        public const string GameplayRestart = "GameplayRestart";
        
        /* Recharge */
        public const string PassiveRefill = "PassiveRefill";
        
        /* Ads */
        [Obsolete("This is legacy as the energy bar ads is no longer available")]
        public const string EnergyBarAds = "EnergyBarAds";
        public const string OutOfEnergyAds = "OutOfEnergyAds";
    }

    public static class GAProgression01
    {
        public const string Tutorial = "Tutorial";
        public const string Classic = "Classic";
        public const string LevelShape = "LevelShape";
    }

    public static class GACustomDimension01
    {
        public const string F2P = "F2P"; //For user who hasn't spent any money.
        public const string Spender = "Spender"; //For user who has spent money but not on subscription.
        public const string Premium = "Premium"; //For user who has spent money on subscription.
    }

    public static class GACartType
    {
        public const string Store = "Store";
    }
}