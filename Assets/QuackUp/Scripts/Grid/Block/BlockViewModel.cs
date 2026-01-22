using System;
using System.Collections.Generic;
using QuackUp.Utils;
using R3;
using VContainer;

namespace FitMe.Grid
{
    public class BlockViewModel : IDisposable
    {
        public ReadOnlyReactiveProperty<BlockInteractionState> BlockInteractionState => _model.BlockInteractionState;
        public ReadOnlyReactiveProperty<BlockColor> BlockType => _model.BlockType;
        public ReactiveCommand<int> SetSortingLayerCommand => _model.SetSortingLayerCommand;
        public ReactiveCommand<int> SetSortingOrderCommand => _model.SetSortingOrderCommand;
        //public IReadOnlyList<AtomModel> Atoms => _model.Atoms;
        
        
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