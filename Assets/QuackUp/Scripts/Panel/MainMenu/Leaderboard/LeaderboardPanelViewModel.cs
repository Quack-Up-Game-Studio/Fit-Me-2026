using System;
using QuackUp.GoogleAdMob;
using System.Linq;
using FitMe.Shared;
using QuackUp.Utils;
using R3;
using VContainer;

namespace FitMe.Panel
{
    public class LeaderboardPanelViewModel : PanelViewModel
    {
        // public ReactiveCommand<GameMode> ChangeTabCommand { get; } = new();
        public ReadOnlyReactiveProperty<LeaderboardStatus> Status => _status.ToReadOnlyReactiveProperty();
        public ReadOnlyReactiveProperty<GameMode> CurrentGameMode => _currentGameMode.ToReadOnlyReactiveProperty();
        
        private readonly ReactiveProperty<GameMode> _currentGameMode = new(GameMode.Classic);
        private readonly ReactiveProperty<LeaderboardStatus> _status = new(LeaderboardStatus.Loading);
        public ReadOnlyReactiveProperty<bool> AdsEnabled => _adsService.AdsEnabled;
        private readonly AdsService _adsService;
        private IDisposable _bindings;
        private IDisposable _nestedBindings;
        
        private readonly PanelManager _nestedPanelManager;
        
        public LeaderboardPanelViewModel(
            [Key(PanelManagerInstaller.NestedPanelId)] PanelManager nestedPanelManager,
            PanelManager panelManager,
            AdsService adsService) 
            : base(panelManager)
        {
            _nestedPanelManager = nestedPanelManager;
            _adsService = adsService;
            Bind();
        }
        
        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            // ChangeTabCommand
            //     .Subscribe(OnChangeTabCommandExecuted)
            //     .AddTo(ref disposableBuilder);
            _nestedPanelManager.OnFinishedInitialize
                .Subscribe(_ => OnNestedPanelFinishInitialize())
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }
        
        public override void Dispose()
        {
            base.Dispose();
            _bindings?.Dispose();
            _nestedBindings?.Dispose();
        }

        protected override void OnVisible()
        {
            base.OnVisible();
            _currentGameMode.OnNext(_currentGameMode.Value); // Force notify, so the data is loaded when the panel is opened
        }

        private void OnNestedPanelFinishInitialize()
        {
            var nestedTabs = _nestedPanelManager.GetPanelsOfType<LeaderboardTabViewModel>();
            var merged = nestedTabs.Select(tab => tab.Status).Merge();
            _nestedBindings = merged
                .Subscribe(x => _status.Value = x);
        }

        // private void OnChangeTabCommandExecuted(GameMode gameMode)
        // {
        //     if (_currentGameMode.Value == gameMode) return;
        //     _currentGameMode.Value = gameMode;
        // }
    }
}