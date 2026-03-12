using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using FitMe.GameData;
using FitMe.Grid;
using FitMe.Panel;
using FitMe.Shared;
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
        private readonly IAudioManager _audioManager;
        private readonly IMessageHub _messageHub;
        private readonly IPublisher<NotificationDisplayEvent> _notificationDisplayEventPublisher;
        
        private IDisposable _subscriptions;
        private AudioReference _bgmReference;
        
        [Inject]
        public MainMenuManager(
            MainMenuManagerConfig mainMenuManagerConfig,
            BlockManagerConfig blockManagerConfig,
            GridManager gridManager,
            LoadSceneManager loadSceneManager,
            MessagePackSaveManager saveManager,
            EnergyManager energyManager,
            IAudioManager audioManager,
            [Key(MainMenuManagerMessageHub.MainMenuManagerMessageHubKey)] IMessageHub messageHub,
            IPublisher<NotificationDisplayEvent> notificationDisplayEventPublisher)
        {
            _mainMenuManagerConfig = mainMenuManagerConfig;
            _blockManagerConfig = blockManagerConfig;
            _gridManager = gridManager;
            _loadSceneManager = loadSceneManager;
            _saveManager = saveManager;
            _energyManager = energyManager;
            _audioManager = audioManager;
            _messageHub = messageHub;
            _notificationDisplayEventPublisher = notificationDisplayEventPublisher;
            Subscribe();
        }
        
        private void Subscribe()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            _gridManager.OnAboutToPlaceBlock
                .SubscribeAwait((x, _) => OnAboutToPlaceBlock(x.cancellation), AwaitOperation.Switch)
                .AddTo(ref disposableBuilder);
            _gridManager.OnBlockPlaced
                .Subscribe(_ => OnBlockPlaced())
                .AddTo(ref disposableBuilder);
            _messageHub.GetObservable<LoadSceneStageEvent>()
                .Where(x => x.Stage is LoadSceneStage.StartOut)
                .Subscribe(_ => OnSceneStartOut())
                .AddTo(ref disposableBuilder);
            _subscriptions = disposableBuilder.Build();
        }
        
        public void Start()
        {
            var saveObjects = _saveManager.GetFirstSaveObjectOfType<PlayerRecordSaveObject>();
            var saveData = saveObjects.GetSaveData<PlayerRecordSaveData>();
            saveData.IsFirstTimePlayer = false;
            _saveManager.Save(saveObjects);
            
            var randomPreset = _blockManagerConfig.BlockPresetDictionary.Values.GetRandomElement();
            _messageHub.Publish(new SpawnWithBlockPresetEvent(randomPreset, false));
            _bgmReference = _audioManager.PlayAudio(_mainMenuManagerConfig.MainMenuBgm, Vector3.zero);
        }

        public void Dispose()
        {
            _subscriptions?.Dispose();
        }
        
        private async UniTask OnAboutToPlaceBlock(CancellationTokenSource cancellationTokenSource)
        {
            if (_energyManager.CurrentEnergy.CurrentValue < 1)
            {
                cancellationTokenSource.Cancel();
                var promise = new Promise<Unit>();
                _notificationDisplayEventPublisher.Publish(new NotificationDisplayEvent(
                    NotificationType.General, 
                    new GeneralNotificationData 
                    { 
                        message = "Not enough energy!"
                    },
                    promise));
                await promise.Task;
            }
        }

        private void OnBlockPlaced()
        {
            //_loadSceneManager.LoadScene(SceneType.ModeSelect, LoadSceneMode.Single, false).Forget();
            ToGameplay().Forget();
        }

        private async UniTaskVoid ToGameplay()
        {
            _energyManager.ChangeEnergy(-1);
            LevelManager.GameMode = GameMode.Classic;
            await _loadSceneManager.LoadScene(SceneType.Gameplay, LoadSceneMode.Single, false);
        }

        private void OnSceneStartOut()
        {
            _audioManager.StopAudio(_bgmReference);
        }   
    }
}