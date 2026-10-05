using System;
using System.Collections.Generic;
using System.Reflection;
using FitMe.Grid;
using FitMe.Panel;
using FitMe.Scene;
using FitMe.Shared;
using FitMe.Scene.MainMenu;
using NUnit.Framework;
using QuackUp.Audio;
using QuackUp.GoogleAdMob;
using QuackUp.Utils;
using R3;
using UnityEngine;

namespace QuackUp.SceneManagement.Tests
{
    public sealed class SourceSceneRecoveryTests
    {
        [TestCase(false, SceneType.MainMenu)]
        [TestCase(true, SceneType.Gameplay)]
        [TestCase(true, SceneType.Tutorial)]
        public void CancellationRecovery_MatchesOnlySurvivingSourceWithoutInitialization(bool level, SceneType source)
        {
            using var hub = new Hub();
            var go = new GameObject("Recovery grid");
            var gridConfig = ScriptableObject.CreateInstance<GridManagerConfig>();
            var mainConfig = ScriptableObject.CreateInstance<MainMenuManagerConfig>();
            var levelConfig = ScriptableObject.CreateInstance<LevelManagerConfig>();
            var grid = new GridManager(go.AddComponent<UnityEngine.Grid>(), gridConfig, null, null, hub);
            var ads = new AdsService(null);
            var banner = new Banner();
            ((Dictionary<Type, AdsInstance>)typeof(AdsService).GetField("_adsInstances",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ads))[typeof(BannerAdInstance)] = banner;
            var outOfEnergy = new OutOfEnergyManager(null, ads, null, null, TimeSpan.Zero, 0);
            var popups = 0;
            using var popupSubscription = outOfEnergy.TransitionInCommand.Subscribe(_ => popups++);
            object consumer = level
                ? new LevelManager(levelConfig, new AudioManagerMock(), hub, grid, null, null, null, ads, null, null, null)
                : new MainMenuManager(mainConfig, null, grid, null, null, null, null, ads, outOfEnergy, new AudioManagerMock(), hub);
            if (consumer is LevelManager manager) manager.IsTutorial = source == SceneType.Tutorial;
            try
            {
                hub.Publish(new LoadSceneStageEvent(LoadSceneStage.CancelledBeforeLoad, SceneType.ModeSelect, SceneType.ModeSelect));
                Assert.That(Bgm(consumer), Is.Null);
                Assert.That(banner.Shows, Is.Zero);
                hub.Publish(new LoadSceneStageEvent(LoadSceneStage.CancelledBeforeLoad, source, source));
                Assert.That(Bgm(consumer), Is.Not.Null, "Surviving source must replay its own BGM.");
                Assert.That(banner.Shows, Is.EqualTo(1));
                Assert.That(banner.AllowShow, Is.True);
                Assert.That(banner.AdContext, Is.EqualTo(source == SceneType.MainMenu ? GAAdContext.MainMenuBanner
                    : source == SceneType.Tutorial ? GAAdContext.TutorialBanner : GAAdContext.GameplayBanner));
                Assert.That(popups, Is.Zero, "Recovery is not FinishIn and must not open out-of-energy UI.");
                Assert.That(hub.OtherPublications, Is.Zero, "Do not rerun grid/tutorial initialization.");
                ((IDisposable)consumer).Dispose();
                hub.Publish(new LoadSceneStageEvent(LoadSceneStage.CancelledBeforeLoad, source, source));
                Assert.That(banner.Shows, Is.EqualTo(1), "Recovery subscription must be owned.");
            }
            finally
            {
                ((IDisposable)consumer).Dispose(); grid.Dispose(); ads.Dispose(); outOfEnergy.Dispose();
                UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(gridConfig);
                UnityEngine.Object.DestroyImmediate(mainConfig); UnityEngine.Object.DestroyImmediate(levelConfig);
            }
        }
        private static object Bgm(object consumer) => consumer.GetType().GetField("_bgmReference",
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(consumer);
        private sealed class Banner : BannerAdInstance
        {
            public int Shows;
            public Banner() : base(null) { }
            public override bool TryShow() { Shows++; return true; }
            public override void Load() { }
        }
        private sealed class Hub : IMessageHub, IDisposable
        {
            private readonly Subject<LoadSceneStageEvent> _stages = new Subject<LoadSceneStageEvent>();
            private readonly List<IDisposable> _other = new List<IDisposable>();
            public int OtherPublications;
            public void Publish<T>(T message)
            {
                if (message is LoadSceneStageEvent stage) _stages.OnNext(stage);
                else OtherPublications++;
            }
            public Observable<T> GetObservable<T>()
            {
                if (typeof(T) == typeof(LoadSceneStageEvent)) return (Observable<T>)(object)_stages;
                var subject = new Subject<T>(); _other.Add(subject); return subject;
            }
            public IDisposable Subscribe<T>(Action<T> callback) => GetObservable<T>().Subscribe(callback);
            public void Dispose() { _stages.Dispose(); foreach (var item in _other) item.Dispose(); }
        }
    }
}
