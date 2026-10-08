using System;
using System.Collections.Generic;
using UnityEngine;

namespace FitMe.Grid
{
    /// <summary>Pure occupancy and board-mask validation for resolved array coordinates.</summary>
    public static class BlockPlacementValidator
    {
        public static bool TryValidate(
            int rowCount,
            int columnCount,
            int[,] stageMask,
            IReadOnlyList<Vector2Int> targetIndexes,
            Func<Vector2Int, bool> isOccupied,
            out Vector2Int[] resolvedIndexes)
        {
            resolvedIndexes = Array.Empty<Vector2Int>();
            if (rowCount <= 0 || columnCount <= 0 || targetIndexes == null || targetIndexes.Count == 0 || isOccupied == null)
                return false;
            if (stageMask != null && (stageMask.GetLength(0) != rowCount || stageMask.GetLength(1) != columnCount))
                return false;

            var unique = new HashSet<Vector2Int>();
            var result = new Vector2Int[targetIndexes.Count];
            for (var i = 0; i < targetIndexes.Count; i++)
            {
                var index = targetIndexes[i];
                if (index.x < 0 || index.x >= rowCount || index.y < 0 || index.y >= columnCount ||
                    !unique.Add(index) || (stageMask != null && stageMask[index.x, index.y] == 0) || isOccupied(index))
                    return false;
                result[i] = index;
            }

            resolvedIndexes = result;
            return true;
        }
    }
}
