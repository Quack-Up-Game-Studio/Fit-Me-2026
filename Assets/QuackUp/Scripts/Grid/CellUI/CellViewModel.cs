using System;
using R3;
using UnityEngine;

namespace FitMe.Grid
{
    public class CellViewModel : IDisposable
    {
        public ReadOnlyReactiveProperty<Vector2Int> ArrayIndex { get; private set; }
        public ReadOnlyReactiveProperty<CellState> State { get; private set; }

        private readonly CellModel _model;
        private IDisposable _bindings;
        
        public CellViewModel(CellModel model)
        {
            _model = model;
            Bind();
        }

        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            ArrayIndex = _model.ArrayIndex
                .ToReadOnlyReactiveProperty()
                .AddTo(ref disposableBuilder);
            State = _model.State
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