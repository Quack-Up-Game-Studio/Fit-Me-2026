using System;
using FitMe.Grid;
using QuackUp.Utils;
using R3;
using VContainer;

namespace FitMe.Panel
{
    public class DragMeToPlayViewModel : IDisposable
    {
        public ReactiveCommand<Promise<Unit>> TransitionInCommand { get; } = new();
        public ReactiveCommand<Promise<Unit>> TransitionOutCommand { get; } = new();

        private readonly IMessageHub _messageHub;
        private IDisposable _bindings;
        private IDisposable _blockBindings;
        private Promise<Unit> _currentTransitionPromise;
        
        [Inject]
        public DragMeToPlayViewModel([Key(BlockManagerMessageHub.MessageHubKey)] IMessageHub messageHub)
        {
            _messageHub = messageHub;
            Bind();
        }
        
        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            _messageHub
                .Subscribe<BlockSpawnedEvent>(OnBlockSpawned)
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }
        
        public void Dispose()
        {
            _bindings?.Dispose();
            _blockBindings?.Dispose();
        }
        
        private void OnBlockSpawned(BlockSpawnedEvent data)
        {
            _blockBindings?.Dispose();
            var disposableBuilder = Disposable.CreateBuilder();
            var viewModel = data.BlockInstances[0].ViewModel;
            viewModel.BlockInteractionState
                .Where(x => x is BlockInteractionState.PickUp)
                .Subscribe(_ => OnPickUp())
                .AddTo(ref disposableBuilder);
            viewModel.BlockInteractionState
                .Where(x => x is BlockInteractionState.PlacedOnSpawn)
                .Subscribe(_ => OnPlaced())
                .AddTo(ref disposableBuilder);
            _blockBindings = disposableBuilder.Build();
        }

        private void OnPickUp()
        {
            _currentTransitionPromise?.Cancel();
            _currentTransitionPromise = new Promise<Unit>();
            TransitionOutCommand.Execute(_currentTransitionPromise);
        }

        private void OnPlaced()
        {
            _currentTransitionPromise?.Cancel();
            _currentTransitionPromise = new Promise<Unit>();
            TransitionInCommand.Execute(_currentTransitionPromise);
        }
    }
}