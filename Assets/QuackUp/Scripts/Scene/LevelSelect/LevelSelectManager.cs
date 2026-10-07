using System;
using FitMe.Panel;
using FitMe.Shared;
using QuackUp.SceneManagement;
using R3;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Scene
{
    public class LevelSelectManager : IStartable, IDisposable
    {
        private readonly PanelManager _panelManager;
        private readonly IPendingScenePayload _pendingScenePayload;
        
        private IDisposable _subscriptions;
        
        [Inject]
        public LevelSelectManager(
            PanelManager panelManager,
            IPendingScenePayload pendingScenePayload)
        {
            _panelManager = panelManager;
            _pendingScenePayload = pendingScenePayload;
        }
        
        public void Start()
        {
            Subscribe();
        }
        
        private void Subscribe()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            
            _panelManager.TryGetPanel<LevelSelectViewModel>("LevelSelect", out var levelSelectViewModel);
            levelSelectViewModel.LevelSelected
                .Subscribe(preset =>
                {
                    _pendingScenePayload.Set(new LevelSessionRequest(GameMode.LevelShape, preset));
                })
                .AddTo(ref disposableBuilder);
            _subscriptions = disposableBuilder.Build();
        }
        
        public void Dispose()
        {
            _subscriptions?.Dispose();
        }
    }
}
