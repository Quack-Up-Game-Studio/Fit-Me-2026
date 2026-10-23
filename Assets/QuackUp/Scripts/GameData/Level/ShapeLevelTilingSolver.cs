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

    public sealed class ShapeLevelTilingPlacement
    {
        public int BlockIndex { get; }
        public IReadOnlyList<Vector2Int> Cells { get; }

        public ShapeLevelTilingPlacement(int blockIndex, IReadOnlyList<Vector2Int> cells)
        {
            BlockIndex = blockIndex;
            Cells = Array.AsReadOnly(new List<Vector2Int>(cells).ToArray());
        }
    }

    public sealed class ShapeLevelTilingResult
    {
        public ShapeLevelTilingStatus Status { get; }
        public int NodesVisited { get; }
        public IReadOnlyList<IReadOnlyList<ShapeLevelTilingPlacement>> Solutions { get; }
        public IReadOnlyList<ShapeLevelTilingPlacement> Placements =>
            Solutions.Count == 0 ? Array.Empty<ShapeLevelTilingPlacement>() : Solutions[0];
        public int SolutionCount { get; }
        public bool IsSearchComplete { get; }
        public bool IsFeasible => SolutionCount > 0;

        internal ShapeLevelTilingResult(
            ShapeLevelTilingStatus status, int nodesVisited, int solutionCount,
            IReadOnlyList<IReadOnlyList<ShapeLevelTilingPlacement>> solutions,
            bool isSearchComplete)
        {
            Status = status;
            NodesVisited = nodesVisited;
            SolutionCount = solutionCount;
            Solutions = solutions == null
                ? Array.AsReadOnly(Array.Empty<IReadOnlyList<ShapeLevelTilingPlacement>>())
                : Array.AsReadOnly(new List<IReadOnlyList<ShapeLevelTilingPlacement>>(solutions).ToArray());
            IsSearchComplete = isSearchComplete;
        }
    }

    /// <summary>
    /// Bounded exact-cover solver for a fixed set of rotatable level blocks. It precomputes unique placements
    /// and branches only on placements covering the first uncovered playable cell.
    /// </summary>
    public static class ShapeLevelTilingSolver
    {
        public static ShapeLevelTilingResult Solve(
            int[,] playableMask, IReadOnlyList<int[,]> blockSchemas, int maxSearchNodes)
        {
            return RunSearch(playableMask, blockSchemas, maxSearchNodes, 1, stopAfterFirst: true);
        }

        public static ShapeLevelTilingResult EnumerateSolutions(
            int[,] playableMask, IReadOnlyList<int[,]> blockSchemas,
            int maxSearchNodes, int maxStoredSolutions)
        {
            return RunSearch(playableMask, blockSchemas, maxSearchNodes, maxStoredSolutions, stopAfterFirst: false);
        }

        private static ShapeLevelTilingResult RunSearch(
            int[,] playableMask, IReadOnlyList<int[,]> blockSchemas,
            int maxSearchNodes, int maxStoredSolutions, bool stopAfterFirst)
        {
            if (playableMask == null || playableMask.Length == 0 ||
                blockSchemas == null || maxSearchNodes < 1 || maxStoredSolutions < 1)
                return CreateResult(ShapeLevelTilingStatus.InvalidInput, 0, 0,
                    Array.Empty<IReadOnlyList<ShapeLevelTilingPlacement>>(), isSearchComplete: false);

            var rows = playableMask.GetLength(0);
            var columns = playableMask.GetLength(1);
            var playable = new bool[rows * columns];
            var playableCellCount = 0;
            for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
            {
                var value = playableMask[row, column];
                if (value != 0 && value != 1)
                    return CreateResult(ShapeLevelTilingStatus.InvalidInput, 0, 0,
                        Array.Empty<IReadOnlyList<ShapeLevelTilingPlacement>>(), isSearchComplete: false);
                if (value != 1) continue;
                playable[row * columns + column] = true;
                playableCellCount++;
            }
            if (playableCellCount == 0)
                return CreateResult(ShapeLevelTilingStatus.InvalidInput, 0, 0,
                    Array.Empty<IReadOnlyList<ShapeLevelTilingPlacement>>(), isSearchComplete: false);

            var groups = new List<ShapeGroup>();
            var groupsByKey = new Dictionary<string, ShapeGroup>(StringComparer.Ordinal);
            long totalBlockCells = 0;
            for (var index = 0; index < blockSchemas.Count; index++)
            {
                if (!TryCreatePiece(blockSchemas[index], out var piece))
                    return CreateResult(ShapeLevelTilingStatus.InvalidInput, 0, 0,
                        Array.Empty<IReadOnlyList<ShapeLevelTilingPlacement>>(), isSearchComplete: false);
                totalBlockCells += piece.CellCount;
                if (!groupsByKey.TryGetValue(piece.RotationInvariantKey, out var group))
                {
                    group = new ShapeGroup(piece.CellCount, piece.Rotations);
                    groupsByKey.Add(piece.RotationInvariantKey, group);
                    groups.Add(group);
                }
                group.BlockIndices.Add(index);
            }
            if (totalBlockCells != playableCellCount)
                return CreateResult(ShapeLevelTilingStatus.Infeasible, 0, 0,
                    Array.Empty<IReadOnlyList<ShapeLevelTilingPlacement>>(), isSearchComplete: true);

            foreach (var group in groups)
            {
                BuildPlacements(group, playable, rows, columns);
                if (group.Placements.Count == 0)
                    return CreateResult(ShapeLevelTilingStatus.Infeasible, 0, 0,
                        Array.Empty<IReadOnlyList<ShapeLevelTilingPlacement>>(), isSearchComplete: true);
            }

            var context = new SearchContext(
                playable, groups.ToArray(), playableCellCount,
                maxSearchNodes, maxStoredSolutions, columns, stopAfterFirst);
            Search(context, 0, 0, 0);
            var isSearchComplete = !context.SearchLimitReached &&
                                   (!stopAfterFirst || context.SolutionCount == 0);
            var status = context.SearchLimitReached
                ? ShapeLevelTilingStatus.SearchLimitReached
                : context.SolutionCount > 0 || context.FirstSolution != null
                    ? ShapeLevelTilingStatus.Feasible
                    : ShapeLevelTilingStatus.Infeasible;
            if (context.Solutions.Count == 0 && context.FirstSolution != null)
                context.Solutions.Add(context.FirstSolution);
            return CreateResult(status, context.NodesVisited, context.SolutionCount,
                context.Solutions, isSearchComplete);
        }

        private static ShapeLevelTilingResult CreateResult(
            ShapeLevelTilingStatus status, int nodesVisited, int solutionCount,
            IReadOnlyList<IReadOnlyList<ShapeLevelTilingPlacement>> solutions, bool isSearchComplete) =>
            new(status, nodesVisited, solutionCount, solutions, isSearchComplete);

        private static bool Search(
            SearchContext context, int anchorStartIndex, int placedCount, int coveredCount)
        {
            if (coveredCount == context.PlayableCellCount)
            {
                if (placedCount != context.TotalPieceCount) return false;
                context.SolutionCount++;
                var solution = CreatePlacements(context);
                context.FirstSolution ??= solution;
                if (context.Solutions.Count < context.MaxStoredSolutions)
                    context.Solutions.Add(solution);
                return context.StopAfterFirst;
            }

            if (context.NodesVisited >= context.MaxSearchNodes)
            {
                context.SearchLimitReached = true;
                return false;
            }
            context.NodesVisited++;

            var anchor = FindFirstUncoveredCell(context, anchorStartIndex);
            if (anchor < 0) return false;
            // Every exact tiling must cover this cell; anchoring here avoids exploring block-order permutations.
            for (var shapeIndex = 0; shapeIndex < context.Groups.Length; shapeIndex++)
            {
                if (context.RemainingCounts[shapeIndex] == 0) continue;
                var group = context.Groups[shapeIndex];
                foreach (var placement in group.ByCell[anchor])
                {
                    var canPlace = true;
                    for (var cellIndex = 0; cellIndex < placement.CellIndices.Length; cellIndex++)
                    {
                        if (!context.Occupied[placement.CellIndices[cellIndex]]) continue;
                        canPlace = false;
                        break;
                    }
                    if (!canPlace) continue;

                    context.RemainingCounts[shapeIndex]--;
                    context.CurrentPlacements.Add(new PendingPlacement(shapeIndex, placement));
                    for (var cellIndex = 0; cellIndex < placement.CellIndices.Length; cellIndex++)
                        context.Occupied[placement.CellIndices[cellIndex]] = true;
                    if (Search(context, anchor + 1, placedCount + 1,
                            coveredCount + group.CellCount)) return true;
                    context.CurrentPlacements.RemoveAt(context.CurrentPlacements.Count - 1);
                    for (var cellIndex = 0; cellIndex < placement.CellIndices.Length; cellIndex++)
                        context.Occupied[placement.CellIndices[cellIndex]] = false;
                    context.RemainingCounts[shapeIndex]++;
                    if (context.SearchLimitReached || context.StopAfterFirst && context.SolutionCount > 0) return false;
                }
            }
            return false;
        }

        private static void BuildPlacements(
            ShapeGroup group, bool[] playable, int rows, int columns)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            group.ByCell = new List<Placement>[rows * columns];
            for (var index = 0; index < group.ByCell.Length; index++)
                group.ByCell[index] = new List<Placement>();

            foreach (var rotation in group.Rotations)
            {
                var maxRow = 0;
                var maxColumn = 0;
                for (var index = 0; index < rotation.Length; index++)
                {
                    maxRow = Math.Max(maxRow, rotation[index].x);
                    maxColumn = Math.Max(maxColumn, rotation[index].y);
                }

                for (var originRow = 0; originRow < rows - maxRow; originRow++)
                for (var originColumn = 0; originColumn < columns - maxColumn; originColumn++)
                {
                    var cellIndices = new int[rotation.Length];
                    var isPlayable = true;
                    for (var cellIndex = 0; cellIndex < rotation.Length; cellIndex++)
                    {
                        var row = originRow + rotation[cellIndex].x;
                        var column = originColumn + rotation[cellIndex].y;
                        var boardCellIndex = row * columns + column;
                        if (!playable[boardCellIndex])
                        {
                            isPlayable = false;
                            break;
                        }
                        cellIndices[cellIndex] = boardCellIndex;
                    }
                    if (!isPlayable) continue;

                    Array.Sort(cellIndices);
                    if (!seen.Add(string.Join(",", cellIndices))) continue;
                    var placement = new Placement(cellIndices);
                    group.Placements.Add(placement);
                    for (var cellIndex = 0; cellIndex < cellIndices.Length; cellIndex++)
                        group.ByCell[cellIndices[cellIndex]].Add(placement);
                }
            }
        }

        private static IReadOnlyList<ShapeLevelTilingPlacement> CreatePlacements(SearchContext context)
        {
            var nextBlockIndex = new int[context.Groups.Length];
            var placements = new ShapeLevelTilingPlacement[context.CurrentPlacements.Count];
            for (var index = 0; index < context.CurrentPlacements.Count; index++)
            {
                var pending = context.CurrentPlacements[index];
                var group = context.Groups[pending.ShapeIndex];
                var blockIndex = group.BlockIndices[nextBlockIndex[pending.ShapeIndex]++];
                var cellIndices = pending.Placement.CellIndices;
                var cells = new Vector2Int[cellIndices.Length];
                for (var cellIndex = 0; cellIndex < cellIndices.Length; cellIndex++)
                {
                    cells[cellIndex] = new Vector2Int(
                        cellIndices[cellIndex] / context.Columns, cellIndices[cellIndex] % context.Columns);
                }
                placements[index] = new ShapeLevelTilingPlacement(blockIndex, cells);
            }
            return Array.AsReadOnly(placements);
        }

        private static int FindFirstUncoveredCell(SearchContext context, int startIndex)
        {
            for (var index = startIndex; index < context.Playable.Length; index++)
                if (context.Playable[index] && !context.Occupied[index]) return index;
            return -1;
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

        private sealed class ShapeGroup
        {
            public readonly int CellCount;
            public readonly CellOffset[][] Rotations;
            public readonly List<int> BlockIndices = new List<int>();
            public readonly List<Placement> Placements = new List<Placement>();
            public List<Placement>[] ByCell = Array.Empty<List<Placement>>();

            public ShapeGroup(int cellCount, CellOffset[][] rotations)
            {
                CellCount = cellCount;
                Rotations = rotations;
            }
        }

        private sealed class SearchContext
        {
            public readonly bool[] Playable;
            public readonly bool[] Occupied;
            public readonly ShapeGroup[] Groups;
            public readonly int[] RemainingCounts;
            public readonly List<PendingPlacement> CurrentPlacements = new List<PendingPlacement>();
            public readonly List<IReadOnlyList<ShapeLevelTilingPlacement>> Solutions =
                new List<IReadOnlyList<ShapeLevelTilingPlacement>>();
            public readonly int PlayableCellCount;
            public readonly int TotalPieceCount;
            public readonly int MaxSearchNodes;
            public readonly int MaxStoredSolutions;
            public readonly int Columns;
            public readonly bool StopAfterFirst;
            public IReadOnlyList<ShapeLevelTilingPlacement> FirstSolution;
            public int SolutionCount;
            public int NodesVisited;
            public bool SearchLimitReached;

            public SearchContext(
                bool[] playable, ShapeGroup[] groups, int playableCellCount,
                int maxSearchNodes, int maxStoredSolutions, int columns, bool stopAfterFirst)
            {
                Playable = playable;
                Occupied = new bool[playable.Length];
                Groups = groups;
                RemainingCounts = new int[groups.Length];
                for (var index = 0; index < groups.Length; index++)
                {
                    RemainingCounts[index] = groups[index].BlockIndices.Count;
                    TotalPieceCount += RemainingCounts[index];
                }
                PlayableCellCount = playableCellCount;
                MaxSearchNodes = maxSearchNodes;
                MaxStoredSolutions = maxStoredSolutions;
                Columns = columns;
                StopAfterFirst = stopAfterFirst;
            }
        }

        private sealed class Placement
        {
            public readonly int[] CellIndices;

            public Placement(int[] cellIndices)
            {
                CellIndices = cellIndices;
            }
        }

        private readonly struct PendingPlacement
        {
            public readonly int ShapeIndex;
            public readonly Placement Placement;

            public PendingPlacement(int shapeIndex, Placement placement)
            {
                ShapeIndex = shapeIndex;
                Placement = placement;
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
