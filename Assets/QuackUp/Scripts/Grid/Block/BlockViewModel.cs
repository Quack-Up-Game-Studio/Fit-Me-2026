using System;
using QuackUp.Utils;
using R3;
using VContainer;

namespace FitMe.Grid
{
    public class BlockViewModel : IDisposable
    {
        public ReadOnlyReactiveProperty<BlockInteractionState> BlockInteractionState => _model.BlockInteractionState;
        public ReadOnlyReactiveProperty<BlockTypes> BlockType => _model.BlockType;
        public TransformData TransformData => _model.TransformData;
        
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
            _bindings = disposableBuilder.Build();
        }
        
        public void Dispose()
        {
            _bindings?.Dispose();
        }
    }
}