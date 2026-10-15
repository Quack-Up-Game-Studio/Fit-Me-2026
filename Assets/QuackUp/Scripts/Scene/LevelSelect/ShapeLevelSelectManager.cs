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

        private readonly PanelManager _panelManager;
        private readonly ShapeLevelSelectionService _selectionService;
        private readonly MessagePackSaveManager _saveManager;
        private readonly EnergyManager _energyManager;
        private readonly IPendingScenePayload _pendingScenePayload;
        private readonly LoadSceneManager _loadSceneManager;

        private IDisposable _subscriptions;

        [Inject]
        public ShapeLevelSelectManager(
            PanelManager panelManager,
            ShapeLevelSelectionService selectionService,
            MessagePackSaveManager saveManager,
            EnergyManager energyManager,
            IPendingScenePayload pendingScenePayload,
            LoadSceneManager loadSceneManager)
        {
            _panelManager = panelManager;
            _selectionService = selectionService;
            _saveManager = saveManager;
            _energyManager = energyManager;
            _pendingScenePayload = pendingScenePayload;
            _loadSceneManager = loadSceneManager;
        }

        public void Start()
        {
            Subscribe();
            PopulateItems().Forget();
        }

        private void Subscribe()
        {
            
            if (!_panelManager.TryGetPanel<ShapeLevelSelectViewModel>(ShapePanelId, out var viewModel))
            {
                DebugUtils.LogError($"Shape Level Select scene requires a '{ShapePanelId}' startup panel.");
                return;
            }
            var disposableBuilder = Disposable.CreateBuilder();
            viewModel.SelectLevelCommand
                .SubscribeAwait((levelId, _) => SelectLevel(levelId), AwaitOperation.Drop)
                .AddTo(ref disposableBuilder);
            viewModel.ReturnToModeSelectCommand
                .SubscribeAwait((_, _) => ReturnToModeSelect(), AwaitOperation.Drop)
                .AddTo(ref disposableBuilder);
            _subscriptions = disposableBuilder.Build();
        }

        private async UniTaskVoid PopulateItems()
        {
            await _saveManager.WaitForSaveDataReady;
            if (!_panelManager.TryGetPanel<ShapeLevelSelectViewModel>(ShapePanelId, out var viewModel))
            {
                DebugUtils.LogError($"Shape Level Select scene requires a '{ShapePanelId}' startup panel.");
                return;
            }
            var saveObject = _saveManager.GetFirstSaveObjectOfType<PlayerRecordSaveObject>();
            var highestUnlockedOrder = saveObject
                ? saveObject.GetSaveData<PlayerRecordSaveData>().HighestUnlockedShapeOrder
                : 1;
            viewModel.InitializeItems(highestUnlockedOrder);
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
            if (!_selectionService.TryDispatchSelection(levelId, progress, request => selectedRequest = request))
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
