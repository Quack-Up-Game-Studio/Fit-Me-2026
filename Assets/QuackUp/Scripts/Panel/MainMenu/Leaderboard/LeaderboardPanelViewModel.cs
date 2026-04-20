using System;
using FitMe.Shared;
using R3;

namespace FitMe.Panel
{
    public class LeaderboardPanelViewModel : PanelViewModel
    {
        // public ReactiveCommand<GameMode> ChangeTabCommand { get; } = new();
        public ReadOnlyReactiveProperty<GameMode> CurrentGameMode => _currentGameMode.ToReadOnlyReactiveProperty();
        
        private readonly ReactiveProperty<GameMode> _currentGameMode = new(GameMode.Classic);
        private IDisposable _bindings;
        
        public LeaderboardPanelViewModel(PanelManager panelManager) 
            : base(panelManager)
        {
            Bind();
        }
        
        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            // ChangeTabCommand
            //     .Subscribe(OnChangeTabCommandExecuted)
            //     .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }
        
        public override void Dispose()
        {
            base.Dispose();
            _bindings?.Dispose();
        }

        protected override void OnVisible()
        {
            base.OnVisible();
            _currentGameMode.OnNext(_currentGameMode.Value); // Force notify, so the data is loaded when the panel is opened
        }

        // private void OnChangeTabCommandExecuted(GameMode gameMode)
        // {
        //     if (_currentGameMode.Value == gameMode) return;
        //     _currentGameMode.Value = gameMode;
        // }
    }
}