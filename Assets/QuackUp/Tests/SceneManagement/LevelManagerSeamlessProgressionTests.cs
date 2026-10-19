using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using FitMe.GameData;
using FitMe.Grid;
using FitMe.Panel;
using FitMe.Scene;
using FitMe.Shared;
using NUnit.Framework;
using QuackUp.Analytics;
using QuackUp.Audio;
using QuackUp.Save;
using QuackUp.SocialService;
using QuackUp.Utils;
using R3;
using UnityEngine;
using UnityEngine.TestTools;

namespace QuackUp.SceneManagement.Tests
{
    public sealed class LevelManagerSeamlessProgressionTests
    {
        private const string WorldKey = "world-one";
        private readonly List<UnityEngine.Object> _createdObjects = new();
        private readonly List<string> _saveFiles = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var saveFile in _saveFiles)
            {
                if (File.Exists(saveFile)) File.Delete(saveFile);
                var temporaryFile = saveFile + ".tmp";
                if (File.Exists(temporaryFile)) File.Delete(temporaryFile);
            }
            _saveFiles.Clear();
            foreach (var created in _createdObjects)
                if (created) UnityEngine.Object.DestroyImmediate(created);
            _createdObjects.Clear();
        }

        [UnityTest]
        public IEnumerator IntermediateShapePass_LoadsNextLevelWithoutShowingResult()
        {
            var first = CreateLevel();
            var second = CreateLevel();
            var third = CreateLevel();
            var levelCatalog = CreateCatalog(("level-one", first), ("level-two", second), ("level-three", third));
            var worldCatalog = CreateWorldCatalog((WorldKey, levelCatalog));
            var catalogService = new ShapeLevelWorldCatalogService(worldCatalog);
            var saveObject = Keep(ScriptableObject.CreateInstance<PlayerRecordSaveObject>());
            saveObject.Reset();
            saveObject.OnInitialize();
            var saveData = saveObject.GetSaveData<PlayerRecordSaveData>();
            var progressService = new ShapeLevelProgressService(catalogService);
            Assert.That(progressService.TryRecordPass(
                "level-one", WorldKey, first, saveData, out _), Is.True);
            var saveConfig = Keep(MessagePackSaveConfig.CreateForTests(new SaveSettings
            {
                saveLocation = SaveLocation.Custom,
                saveDirectory = Path.GetTempPath(),
                saveFileName = $"fitme-seamless-{Guid.NewGuid():N}"
            }));
            var saveFile = Path.ChangeExtension(saveConfig.CurrentSaveSettings.GetFullSavePath(), ".sav");
            _saveFiles.Add(saveFile);
            SetPrivateField(saveConfig, "initialSaveObjects",
                new Dictionary<string, MessagePackSaveObject> { ["PlayerRecord"] = saveObject });
            var saveManager = new MessagePackSaveManager(saveConfig);

            using var levelHub = new TestMessageHub();
            using var gridHub = new TestMessageHub();
            var gridObject = Keep(new GameObject("Progression test grid"));
            var gridConfig = Keep(ScriptableObject.CreateInstance<GridManagerConfig>());
            var gridManager = new GridManager(gridObject.AddComponent<UnityEngine.Grid>(), gridConfig, null, null, gridHub);
            var panelManager = new PanelManager(new Dictionary<string, PanelLifetimeScope>(), string.Empty);
            var resultPanel = new TransitionProbe(panelManager) { PanelId = "Result" };
            var panels = (Dictionary<string, IPanelViewModel>)typeof(PanelManager)
                .GetField("_panels", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(panelManager);
            panels.Add(resultPanel.PanelId, resultPanel);
            var cameraObject = Keep(new GameObject("Progression test camera"));
            var cameraManager = new OrthographicCameraManager(cameraObject.AddComponent<Camera>());
            var config = Keep(ScriptableObject.CreateInstance<LevelManagerConfig>());
            SetPrivateField(config, "<CameraFitShakeSettings>k__BackingField",
                new PrimeTween.ShakeSettings(Vector3.one, 0.1f, 10f));
            var pendingPayload = new PendingScenePayload();
            var currentRequest = LevelSessionRequest.ForShapeLevel(WorldKey, "level-two", second);
            pendingPayload.SetActive(currentRequest);
            var cloudSaveService = new MockCloudSaveService();
            var manager = new LevelManager(
                config, catalogService, new AudioManagerMock(), levelHub, levelHub, gridManager,
                saveManager, null, panelManager, null, cameraManager, null,
                cloudSaveService, new NoOpAnalyticsService(), pendingPayload, null);
            SetPrivateField(manager, "_gameMode", GameMode.LevelShape);
            SetPrivateField(manager, "_shapeWorldKey", WorldKey);
            SetPrivateField(manager, "_shapeLevelId", "level-two");
            SetPrivateField(manager, "_shapeLevelDefinition", second);
            SetPrivateField(manager, "_playerRecordSaveObject", saveObject);

            try
            {
                typeof(LevelManager).GetMethod("OnFit", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(manager, null);
                for (var frame = 0; frame < 3; frame++) yield return null;

                var hasActiveRequest = pendingPayload.TryGetActive<LevelSessionRequest>(out var activeRequest);
                var advancedWithinWorld = hasActiveRequest &&
                    activeRequest.ShapeWorldKey == WorldKey && activeRequest.ShapeLevelId == "level-three";
                var resultIsHidden = resultPanel.VisibilityState.Value == VisibilityState.Hidden;
                var cloudSaveRequested = cloudSaveService.SaveCalls == 1;
                var localSaveWritten = saveManager.GetZipBytes() is { Length: > 0 };
                var completionNotifications = levelHub.PublishedMessages
                    .OfType<NotificationDisplayEvent>()
                    .Select(x => x.data)
                    .OfType<GeneralNotificationData>()
                    .ToArray();
                var notificationPublished = completionNotifications.Length == 1 &&
                                            completionNotifications[0].message == "Level 2 completed!";
                Assert.That(advancedWithinWorld && resultIsHidden && cloudSaveRequested && localSaveWritten &&
                            notificationPublished, Is.True,
                    $"Expected same-world advance with no Result, local/cloud saves, and completion toast; active={activeRequest.ShapeWorldKey}/{activeRequest.ShapeLevelId}, result={resultPanel.VisibilityState.Value}, cloud saves={cloudSaveService.SaveCalls}, local save={localSaveWritten}, notifications={completionNotifications.Length}.");
            }
            finally
            {
                manager.Dispose();
                resultPanel.Dispose();
                gridManager.Dispose();
                cameraManager.Dispose();
                saveManager.Dispose();
            }
        }

        private ShapeLevelDefinition CreateLevel()
        {
            var board = Keep(ScriptableObject.CreateInstance<GridPreset>());
            board.PresetGridType = GridType.Rectangle;
            board.GridSize = Vector2Int.one;
            var preset = Keep(ScriptableObject.CreateInstance<BlockPreset>());
            preset.BlockSchema.schema = new[,] { { 1 } };
            var definition = Keep(ScriptableObject.CreateInstance<ShapeLevelDefinition>());
            SetPrivateField(definition, "_board", board);
            SetPrivateField(definition, "_playerBlocks", new List<ShapePlayerBlockEntry> { new(preset, 1) });
            return definition;
        }

        private ShapeLevelCatalog CreateCatalog(params (string id, ShapeLevelDefinition level)[] entries)
        {
            var catalog = Keep(ScriptableObject.CreateInstance<ShapeLevelCatalog>());
            var field = typeof(ShapeLevelCatalog).GetField("_entries", BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(catalog, entries.Select(x => new ShapeLevelCatalogEntry(x.id, x.level)).ToList());
            return catalog;
        }

        private ShapeLevelWorldCatalog CreateWorldCatalog(params (string key, ShapeLevelCatalog catalog)[] entries)
        {
            var catalog = Keep(ScriptableObject.CreateInstance<ShapeLevelWorldCatalog>());
            var field = typeof(ShapeLevelWorldCatalog).GetField("_entries", BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(catalog, entries.Select(x => new ShapeLevelWorldEntry(x.key, x.catalog)).ToList());
            return catalog;
        }

        private T Keep<T>(T item) where T : UnityEngine.Object
        {
            _createdObjects.Add(item);
            return item;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Expected field {fieldName}.");
            field.SetValue(target, value);
        }

        private sealed class TransitionProbe : PanelViewModel
        {
            private readonly IDisposable _transitionSubscription;

            public TransitionProbe(PanelManager panelManager) : base(panelManager)
            {
                _transitionSubscription = TransitionInCommand.Subscribe(data => data.Promise.TrySetResult(Unit.Default));
            }

            public override void Dispose()
            {
                _transitionSubscription.Dispose();
                base.Dispose();
            }
        }

        private sealed class TestMessageHub : IMessageHub, IDisposable
        {
            private readonly Dictionary<Type, object> _subjects = new();
            public List<object> PublishedMessages { get; } = new();

            public void Publish<T>(T message)
            {
                PublishedMessages.Add(message);
                if (_subjects.TryGetValue(typeof(T), out var subject))
                    ((Subject<T>)subject).OnNext(message);
            }

            public Observable<T> GetObservable<T>()
            {
                if (!_subjects.TryGetValue(typeof(T), out var subject))
                {
                    subject = new Subject<T>();
                    _subjects.Add(typeof(T), subject);
                }
                return (Observable<T>)subject;
            }

            public IDisposable Subscribe<T>(Action<T> callback) => GetObservable<T>().Subscribe(callback);

            public void Dispose()
            {
                foreach (var subject in _subjects.Values) ((IDisposable)subject).Dispose();
                _subjects.Clear();
            }
        }

        private sealed class NoOpAnalyticsService : IAnalyticsService
        {
            public void TrackProgression(ProgressionStatus status, string progression01) { }
            public void TrackProgression(ProgressionStatus status, string progression01, string progression02) { }
            public void TrackProgression(ProgressionStatus status, string progression01, string progression02, int score,
                Dictionary<string, object> customFields) { }
            public void TrackResourceFlow(ResourceFlowType flowType, string currency, float amount, string itemType, string itemId) { }
            public void TrackBusinessEvent(string currency, int amount, string itemType, string itemId, string cartType) { }
            public void SetPlayerTier(string tier) { }
            public void TrackAdEvent(AdAction action, AdType type, string unitId, string placement) { }
        }

        private sealed class MockCloudSaveService : ICloudSaveService
        {
            public int SaveCalls { get; private set; }
            public Observable<bool> OnSyncResult { get; } = new Subject<bool>();

            public UniTask<bool> SaveToService(SaveToServiceParameters parameters)
            {
                SaveCalls++;
                return UniTask.FromResult(true);
            }

            public UniTask<bool> LoadFromService(LoadFromServiceParameters parameters) => UniTask.FromResult(true);
        }
    }
}
