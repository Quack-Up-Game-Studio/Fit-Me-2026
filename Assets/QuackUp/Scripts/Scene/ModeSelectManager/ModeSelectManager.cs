using System;
using Cysharp.Threading.Tasks;
using FitMe.GameData;
using FitMe.Panel;
using FitMe.Shared;
using MessagePipe;
using QuackUp.SceneManagement;
using QuackUp.Utils;
using R3;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace FitMe.Scene
{
    public class ModeSelectManager : IStartable, IDisposable
    {
        private readonly PanelManager _panelManager;
        private readonly LoadSceneManager _loadSceneManager;
        private readonly EnergyManager _energyManager;
        
        private IDisposable _subscriptions;
        
        [Inject]
        public ModeSelectManager(
            PanelManager panelManager,
            LoadSceneManager loadSceneManager,
            EnergyManager energyManager)

        {
            _panelManager = panelManager;
            _loadSceneManager = loadSceneManager;
            _energyManager = energyManager;
        }
        
        public void Start()
        {
            Subscribe();
        }
        
        private void Subscribe()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            _panelManager.TryGetPanel<ModeSelectViewModel>("ModeSelect", out var modeSelectViewModel);
            modeSelectViewModel.SelectGameModeCommand
                .SubscribeAwait((x, _) => OnModeSelected(x), AwaitOperation.Drop)
                .AddTo(ref disposableBuilder);
            _subscriptions = disposableBuilder.Build();
        }
        
        public void Dispose()
        {
            _subscriptions?.Dispose();
        }
        
        private async UniTask OnModeSelected(GameMode mode)
        {
            if (!_energyManager.HasEnoughEnergy(1))
            {
                await _energyManager.ShowNotEnoughEnergyNotification();
                return;
            }
            _energyManager.ChangeEnergy(-1);
            LevelManager.GameMode = mode;
            await _loadSceneManager.LoadScene(SceneType.Gameplay, LoadSceneMode.Single, false);
        }
    }
}
