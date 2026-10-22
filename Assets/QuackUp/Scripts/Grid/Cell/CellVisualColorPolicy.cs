using System;
using UnityEngine;

namespace FitMe.Grid
{
    public static class CellVisualColorPolicy
    {
        public static Color Resolve(
            Color baseColor,
            CellState state,
            bool isHintHighlighted,
            bool isHintFlashVisible,
            Color canBePlacedColor,
            Color cannotBePlacedColor,
            Color hintFlashColor)
        {
            if (state == CellState.CanBePlaced) return canBePlacedColor;
            if (state == CellState.CannotBePlaced) return cannotBePlacedColor;
            if (state != CellState.None) throw new ArgumentOutOfRangeException(nameof(state), state, null);
            return isHintHighlighted && isHintFlashVisible ? hintFlashColor : baseColor;
        }
    }
}
