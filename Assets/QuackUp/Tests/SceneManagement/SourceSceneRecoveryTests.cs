using System;
using System.Collections.Generic;
using System.Reflection;
using FitMe.GameData;
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
            var ads = new AdsService(null, null, null);
            var banner = new Banner();
            ((Dictionary<Type, AdsInstance>)typeof(AdsService).GetField("_adsInstances",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ads))[typeof(BannerAdInstance)] = banner;
            var outOfEnergy = new OutOfEnergyManager(null, ads, null, null, TimeSpan.Zero, 0);
            var popups = 0;
            using var popupSubscription = outOfEnergy.TransitionInCommand.Subscribe(_ => popups++);
            object consumer = level
                ? new LevelManager(levelConfig, null, new AudioManagerMock(), hub, hub, grid, null, null, null, ads, null, null, null, null, new PendingScenePayload(), null)
                : new MainMenuManager(mainConfig, null, grid, null, null, null, null, ads, outOfEnergy, new AudioManagerMock(), new PendingScenePayload(), hub);
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
        [Test]
        public void LevelShapeMode_DoesNotFallBackToLegacyGridPresetForGeneratedDefinition()
        {
            using var hub = new Hub();
            var gridObject = new GameObject("Generated level selection grid");
            var gridConfig = ScriptableObject.CreateInstance<GridManagerConfig>();
            var levelConfig = ScriptableObject.CreateInstance<LevelManagerConfig>();
            var legacyDatabase = ScriptableObject.CreateInstance<LevelDatabase>();
            var legacyPreset = ScriptableObject.CreateInstance<GridPreset>();
            var generatedLevel = ScriptableObject.CreateInstance<ShapeLevelDefinition>();
            legacyDatabase.LevelPresets = new List<GridPreset> { legacyPreset };
            typeof(LevelManagerConfig).GetProperty(nameof(LevelManagerConfig.ShapeLevelDatabase))
                .SetValue(levelConfig, legacyDatabase);
            generatedLevel.SetGeneratedContent(
                new[,] { { 1 } },
                new[,] { { 0 } },
                Array.Empty<ShapePlayerBlockEntry>());

            var grid = new GridManager(gridObject.AddComponent<UnityEngine.Grid>(), gridConfig, null, null, hub);
            var ads = new AdsService(null, null, null);
            var manager = new LevelManager(
                levelConfig,
                null,
                new AudioManagerMock(),
                hub,
                hub,
                grid,
                null,
                null,
                null,
                ads,
                null,
                null,
                null,
                null,
                new PendingScenePayload(),
                null);
            try
            {
                typeof(LevelManager).GetField("_shapeLevelDefinition", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(manager, generatedLevel);
                typeof(LevelManager).GetMethod("LevelShapeMode", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(manager, null);

                var selectedGrid = typeof(LevelManager).GetField("_gridPreset",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
                Assert.That(selectedGrid, Is.Null,
                    "A generated Shape Level owns its inline board and must not substitute a legacy GridPreset.");
            }
            finally
            {
                manager.Dispose();
                grid.Dispose();
                ads.Dispose();
                UnityEngine.Object.DestroyImmediate(generatedLevel);
                UnityEngine.Object.DestroyImmediate(legacyPreset);
                UnityEngine.Object.DestroyImmediate(legacyDatabase);
                UnityEngine.Object.DestroyImmediate(levelConfig);
                UnityEngine.Object.DestroyImmediate(gridConfig);
                UnityEngine.Object.DestroyImmediate(gridObject);
            }
        }

        private static object Bgm(object consumer) => consumer.GetType().GetField("_bgmReference",
             BindingFlags.Instance | BindingFlags.NonPublic).GetValue(consumer);
        private sealed class Banner : BannerAdInstance
        {
            public int Shows;
            public Banner() : base(null, null, null) { }
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
