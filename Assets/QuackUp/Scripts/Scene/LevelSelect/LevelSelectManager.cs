using System;
using FitMe.Panel;
using R3;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Scene
{
    public class LevelSelectManager : IStartable, IDisposable
    {
        private readonly PanelManager _panelManager;
        
        private IDisposable _subscriptions;
        
        [Inject]
        public LevelSelectManager(
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
            
            _panelManager.TryGetPanel("LevelSelect", out var viewModel);
            if (viewModel is LevelSelectViewModel levelSelectViewModel)
            {
                levelSelectViewModel.LevelSelected
                    .Subscribe(preset =>
                    {
                        LevelManager.GridPreset = preset;
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
