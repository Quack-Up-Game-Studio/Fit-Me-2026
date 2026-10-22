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
        public ReactiveProperty<Vector4> BaseOutlineDiagonals { get; } = new(Vector4.zero);
        public ReactiveProperty<Vector4> BaseOutlineEdges { get; } = new(Vector4.one);
        public ReactiveProperty<AtomInstance> CurrentAtom { get; set; } = new();
        public ReactiveProperty<Vector2Int> ArrayIndex { get; set; } = new();
        public ReactiveProperty<Vector2Int> GridIndex { get; set; } = new();
        public ReactiveProperty<CellState> State { get; set; } = new(CellState.None);
        public ReactiveProperty<bool> HintHighlighted { get; set; } = new(false);
        public ReactiveProperty<bool> HintFlashVisible { get; set; } = new(false);

        [Inject]
        public CellModel()
        {
        }

        public void Dispose()
        {
            BaseOutlineDiagonals.Dispose();
            BaseOutlineEdges.Dispose();
            CurrentAtom?.Dispose();
            ArrayIndex?.Dispose();
            GridIndex?.Dispose();
            State?.Dispose();
            HintHighlighted?.Dispose();
            HintFlashVisible?.Dispose();
        }
    }
}