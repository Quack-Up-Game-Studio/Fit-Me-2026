using System;
using Cysharp.Threading.Tasks;
using FitMe.GameData;
using FitMe.Panel;
using FitMe.Shared;
using QuackUp.Save;
using QuackUp.SceneManagement;
using QuackUp.Utils;
using R3;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace FitMe.Scene
{
    public sealed class ShapeLevelSelectManager : IStartable, IDisposable
    {
        public const string ShapePanelId = "ShapeLevelSelect";
        public const string WorldPanelId = "ShapeWorldSelect";

        private readonly PanelManager _panelManager;
        private readonly ShapeLevelSelectionService _selectionService;
        private readonly MessagePackSaveManager _saveManager;
        private readonly EnergyManager _energyManager;
        private readonly IPendingScenePayload _pendingScenePayload;
        private readonly LoadSceneManager _loadSceneManager;
        private readonly ShapeLevelWorldCatalogService _catalogService;
        private string _selectedWorldKey;

        private IDisposable _subscriptions;

        [Inject]
        public ShapeLevelSelectManager(
            PanelManager panelManager,
            ShapeLevelSelectionService selectionService,
            MessagePackSaveManager saveManager,
            EnergyManager energyManager,
            IPendingScenePayload pendingScenePayload,
            LoadSceneManager loadSceneManager,
            ShapeLevelWorldCatalogService catalogService)
        {
            _panelManager = panelManager;
            _selectionService = selectionService;
            _saveManager = saveManager;
            _energyManager = energyManager;
            _pendingScenePayload = pendingScenePayload;
            _loadSceneManager = loadSceneManager;
            _catalogService = catalogService;
        }

        public void Start()
        {
            Subscribe().Forget();
        }

        private async UniTaskVoid Subscribe()
        {
            await _saveManager.WaitForSaveDataReady;
            if (!_panelManager.TryGetPanel<ShapeLevelSelectViewModel>(ShapePanelId, out var viewModel) ||
                !_panelManager.TryGetPanel<ShapeWorldSelectViewModel>(WorldPanelId, out var worldViewModel))
            {
                DebugUtils.LogError("Shape Level Select scene requires both world and level selection panels.");
                return;
            }
            var saveObject = _saveManager.GetFirstSaveObjectOfType<PlayerRecordSaveObject>();
            var progress = saveObject ? saveObject.GetSaveData<PlayerRecordSaveData>() : new PlayerRecordSaveData();
            progress.ShapeWorldProgress ??= new System.Collections.Generic.Dictionary<string, ShapeWorldProgressData>();
            ShapeWorldProgressRules.InitializeUnlocks(_catalogService, progress.ShapeWorldProgress);
            _selectedWorldKey = ConsumeReturnWorldKey();
            SubscribeToPanelCommands(viewModel, worldViewModel);
            if (saveObject) saveObject.GetSaveData<PlayerRecordSaveData>().ShapeWorldProgress = progress.ShapeWorldProgress;
            worldViewModel.InitializeItems(progress);
            if (!string.IsNullOrWhiteSpace(_selectedWorldKey) && _catalogService.TryGetWorld(_selectedWorldKey, out _))
            {
                viewModel.InitializeItems(_selectedWorldKey, progress.ShapeWorldProgress);
                await FadeOutTo(ShapePanelId, WorldPanelId);
            }
            else
                await _panelManager.Crossfade(null, WorldPanelId, new CrossfadeSettings { crossFadeType = CrossfadeType.InOnly });
        }

        private void SubscribeToPanelCommands(ShapeLevelSelectViewModel viewModel, ShapeWorldSelectViewModel worldViewModel)
        {
            var disposableBuilder = Disposable.CreateBuilder();
            viewModel.SelectLevelCommand
                .SubscribeAwait((levelId, _) => SelectLevel(levelId), AwaitOperation.Drop)
                .AddTo(ref disposableBuilder);
            viewModel.BackToWorldsCommand.SubscribeAwait((_, _) => BackToWorlds(), AwaitOperation.Drop).AddTo(ref disposableBuilder);
            worldViewModel.SelectWorldCommand.SubscribeAwait((key, _) => SelectWorld(key), AwaitOperation.Drop).AddTo(ref disposableBuilder);
            worldViewModel.ReturnToModeSelectCommand.SubscribeAwait((_, _) => ReturnToModeSelect(), AwaitOperation.Drop).AddTo(ref disposableBuilder);
            _subscriptions = disposableBuilder.Build();
        }

        private string ConsumeReturnWorldKey()
        {
            if (!_pendingScenePayload.TryTakeActive<LevelSessionRequest>(out var request) || string.IsNullOrWhiteSpace(request.ShapeWorldKey)) return null;
            return request.ShapeWorldKey;
        }

        private async UniTask SelectWorld(string worldKey)
        {
            if (!_catalogService.TryGetWorld(worldKey, out _)) return;
            var saveObject = _saveManager.GetFirstSaveObjectOfType<PlayerRecordSaveObject>();
            var progress = saveObject ? saveObject.GetSaveData<PlayerRecordSaveData>() : null;
            if (progress == null) return;
            progress.ShapeWorldProgress ??= new System.Collections.Generic.Dictionary<string, ShapeWorldProgressData>();
            ShapeWorldProgressRules.InitializeUnlocks(_catalogService, progress.ShapeWorldProgress);
            if (!progress.ShapeWorldProgress.TryGetValue(worldKey, out var state) || !state.IsUnlocked) return;
            if (!_panelManager.TryGetPanel<ShapeLevelSelectViewModel>(ShapePanelId, out var viewModel)) return;
            _selectedWorldKey = worldKey;
            viewModel.InitializeItems(worldKey, progress.ShapeWorldProgress);
            await FadeOutTo(ShapePanelId, WorldPanelId);
        }

        private async UniTask BackToWorlds()
        {
            await FadeOutTo(WorldPanelId, ShapePanelId);
        }

        private async UniTask FadeOutTo(string targetPanelId, string outgoingPanelId)
        {
            await _panelManager.Crossfade(null, targetPanelId, new CrossfadeSettings
            {
                crossFadeType = CrossfadeType.InOnly
            });
            await _panelManager.Crossfade(outgoingPanelId, targetPanelId, new CrossfadeSettings
            {
                crossFadeType = CrossfadeType.OutOnly
            });
        }

        public void Dispose()
        {
            _subscriptions?.Dispose();
        }

        private async UniTask SelectLevel(string levelId)
        {
            var saveObject = _saveManager.GetFirstSaveObjectOfType<PlayerRecordSaveObject>();
            if (!saveObject) return;

            var progress = saveObject.GetSaveData<PlayerRecordSaveData>();
            LevelSessionRequest selectedRequest = default;
            if (!_selectionService.TryDispatchSelection(_selectedWorldKey, levelId, progress, request => selectedRequest = request))
                return;

            if (!_energyManager.HasEnoughEnergy(1))
            {
                await _energyManager.ShowNotEnoughEnergyNotification();
                return;
            }

            _energyManager.ChangeEnergy(-1, itemType: GAItemType.Play, itemId: GAItemId.ModeSelectPlay);
            _pendingScenePayload.Set(selectedRequest);
            await _loadSceneManager.LoadScene(SceneType.Gameplay, LoadSceneMode.Single, false);
        }

        private async UniTask ReturnToModeSelect()
        {
            await _loadSceneManager.LoadScene(SceneType.ModeSelect, LoadSceneMode.Single, false);
        }
    }
}
