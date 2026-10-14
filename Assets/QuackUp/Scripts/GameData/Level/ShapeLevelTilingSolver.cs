using System;
using System.Collections.Generic;
using UnityEngine;

namespace FitMe.GameData
{
    public enum ShapeLevelTilingStatus
    {
        Feasible,
        Infeasible,
        SearchLimitReached,
        InvalidInput
    }

    public sealed class ShapeLevelTilingResult
    {
        public ShapeLevelTilingStatus Status { get; }
        public int NodesVisited { get; }
        public bool IsFeasible => Status == ShapeLevelTilingStatus.Feasible;

        internal ShapeLevelTilingResult(ShapeLevelTilingStatus status, int nodesVisited)
        {
            Status = status;
            NodesVisited = nodesVisited;
        }
    }

    /// <summary>Bounded exact-cover solver for a fixed set of rotatable level blocks.</summary>
    public static class ShapeLevelTilingSolver
    {
        public static ShapeLevelTilingResult Solve(
            int[,] playableMask, IReadOnlyList<int[,]> blockSchemas, int maxSearchNodes)
        {
            if (playableMask == null || playableMask.Length == 0 ||
                blockSchemas == null || maxSearchNodes < 1)
                return new ShapeLevelTilingResult(ShapeLevelTilingStatus.InvalidInput, 0);

            var rows = playableMask.GetLength(0);
            var columns = playableMask.GetLength(1);
            var playable = new bool[rows, columns];
            var playableCellCount = 0;
            for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
            {
                var value = playableMask[row, column];
                if (value != 0 && value != 1)
                    return new ShapeLevelTilingResult(ShapeLevelTilingStatus.InvalidInput, 0);
                playable[row, column] = value == 1;
                if (playable[row, column]) playableCellCount++;
            }
            if (playableCellCount == 0)
                return new ShapeLevelTilingResult(ShapeLevelTilingStatus.InvalidInput, 0);

            var pieces = new Piece[blockSchemas.Count];
            long totalBlockCells = 0;
            for (var index = 0; index < blockSchemas.Count; index++)
            {
                if (!TryCreatePiece(blockSchemas[index], out pieces[index]))
                    return new ShapeLevelTilingResult(ShapeLevelTilingStatus.InvalidInput, 0);
                totalBlockCells += pieces[index].CellCount;
            }
            if (totalBlockCells != playableCellCount)
                return new ShapeLevelTilingResult(ShapeLevelTilingStatus.Infeasible, 0);

            var context = new SearchContext(playable, pieces, playableCellCount, maxSearchNodes);
            var found = Search(context, 0, 0);
            var status = found
                ? ShapeLevelTilingStatus.Feasible
                : context.SearchLimitReached
                    ? ShapeLevelTilingStatus.SearchLimitReached
                    : ShapeLevelTilingStatus.Infeasible;
            return new ShapeLevelTilingResult(status, context.NodesVisited);
        }

        private static bool Search(SearchContext context, int placedCount, int coveredCount)
        {
            if (context.NodesVisited >= context.MaxSearchNodes)
            {
                context.SearchLimitReached = true;
                return false;
            }
            context.NodesVisited++;

            if (coveredCount == context.PlayableCellCount)
                return placedCount == context.Pieces.Length;

            var anchor = FindFirstUncoveredCell(context);
            if (anchor.x < 0) return false;

            var seenPlacements = new HashSet<string>();
            for (var pieceIndex = 0; pieceIndex < context.Pieces.Length; pieceIndex++)
            {
                if (context.Placed[pieceIndex] || HasEarlierEquivalentUnplacedPiece(context, pieceIndex))
                    continue;

                var piece = context.Pieces[pieceIndex];
                foreach (var rotation in piece.Rotations)
                for (var anchorCellIndex = 0; anchorCellIndex < rotation.Length; anchorCellIndex++)
                {
                    var originRow = anchor.x - rotation[anchorCellIndex].x;
                    var originColumn = anchor.y - rotation[anchorCellIndex].y;
                    if (!TryGetPlacement(context, rotation, originRow, originColumn,
                            out var cellIndices, out var placementKey) || !seenPlacements.Add(placementKey))
                        continue;

                    context.Placed[pieceIndex] = true;
                    SetCovered(context, cellIndices, true);
                    if (Search(context, placedCount + 1, coveredCount + cellIndices.Length)) return true;
                    SetCovered(context, cellIndices, false);
                    context.Placed[pieceIndex] = false;
                    if (context.SearchLimitReached) return false;
                }
            }
            return false;
        }

        private static bool HasEarlierEquivalentUnplacedPiece(SearchContext context, int pieceIndex)
        {
            var key = context.Pieces[pieceIndex].RotationInvariantKey;
            for (var earlier = 0; earlier < pieceIndex; earlier++)
                if (!context.Placed[earlier] &&
                    string.Equals(context.Pieces[earlier].RotationInvariantKey, key, StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static Vector2Int FindFirstUncoveredCell(SearchContext context)
        {
            for (var row = 0; row < context.Playable.GetLength(0); row++)
            for (var column = 0; column < context.Playable.GetLength(1); column++)
                if (context.Playable[row, column] && !context.Covered[row, column])
                    return new Vector2Int(row, column);
            return new Vector2Int(-1, -1);
        }

        private static bool TryGetPlacement(
            SearchContext context, CellOffset[] rotation, int originRow, int originColumn,
            out int[] cellIndices, out string placementKey)
        {
            cellIndices = new int[rotation.Length];
            placementKey = null;
            var columns = context.Playable.GetLength(1);
            for (var index = 0; index < rotation.Length; index++)
            {
                var row = originRow + rotation[index].x;
                var column = originColumn + rotation[index].y;
                if (row < 0 || row >= context.Playable.GetLength(0) ||
                    column < 0 || column >= columns || !context.Playable[row, column] ||
                    context.Covered[row, column])
                    return false;
                cellIndices[index] = row * columns + column;
            }
            Array.Sort(cellIndices);
            placementKey = string.Join(",", cellIndices);
            return true;
        }

        private static void SetCovered(SearchContext context, IReadOnlyList<int> cellIndices, bool covered)
        {
            var columns = context.Playable.GetLength(1);
            for (var index = 0; index < cellIndices.Count; index++)
                context.Covered[cellIndices[index] / columns, cellIndices[index] % columns] = covered;
        }

        private static bool TryCreatePiece(int[,] schema, out Piece piece)
        {
            piece = null;
            if (schema == null || schema.Length == 0) return false;

            var cells = new List<CellOffset>();
            var minRow = schema.GetLength(0);
            var minColumn = schema.GetLength(1);
            for (var row = 0; row < schema.GetLength(0); row++)
            for (var column = 0; column < schema.GetLength(1); column++)
            {
                var value = schema[row, column];
                if (value != 0 && value != 1) return false;
                if (value != 1) continue;
                cells.Add(new CellOffset(row, column));
                minRow = Math.Min(minRow, row);
                minColumn = Math.Min(minColumn, column);
            }
            if (cells.Count == 0) return false;

            for (var index = 0; index < cells.Count; index++)
                cells[index] = new CellOffset(cells[index].x - minRow, cells[index].y - minColumn);
            cells.Sort(CellOffset.Compare);

            var rotations = new List<CellOffset[]>(4);
            var rotationKeys = new HashSet<string>(StringComparer.Ordinal);
            var current = cells.ToArray();
            for (var turn = 0; turn < 4; turn++)
            {
                var normalized = NormalizeRotation(current);
                var key = CreateShapeKey(normalized);
                if (rotationKeys.Add(key)) rotations.Add(normalized);
                current = RotateClockwise(current);
            }

            var sortedKeys = new List<string>(rotationKeys);
            sortedKeys.Sort(StringComparer.Ordinal);
            piece = new Piece(cells.Count, rotations.ToArray(), string.Join("|", sortedKeys));
            return true;
        }

        private static CellOffset[] RotateClockwise(IReadOnlyList<CellOffset> cells)
        {
            var rotated = new CellOffset[cells.Count];
            for (var index = 0; index < cells.Count; index++)
                rotated[index] = new CellOffset(cells[index].y, -cells[index].x);
            return rotated;
        }

        private static CellOffset[] NormalizeRotation(IReadOnlyList<CellOffset> cells)
        {
            var minRow = int.MaxValue;
            var minColumn = int.MaxValue;
            for (var index = 0; index < cells.Count; index++)
            {
                minRow = Math.Min(minRow, cells[index].x);
                minColumn = Math.Min(minColumn, cells[index].y);
            }
            var normalized = new CellOffset[cells.Count];
            for (var index = 0; index < cells.Count; index++)
                normalized[index] = new CellOffset(cells[index].x - minRow, cells[index].y - minColumn);
            Array.Sort(normalized, CellOffset.Compare);
            return normalized;
        }

        private static string CreateShapeKey(IReadOnlyList<CellOffset> cells)
        {
            var values = new string[cells.Count];
            for (var index = 0; index < cells.Count; index++)
                values[index] = cells[index].x + "," + cells[index].y;
            return string.Join(";", values);
        }

        private sealed class Piece
        {
            public readonly int CellCount;
            public readonly CellOffset[][] Rotations;
            public readonly string RotationInvariantKey;

            public Piece(int cellCount, CellOffset[][] rotations, string rotationInvariantKey)
            {
                CellCount = cellCount;
                Rotations = rotations;
                RotationInvariantKey = rotationInvariantKey;
            }
        }

        private sealed class SearchContext
        {
            public readonly bool[,] Playable;
            public readonly bool[,] Covered;
            public readonly Piece[] Pieces;
            public readonly bool[] Placed;
            public readonly int PlayableCellCount;
            public readonly int MaxSearchNodes;
            public int NodesVisited;
            public bool SearchLimitReached;

            public SearchContext(bool[,] playable, Piece[] pieces, int playableCellCount, int maxSearchNodes)
            {
                Playable = playable;
                Covered = new bool[playable.GetLength(0), playable.GetLength(1)];
                Pieces = pieces;
                Placed = new bool[pieces.Length];
                PlayableCellCount = playableCellCount;
                MaxSearchNodes = maxSearchNodes;
            }
        }

        private readonly struct CellOffset
        {
            public readonly int x;
            public readonly int y;

            public CellOffset(int x, int y)
            {
                this.x = x;
                this.y = y;
            }

            public static int Compare(CellOffset left, CellOffset right) => left.x != right.x
                ? left.x.CompareTo(right.x)
                : left.y.CompareTo(right.y);
        }
    }
}
