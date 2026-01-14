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
    public class CellModel
    {
        public ReactiveProperty<Atom> CurrentAtom { get; set; } = new();
        public ReactiveProperty<Vector2Int> ArrayIndex { get; set; } = new();
        public ReactiveProperty<Vector2Int> GridIndex { get; set; } = new();
        public ReactiveProperty<CellState> State { get; set; } = new(CellState.None);
    }
}