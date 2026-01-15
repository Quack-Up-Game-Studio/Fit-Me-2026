using System;
using QuackUp.Utils;
using R3;
using UnityEngine;

namespace FitMe.Grid
{
    public class CellViewModel : IDisposable
    {
        public ReadOnlyReactiveProperty<Vector2Int> ArrayIndex { get; private set; }
        public ReadOnlyReactiveProperty<CellState> State { get; private set; }
        public TransformData TransformData => _model.TransformData;
        //expose event directly from the model
        public event Action OnDisposed
        {
            add => _model.OnDisposed += value;
            remove => _model.OnDisposed -= value;
        }

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
        
        private void OnModelDisposed()
        {
            Dispose();
        }

        public void Dispose()
        {
            _bindings?.Dispose();
        }
    }
}