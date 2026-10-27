using System;
using FitMe.Grid;
using R3;
using VContainer;

namespace FitMe.Panel
{
    public sealed class RotateBlockViewModel : IDisposable
    {
        public ReactiveCommand RotateCommand { get; } = new();
        public ReadOnlyReactiveProperty<bool> CanRotate => _blockManager.CanRotate;

        private readonly BlockManager _blockManager;
        private IDisposable _bindings;

        [Inject]
        public RotateBlockViewModel(BlockManager blockManager)
        {
            _blockManager = blockManager;
            var builder = Disposable.CreateBuilder();
            RotateCommand.Subscribe(_ => _blockManager.Rotate()).AddTo(ref builder);
            _bindings = builder.Build();
        }

        public void Dispose()
        {
            _bindings?.Dispose();
        }
    }
}
