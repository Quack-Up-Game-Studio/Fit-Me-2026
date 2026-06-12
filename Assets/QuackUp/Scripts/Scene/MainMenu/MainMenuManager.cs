using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using FitMe.GameData;
using FitMe.Grid;
using FitMe.Panel;
using FitMe.Shared;
using GameAnalyticsSDK;
using MessagePipe;
using QuackUp.Audio;
using QuackUp.Save;
using QuackUp.SceneManagement;
using QuackUp.Utils;
using R3;
using Redcode.Extensions;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace FitMe.Scene.MainMenu
{
    public class MainMenuManager : IStartable, IDisposable
    {
        private readonly MainMenuManagerConfig _mainMenuManagerConfig;
        private readonly BlockManagerConfig _blockManagerConfig;
        private readonly GridManager _gridManager;
        private readonly LoadSceneManager _loadSceneManager;
        private readonly MessagePackSaveManager _saveManager;
        private readonly EnergyManager _energyManager;
        private readonly PanelManager _panelManager;
        private readonly AdsService  _adsService;
        private readonly OutOfEnergyManager _outOfEnergyManager;
        private readonly IAudioManager _audioManager;
        private readonly IMessageHub _messageHub;
        
        private IDisposable _subscriptions;
        private IDisposable _panelSubscriptions;
        private AudioReference _bgmReference;
        
        [Inject]
        public MainMenuManager(
            MainMenuManagerConfig mainMenuManagerConfig,
            BlockManagerConfig blockManagerConfig,
            GridManager gridManager,
            LoadSceneManager loadSceneManager,
            MessagePackSaveManager saveManager,
            EnergyManager energyManager,
            PanelManager panelManager,
            AdsService adsService,
            OutOfEnergyManager outOfEnergyManager,
            IAudioManager audioManager,
            [Key(MainMenuManagerMessageHub.MainMenuManagerMessageHubKey)] IMessageHub messageHub)
        {
            _mainMenuManagerConfig = mainMenuManagerConfig;
            _blockManagerConfig = blockManagerConfig;
            _gridManager = gridManager;
            _loadSceneManager = loadSceneManager;
            _saveManager = saveManager;
            _energyManager = energyManager;
            _panelManager = panelManager;
            _adsService = adsService;
            _outOfEnergyManager = outOfEnergyManager;
            _audioManager = audioManager;
            _messageHub = messageHub;
            Subscribe();
        }
        
        private void Subscribe()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            _gridManager.OnAboutToPlaceBlock
                .Subscribe(x =>
                {
                    if (!ShouldCancelPlacement()) return;
                    _outOfEnergyManager.TransitionInCommand.Execute(new Promise<Unit>());
                })
                .AddTo(ref disposableBuilder);
            _gridManager.OnAboutToPlaceBlock
                .Subscribe(x =>
                {
                    if (!ShouldCancelPlacement()) return;
                    x.placeCancellation.Cancel();
                })
                .AddTo(ref disposableBuilder);
            _gridManager.OnBlockPlaced
                .Subscribe(_ => OnBlockPlaced())
                .AddTo(ref disposableBuilder);
            _messageHub.GetObservable<LoadSceneStageEvent>()
                .Where(x => x.Stage is LoadSceneStage.StartOut)
                .Subscribe(_ => OnSceneStartOut())
                .AddTo(ref disposableBuilder);
            _messageHub.GetObservable<LoadSceneStageEvent>()
                .Where(x => x.Stage is LoadSceneStage.FinishIn)
                .Subscribe(_ => OnSceneFinishIn())
                .AddTo(ref disposableBuilder);
            _outOfEnergyManager.OnToShop
                .Subscribe(_ => OnToShop())
                .AddTo(ref disposableBuilder);
            _subscriptions = disposableBuilder.Build();
        }

        public void Start()
        {
            StartAsync().Forget();
        }

        private async UniTaskVoid StartAsync()
        {
            await _saveManager.WaitForSaveDataReady;
            var saveObjects = _saveManager.GetFirstSaveObjectOfType<PlayerRecordSaveObject>();
            var saveData = saveObjects.GetSaveData<PlayerRecordSaveData>();
            saveData.IsFirstTimePlayer = false;
            _saveManager.Save(saveObjects);
            if (!_panelManager.GetFirstPanelOfType<MainMenuPanelViewModel>(out var mainMenuPanel))
            {
                DebugUtils.LogError("MainMenu panel not found!");
            }
            _panelSubscriptions = mainMenuPanel.ToTutorial
                .SubscribeAwait((_, _) => ToTutorial(), AwaitOperation.Switch);
            var randomPreset = _blockManagerConfig.BlockPresetDictionary.Values.GetRandomElement();
            _messageHub.Publish(new SpawnWithBlockPresetEvent(randomPreset, false));
            _bgmReference = _audioManager.PlayAudio(_mainMenuManagerConfig.MainMenuBgm, Vector3.zero);
        }

        public void Dispose()
        {
            _subscriptions?.Dispose();
            _panelSubscriptions?.Dispose();
        }

        private bool ShouldCancelPlacement()
        {
            if (!_saveManager.IsSaveReady) return false;
            var saveObjects = _saveManager.GetFirstSaveObjectOfType<PlayerRecordSaveObject>();
            var saveData = saveObjects.GetSaveData<PlayerRecordSaveData>();
            if (!saveData.CompletedTutorial) return false;
            return !_energyManager.HasEnoughEnergy(1);
        }

        private void OnBlockPlaced()
        {
            //_loadSceneManager.LoadScene(SceneType.ModeSelect, LoadSceneMode.Single, false).Forget();
            var saveObjects = _saveManager.GetFirstSaveObjectOfType<PlayerRecordSaveObject>();
            var saveData = saveObjects.GetSaveData<PlayerRecordSaveData>();
            if (saveData.CompletedTutorial)
            {
                ToGameplay().Forget();
            }
            else
            {
                ToTutorial().Forget();
            }
        }

        private void OnToShop()
        {
            _panelManager.Crossfade("MainMenu", "Shop", new CrossfadeSettings
            {
                crossFadeType = CrossfadeType.InOnly,
            }).Forget();
        }

        private async UniTask ToTutorial()
        {
            LevelManager.GameMode = GameMode.Classic;
            await _loadSceneManager.LoadScene(SceneType.Tutorial, LoadSceneMode.Single, false);
        }

        private async UniTaskVoid ToGameplay()
        {
            _energyManager.ChangeEnergy(-1, itemType: GAItemType.Play, itemId: GAItemId.MainMenuPlay);
            LevelManager.GameMode = GameMode.Classic;
            await _loadSceneManager.LoadScene(SceneType.Gameplay, LoadSceneMode.Single, false);
        }

        private void OnSceneFinishIn()
        {
            if (_saveManager.IsSaveReady && !_energyManager.HasEnoughEnergy(1))
            {
                _outOfEnergyManager.TransitionInCommand.Execute(new Promise<Unit>());
            }
            if (!_adsService.TryGetAdsInstance<BannerAdInstance>(out var bannerAdInstance)) return;
            if (!bannerAdInstance.Enabled) return;
            bannerAdInstance.AdContext = GAAdContext.MainMenuBanner;
            bannerAdInstance.TryShow();
        }

        private void OnSceneStartOut()
        {
            _audioManager.StopAudio(_bgmReference);
            if (!_adsService.TryGetAdsInstance<BannerAdInstance>(out var bannerAdInstance)) return;
            if (!bannerAdInstance.Enabled) return;
            bannerAdInstance.DestroyView();
        }   
    }
}
