using System;
using R3;
using VContainer;

namespace FitMe.Grid
{
    public class BlockViewModel : IDisposable
    {
        public ReadOnlyReactiveProperty<BlockInteractionState> BlockInteractionState { get; private set; }
        
        private readonly BlockModel _model;
        private IDisposable _bindings;

        [Inject]
        public BlockViewModel(BlockModel model)
        {
            _model = model;
            Bind();
        }

        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            BlockInteractionState = _model.BlockInteractionState
                .ToReadOnlyReactiveProperty()
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }
        
        public void Dispose()
        {
            _bindings?.Dispose();
        }
    }
}