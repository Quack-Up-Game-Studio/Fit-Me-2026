using System;
using FitMe.Shared;
using QuackUp.Utils;
using R3;
using VContainer;

namespace FitMe.Panel
{
    public class GameplayPanelViewModel : PanelViewModel
    {
        public ReactiveCommand PauseCommand { get; } = new();
        
        private readonly IGameStateManager _gameStateManager;
        private IDisposable _bindings;
        
        [Inject]
        public GameplayPanelViewModel(
            PanelManager panelManager,
            IGameStateManager gameStateManager) : base(panelManager)
        {
            _gameStateManager = gameStateManager;
            Bind();
        }

        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            PauseCommand
                .Subscribe(_ => OnPause())
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }
        
        public override void Dispose()
        {
            base.Dispose();
            _bindings?.Dispose();
        }

        private void OnPause()
        {
            _gameStateManager.SetPause(true);
        }
    }
}