using System;
using FitMe.Grid;
using FitMe.Shared;
using QuackUp.Utils;
using R3;
using VContainer;

namespace FitMe.Panel
{
    public class GameplayPanelViewModel : PanelViewModel
    {
        public ReactiveCommand PauseCommand { get; } = new();
        public ReadOnlyReactiveProperty<GameMode> CurrentGameMode => _currentGameMode;
        private readonly ReactiveProperty<GameMode> _currentGameMode;
        
        private readonly IGameStateManager _gameStateManager;
        private IDisposable _bindings;
        private readonly IDisposable _modeBinding;
        
        [Inject]
        public GameplayPanelViewModel(
            PanelManager panelManager,
            IGameStateManager gameStateManager,
            ILevelManager levelManager,
            [Key(GridManagerMessageHub.GridManagerMessageHubKey)] IMessageHub messageHub) : base(panelManager)
        {
            _gameStateManager = gameStateManager;
            _currentGameMode = new ReactiveProperty<GameMode>(levelManager.CurrentGameMode);
            // LevelManager assigns its mode before publishing this session event,
            // including Classic (null runtime data), retry and seamless Shape levels.
            _modeBinding = messageHub.Subscribe<SpawnWithShapeLevelEvent>(
                _ => _currentGameMode.Value = levelManager.CurrentGameMode);
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
            _modeBinding?.Dispose();
            _currentGameMode.Dispose();
        }

        private void OnPause()
        {
            _gameStateManager.SetPause(true);
        }
    }
}