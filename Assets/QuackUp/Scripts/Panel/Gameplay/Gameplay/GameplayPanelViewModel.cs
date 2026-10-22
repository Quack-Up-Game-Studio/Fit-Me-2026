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
        public ReactiveCommand HintCommand { get; } = new();
        public ReactiveCommand RetryCommand { get; } = new();
        public ReadOnlyReactiveProperty<bool> IsHintVisible => _shapeHintController.IsVisible;
        public ReadOnlyReactiveProperty<bool> CanUseHint => _shapeHintController.CanUseHint;
        public ReadOnlyReactiveProperty<bool> IsRetryVisible => _shapeHintController.RetryVisible;
        public ReadOnlyReactiveProperty<bool> RetryFlashVisible => _shapeHintController.RetryFlashVisible;
        
        private readonly IGameStateManager _gameStateManager;
        private readonly ShapeHintController _shapeHintController;
        private readonly IMessageHub _gridManagerMessageHub;
        private readonly IMessageHub _blockManagerMessageHub;
        private IDisposable _bindings;
        
        [Inject]
        public GameplayPanelViewModel(
            PanelManager panelManager,
            IGameStateManager gameStateManager,
            ShapeHintController shapeHintController,
            [Key(GridManagerMessageHub.GridManagerMessageHubKey)] IMessageHub gridManagerMessageHub,
            [Key(BlockManagerMessageHub.MessageHubKey)] IMessageHub blockManagerMessageHub) : base(panelManager)
        {
            _gameStateManager = gameStateManager;
            _shapeHintController = shapeHintController;
            _gridManagerMessageHub = gridManagerMessageHub;
            _blockManagerMessageHub = blockManagerMessageHub;
            Bind();
        }

        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            PauseCommand
                .Subscribe(_ => OnPause())
                .AddTo(ref disposableBuilder);
            HintCommand
                .Subscribe(_ => _shapeHintController.RequestHint())
                .AddTo(ref disposableBuilder);
            RetryCommand
                .Subscribe(_ => OnRetry())
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

        private void OnRetry()
        {
            _shapeHintController?.DismissRetryGuidance();
            _blockManagerMessageHub.Publish(new ContinueEvent());
            _gridManagerMessageHub.Publish(new ClearGridEvent(true, false));
        }
    }
}