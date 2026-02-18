using System;
using FitMe.Panel;
using R3;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Scene
{
    public class ModeSelectManager : IStartable, IDisposable
    {
        public int GameMode = 0;
        
        private readonly PanelManager _panelManager;
        
        private IDisposable _subscriptions;
        
        [Inject]
        public ModeSelectManager(
            PanelManager panelManager)
        {
            _panelManager = panelManager;
        }
        
        public void Start()
        {
            Subscribe();
        }
        
        private void Subscribe()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            
            _panelManager.TryGetPanel("ModeSelect", out var viewModel);
            if (viewModel is ModeSelectViewModel modeSelectViewModel)
            {
                modeSelectViewModel.ModeSelected
                    .Subscribe(mode =>
                    {
                        GameMode = mode;
                        LevelManager.GameMode = (AllGameMode)GameMode;
                    })
                    .AddTo(ref disposableBuilder);
            }
            
            _subscriptions = disposableBuilder.Build();
        }
        
        public void Dispose()
        {
            _subscriptions?.Dispose();
        }
    }
}
