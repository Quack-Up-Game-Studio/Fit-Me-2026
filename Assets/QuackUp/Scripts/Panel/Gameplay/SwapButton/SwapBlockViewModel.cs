using System;
using FitMe.Grid;
using R3;
using VContainer;

namespace FitMe.Panel
{
    public class SwapBlockViewModel : IDisposable
    {
        public ReactiveCommand SwapCommand { get; } = new();
        private readonly BlockManager _blockManager;
        
        private IDisposable _bindings;

        [Inject]
        public SwapBlockViewModel(BlockManager blockManager)
        {
            _blockManager = blockManager;
            Bind();
        }
        
        private void Bind()
        {
            var builder = Disposable.CreateBuilder();
            SwapCommand
                .Subscribe(_ => OnSwap())
                .AddTo(ref builder);
            _bindings = builder.Build();
        }

        public void Dispose()
        {
            _bindings?.Dispose();
        }

        private void OnSwap()
        {
            _blockManager.Swap();
        }
    }
}