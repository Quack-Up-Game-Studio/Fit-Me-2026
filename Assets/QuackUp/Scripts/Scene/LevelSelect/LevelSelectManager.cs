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
    public readonly struct ShapeLevelSelectSceneRequest
    {
    }

    public class LevelSelectManager : IStartable, IDisposable
    {
        public const string ShapePanelId = "ShapeLevelSelect";

        private readonly PanelManager _panelManager;
        private readonly IPendingScenePayload _pendingScenePayload;
        private readonly ShapeLevelSelectionService _shapeLevelSelectionService;
        private readonly MessagePackSaveManager _saveManager;
        private readonly EnergyManager _energyManager;
        private readonly LoadSceneManager _loadSceneManager;

        private IDisposable _subscriptions;
        private ShapeLevelSelectViewModel _shapeLevelSelectViewModel;

        [Inject]
        public LevelSelectManager(
            PanelManager panelManager,
            IPendingScenePayload pendingScenePayload,
            ShapeLevelSelectionService shapeLevelSelectionService,
            MessagePackSaveManager saveManager,
            EnergyManager energyManager,
            LoadSceneManager loadSceneManager)
        {
            _panelManager = panelManager;
            _pendingScenePayload = pendingScenePayload;
            _shapeLevelSelectionService = shapeLevelSelectionService;
            _saveManager = saveManager;
            _energyManager = energyManager;
            _loadSceneManager = loadSceneManager;
        }

        public void Start()
        {
            Subscribe();
        }

        private void Subscribe()
        {
            var disposableBuilder = Disposable.CreateBuilder();

            if (_panelManager.TryGetPanel<LevelSelectViewModel>("LevelSelect", out var levelSelectViewModel))
            {
                levelSelectViewModel.LevelSelected
                    .Subscribe(preset =>
                    {
                        _pendingScenePayload.Set(new LevelSessionRequest(GameMode.LevelShape, preset));
                    })
                    .AddTo(ref disposableBuilder);
            }

            var shapePanels = _panelManager.GetPanelsOfType<ShapeLevelSelectViewModel>();
            if (shapePanels.Count == 1)
            {
                _shapeLevelSelectViewModel = shapePanels[0];
                _shapeLevelSelectViewModel.SelectLevelCommand
                    .SubscribeAwait((levelId, _) => OnShapeLevelSelected(levelId), AwaitOperation.Drop)
                    .AddTo(ref disposableBuilder);
                _shapeLevelSelectViewModel.ReturnToModeSelectCommand
                    .SubscribeAwait((_, _) => ReturnToModeSelect(), AwaitOperation.Drop)
                    .AddTo(ref disposableBuilder);
            }
            else if (shapePanels.Count > 1)
            {
                DebugUtils.LogError($"Expected one '{ShapePanelId}' panel, found {shapePanels.Count}.");
            }

            if (_pendingScenePayload.TryTake<ShapeLevelSelectSceneRequest>(out _))
            {
                if (_shapeLevelSelectViewModel == null)
                {
                    DebugUtils.LogError(
                        $"Shape Level Select was requested, but no {nameof(ShapeLevelSelectLifetimeScope)} is registered " +
                        $"under panel key '{ShapePanelId}'. Add the scope and assign its view in the Level Select scene Inspector.");
                }
                else
                {
                    _panelManager.Crossfade("LevelSelect", _shapeLevelSelectViewModel.PanelId, new CrossfadeSettings
                    {
                        crossFadeType = CrossfadeType.None
                    }).Forget();
                }
            }

            _subscriptions = disposableBuilder.Build();
        }

        public void Dispose()
        {
            _subscriptions?.Dispose();
        }

        private async UniTask OnShapeLevelSelected(string levelId)
        {
            var saveObject = _saveManager.GetFirstSaveObjectOfType<PlayerRecordSaveObject>();
            if (!saveObject) return;

            var progress = saveObject.GetSaveData<PlayerRecordSaveData>();
            LevelSessionRequest selectedRequest = default;
            if (!_shapeLevelSelectionService.TryDispatchSelection(
                    levelId,
                    progress,
                    request => selectedRequest = request))
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
