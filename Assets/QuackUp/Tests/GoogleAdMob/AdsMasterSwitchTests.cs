using System;
using System.Reflection;
using NUnit.Framework;
using QuackUp.GoogleAdMob;
using UnityEngine;

namespace QuackUp.GoogleAdMob.Tests
{
    public sealed class AdsMasterSwitchTests
    {
        [Test]
        public void DisabledSettingsCannotBeOverriddenByRuntimeEnableRequests()
        {
            var settings = ScriptableObject.CreateInstance<AdsSettings>();
            JsonUtility.FromJsonOverwrite("{\"adsEnabled\":false}", settings);
            using var service = new AdsService(settings, null, null);
            try
            {
                Assert.That(service.AdsEnabled.CurrentValue, Is.False);
                service.SetEnableStateAll(true);
                Assert.That(service.AdsEnabled.CurrentValue, Is.False);
                service.PostInitialize();
                Assert.That(typeof(AdsService).GetField("_sdkInitializationStarted", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(service), Is.False);
                Assert.That(service.TryGetAdsInstance<BannerAdInstance>(out _), Is.False);
                Assert.That(service.TryGetAdsInstance<RewardedAdInstance>(out _), Is.False);
                Assert.That(service.TryGetAdsInstance<InterstitialAdInstance>(out _), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(settings); }
        }

        [Test]
        public void DisabledSettingsBlockEveryAdTypeEvenWithDirectEnableAndLoadCalls()
        {
            var settings = ScriptableObject.CreateInstance<AdsSettings>();
            settings.DisableAds();
            var banner = new BannerAdInstance(settings, null, null);
            var rewarded = new RewardedAdInstance(settings, null, null);
            var interstitial = new InterstitialAdInstance(settings, null, null);
            var instances = new AdsInstance[] { banner, rewarded, interstitial };
            try
            {
                foreach (var instance in instances)
                {
                    instance.Enabled = true;
                    Assert.That(instance.Enabled, Is.False);
                    Assert.DoesNotThrow(instance.Load, "Disabled ads must not reach SDK or revenue bridge.");
                    Assert.That(instance.CanShowAd(), Is.False);
                    Assert.That(instance.TryShow(), Is.False);
                }
                var rewards = 0;
                Assert.That(rewarded.TryShow("Disabled", () => rewards++, out var attempt), Is.False);
                Assert.That(attempt, Is.Null);
                Assert.That(rewards, Is.Zero);
            }
            finally
            {
                foreach (var instance in instances) instance.Dispose();
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void MasterToggleUpdatesServiceButDoesNotOverrideRuntimeNoAdsState()
        {
            var settings = ScriptableObject.CreateInstance<AdsSettings>();
            using var service = new AdsService(settings, null, null);
            try
            {
                Assert.That(settings.AdsEnabled, Is.True);
                Assert.That(service.AdsEnabled.CurrentValue, Is.True);
                settings.DisableAds();
                Assert.That(service.AdsEnabled.CurrentValue, Is.False);
                settings.EnableAds();
                Assert.That(service.AdsEnabled.CurrentValue, Is.True);
                service.SetEnableStateAll(false);
                settings.DisableAds();
                settings.EnableAds();
                Assert.That(service.AdsEnabled.CurrentValue, Is.False);
                service.SetEnableStateAll(true);
                Assert.That(service.AdsEnabled.CurrentValue, Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(settings); }
        }

        [Test]
        public void DisablingInvalidatesPendingRewardedLoadsAndCancelsRetryTimers()
        {
            var settings = ScriptableObject.CreateInstance<AdsSettings>();
            using var instance = new LoadProbe(settings);
            try
            {
                var generation = instance.BeginLoad();
                Assert.That(instance.Accepts(generation), Is.True);
                instance.ScheduleRetry();
                settings.DisableAds();
                Assert.That(instance.Accepts(generation), Is.False);
                Assert.That(typeof(AdsInstance).GetField("_loadRetryCts", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(instance), Is.Null);
                settings.EnableAds();
                Assert.That(instance.Accepts(generation), Is.False, "Old callback must remain invalid after re-enabling.");
                Assert.That(instance.Accepts(instance.BeginLoad()), Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(settings); }
        }

        [Test]
        public void LateSdkInitializationCannotCreateAdsAfterMasterSwitchWasDisabled()
        {
            var settings = ScriptableObject.CreateInstance<AdsSettings>();
            using var service = new AdsService(settings, null, null);
            try
            {
                settings.DisableAds();
                typeof(AdsService).GetMethod("OnSdkInitialized", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(service, null);
                Assert.That(service.TryGetAdsInstance<BannerAdInstance>(out _), Is.False);
                Assert.That(service.TryGetAdsInstance<RewardedAdInstance>(out _), Is.False);
                Assert.That(service.TryGetAdsInstance<InterstitialAdInstance>(out _), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(settings); }
        }

        [Test]
        public void DisposedConsumersUnsubscribeFromSettingsChanges()
        {
            var settings = ScriptableObject.CreateInstance<AdsSettings>();
            var service = new AdsService(settings, null, null);
            var instance = new RewardedAdInstance(settings, null, null);
            service.Dispose();
            instance.Dispose();
            try
            {
                Assert.DoesNotThrow(settings.DisableAds);
                Assert.DoesNotThrow(settings.EnableAds);
                Assert.DoesNotThrow(service.PostInitialize);
            }
            finally { UnityEngine.Object.DestroyImmediate(settings); }
        }

        private sealed class LoadProbe : RewardedAdInstance
        {
            public LoadProbe(AdsSettings settings) : base(settings, null, null) { }
            public int BeginLoad() => BeginLoadGeneration();
            public bool Accepts(int generation) => CanAcceptLoadedAd(generation);
            public void ScheduleRetry() => ScheduleLoadRetry(() => { });
        }
    }
}
