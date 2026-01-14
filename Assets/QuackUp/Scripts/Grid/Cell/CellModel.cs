using System;
using R3;
using UnityEngine;

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
        public ReactiveProperty<AtomView> CurrentAtom { get; set; } = new();
        public ReactiveProperty<Vector2Int> ArrayIndex { get; set; } = new();
        public ReactiveProperty<Vector2Int> GridIndex { get; set; } = new();
        public ReactiveProperty<CellState> State { get; set; } = new(CellState.None);

        public event Action OnDisposed;

        public void Dispose()
        {
            CurrentAtom?.Dispose();
            ArrayIndex?.Dispose();
            GridIndex?.Dispose();
            State?.Dispose();
            OnDisposed?.Invoke();
        }
    }
}