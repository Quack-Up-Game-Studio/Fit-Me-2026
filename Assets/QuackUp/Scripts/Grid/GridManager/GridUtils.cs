using System.Collections.Generic;
using System.Linq;
using Redcode.Extensions;
using UnityEngine;

namespace FitMe.Grid
{
    public static class GridUtils
    {
        public static Vector2Int CalculateGridOffset(GridManagerConfig config, Vector2Int size)
        {
            var x = 0;
            var y = 0;
            switch (config.GridHorizontalOffsetType)
            {
                case GridOffsetType.Automatic:
                    x = -Mathf.FloorToInt(size.x / 2f) + config.CustomOffsetX;
                    break;
                case GridOffsetType.Custom:
                    x = config.CustomOffsetX;
                    break;
            }
            
            switch (config.GridVerticalOffsetType)
            {
                case GridOffsetType.Automatic:
                    y = Mathf.FloorToInt(size.y / 2f) + config.CustomOffsetY;
                    break;
                case GridOffsetType.Custom:
                    y = config.CustomOffsetY;
                    break;
            }
            return new Vector2Int(x, y);
        }
        
        public static Vector2Int ArrayToGridIndex(Vector2Int arrayIndex, Vector2Int currentOffset)
        {
            return new Vector2Int(arrayIndex.y + currentOffset.x, currentOffset.y - arrayIndex.x);
        }
        
        public static Vector2Int GridToArrayIndex(Vector2Int gridIndex, Vector2Int currentOffset)
        {
            return new Vector2Int(currentOffset.y - gridIndex.y, gridIndex.x - currentOffset.x);
        }
        
        public static Bounds GetCellBounds(this UnityEngine.Grid grid, CellModel cellModel)
        {
            var index = cellModel.GridIndex.Value;
            return grid.GetCellBounds(index);
        }

        public static Bounds GetCellBounds(this UnityEngine.Grid grid, Vector2Int gridIndex)
        {
            var cellBounds = grid.GetBoundsLocal((Vector3Int)gridIndex);
            var centerWorld = grid.GetCellCenterWorld((Vector3Int)gridIndex);
            cellBounds.center = centerWorld;
            return cellBounds;
        }

        public static Vector2 GetGridCenter(this UnityEngine.Grid grid, Vector2Int currentGridSize, Vector2Int currentOffset)
        {
            var column = currentGridSize.x;
            var row = currentGridSize.y;
            List<int> centerRows = column % 2 == 0
                ? new List<int> { column / 2 - 1, column / 2 }
                : new List<int> { Mathf.FloorToInt(column / 2f) };
            List<int> centerColumn = row % 2 == 0
                ? new List<int> { row / 2 - 1, row / 2 }
                : new List<int> { Mathf.FloorToInt(row / 2f) };
            List<Vector2Int> centerCells = (from x in centerRows from y in centerColumn 
                select ArrayToGridIndex(new Vector2Int(y, x), currentOffset)).ToList();
            Debug.Log("Center Cells: " + string.Join(", ", centerCells.Select(c => c.ToString())));
            List<Bounds> centerCellBounds = centerCells.Select(grid.GetCellBounds).ToList();
            var center = centerCellBounds.Aggregate(Vector3.zero, (current, bounds) => current + bounds.center) 
                         / centerCellBounds.Count;
            return center;
        }
    }
}