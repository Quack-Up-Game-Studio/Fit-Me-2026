using System;
using QuackUp.Utils;
using R3;
using UnityEngine;
using VContainer;

namespace FitMe.Grid
{
    public enum CellState
    {
        None,
        CanBePlaced,
        CannotBePlaced,
    }
    
    [Serializable]
    public class CellModel : IDisposable
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public ReactiveProperty<AtomModel> CurrentAtom { get; set; } = new();
        public ReactiveProperty<Vector2Int> ArrayIndex { get; set; } = new();
        public ReactiveProperty<Vector2Int> GridIndex { get; set; } = new();
        public ReactiveProperty<CellState> State { get; set; } = new(CellState.None);
        public ICellView CellView { get; private set; }

        [Inject]
        public CellModel(ICellView cellView)
        {
            CellView = cellView;
        }

        public void Dispose()
        {
            CurrentAtom?.Dispose();
            ArrayIndex?.Dispose();
            GridIndex?.Dispose();
            State?.Dispose();
        }
    }
}