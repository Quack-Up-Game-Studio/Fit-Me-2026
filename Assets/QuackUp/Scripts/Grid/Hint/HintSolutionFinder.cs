using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using QuackUp.Utils;

namespace FitMe.Grid
{
    public enum HintSolutionStatus
    {
        Feasible,
        Infeasible,
        SearchLimitReached,
        InvalidInput
    }

    public sealed class HintSolutionMove
    {
        private readonly IReadOnlyList<TilingCell> _cells;
        public int BlockIndex { get; }
        public bool RequiresSwap { get; }
        public IReadOnlyList<TilingCell> Cells => _cells;

        internal HintSolutionMove(int blockIndex, bool requiresSwap, IReadOnlyList<TilingCell> cells)
        {
            BlockIndex = blockIndex;
            RequiresSwap = requiresSwap;
            var copy = new TilingCell[cells.Count];
            for (var index = 0; index < cells.Count; index++) copy[index] = cells[index];
            _cells = Array.AsReadOnly(copy);
        }
    }

    public sealed class HintSolutionResult
    {
        public HintSolutionStatus Status { get; }
        public HintSolutionMove FirstMove { get; }
        public int NodesVisited { get; }

        internal HintSolutionResult(HintSolutionStatus status, HintSolutionMove firstMove, int nodesVisited)
        {
            Status = status;
            FirstMove = firstMove;
            NodesVisited = nodesVisited;
        }
    }

    /// <summary>
    /// Finds a complete legal placement sequence for the finite Shape Mode supply.
    /// Search follows the active hand and permits swapping only its first occupied slot with the next queued block.
    /// </summary>
    public static class HintSolutionFinder
    {
        public static HintSolutionResult FindFirstMove(
            int[,] playableMask,
            int[,] occupiedMask,
            int[,] obstacleMask,
            IReadOnlyList<int[,]> remainingBlockSchemas,
            int activeHandCount,
            int maxSearchNodes,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryCreateContext(playableMask, occupiedMask, obstacleMask, remainingBlockSchemas,
                    activeHandCount, maxSearchNodes, cancellationToken, out var context))
                return Result(HintSolutionStatus.InvalidInput);

            if (context.ClearableCellCount == 0 || HasNoFreeCellBeforeMove(context))
                return Result(HintSolutionStatus.InvalidInput);

            Search(context, context.InitialOccupied, context.Pieces, activeHandCount);
            if (context.SearchLimitReached)
                return new HintSolutionResult(HintSolutionStatus.SearchLimitReached, null, context.NodesVisited);
            if (context.FirstMove == null)
                return new HintSolutionResult(HintSolutionStatus.Infeasible, null, context.NodesVisited);
            return new HintSolutionResult(HintSolutionStatus.Feasible, context.FirstMove, context.NodesVisited);
        }

        private static bool Search(Context context, bool[,] occupied, List<Piece> remaining, int activeHandCount)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (remaining.Count == 0) return true;
            // With no queued pieces, only the occupied cells and remaining shape multiset affect feasibility.
            if (activeHandCount == remaining.Count &&
                !context.UnorderedSearchStates.Add(CreateUnorderedStateKey(occupied, remaining)))
                return false;
            if (context.NodesVisited >= context.MaxSearchNodes)
            {
                context.SearchLimitReached = true;
                return false;
            }
            context.NodesVisited++;

            var hasExactCoverAnchor = TryGetExactCoverAnchor(
                context, occupied, remaining, activeHandCount, out var exactCoverAnchor);
            var activeCount = Math.Min(activeHandCount, remaining.Count);
            var variants = new List<SupplyVariant>(2)
            {
                new(remaining, requiresSwap: false)
            };

            if (activeCount > 0 && remaining.Count > activeCount)
            {
                var swapped = new List<Piece>(remaining);
                (swapped[0], swapped[activeCount]) = (swapped[activeCount], swapped[0]);
                if (!string.Equals(remaining[0].RotationInvariantKey, remaining[activeCount].RotationInvariantKey,
                        StringComparison.Ordinal))
                    variants.Add(new SupplyVariant(swapped, requiresSwap: true));
            }

            for (var variantIndex = 0; variantIndex < variants.Count; variantIndex++)
            {
                var variant = variants[variantIndex];
                var handCount = Math.Min(activeCount, variant.Pieces.Count);
                for (var handIndex = 0; handIndex < handCount; handIndex++)
                {
                    var piece = variant.Pieces[handIndex];
                    foreach (var placement in EnumeratePlacements(
                                 context, occupied, piece,
                                 hasExactCoverAnchor ? exactCoverAnchor : (TilingCell?)null))
                    {
                        var nextOccupied = Clone(occupied);
                        ApplyPlacement(nextOccupied, placement);

                        var nextSupply = new List<Piece>(variant.Pieces);
                        nextSupply.RemoveAt(handIndex);
                        if (IsBoardFull(context, nextOccupied) && nextSupply.Count > 0)
                            continue;

                        var nextHandCount = Math.Min(activeHandCount, nextSupply.Count);
                        var move = new HintSolutionMove(handIndex, variant.RequiresSwap, placement);
                        var isFirstMove = context.Path.Count == 0;
                        context.Path.Add(move);

                        if (Search(context, nextOccupied, nextSupply, nextHandCount))
                        {
                            if (isFirstMove) context.FirstMove = move;
                            else if (context.FirstMove == null) context.FirstMove = context.Path[0];
                            return true;
                        }

                        context.Path.RemoveAt(context.Path.Count - 1);
                        if (context.SearchLimitReached) return false;
                    }
                }
            }
            return false;
        }

        private static bool TryGetExactCoverAnchor(
            Context context, bool[,] occupied, IReadOnlyList<Piece> remaining,
            int activeHandCount, out TilingCell anchor)
        {
            anchor = default;
            // Anchoring is complete only when every remaining piece is selectable and all free cells must be covered.
            if (activeHandCount != remaining.Count) return false;

            var remainingCellCount = 0;
            for (var index = 0; index < remaining.Count; index++)
                remainingCellCount += remaining[index].CellCount;

            var freeCellCount = 0;
            for (var row = 0; row < context.PlayableMask.GetLength(0); row++)
            for (var column = 0; column < context.PlayableMask.GetLength(1); column++)
            {
                if (!context.PlayableMask[row, column] || context.ObstacleMask[row, column] || occupied[row, column])
                    continue;
                freeCellCount++;
            }
            if (remainingCellCount != freeCellCount) return false;

            for (var row = 0; row < context.PlayableMask.GetLength(0); row++)
            for (var column = 0; column < context.PlayableMask.GetLength(1); column++)
            {
                if (!context.PlayableMask[row, column] || context.ObstacleMask[row, column] || occupied[row, column])
                    continue;
                anchor = new TilingCell(row, column);
                return true;
            }
            return false;
        }

        private static string CreateUnorderedStateKey(bool[,] occupied, IReadOnlyList<Piece> remaining)
        {
            var builder = new StringBuilder();
            builder.Append(occupied.GetLength(0)).Append('x').Append(occupied.GetLength(1)).Append(':');
            for (var row = 0; row < occupied.GetLength(0); row++)
            for (var column = 0; column < occupied.GetLength(1); column++)
                builder.Append(occupied[row, column] ? '1' : '0');

            var shapeKeys = new string[remaining.Count];
            for (var index = 0; index < remaining.Count; index++)
                shapeKeys[index] = remaining[index].RotationInvariantKey;
            Array.Sort(shapeKeys, StringComparer.Ordinal);

            builder.Append('|').Append(shapeKeys.Length).Append(':');
            for (var index = 0; index < shapeKeys.Length; index++)
                builder.Append(shapeKeys[index].Length).Append(':').Append(shapeKeys[index]);
            return builder.ToString();
        }

        private static IEnumerable<TilingCell[]> EnumeratePlacements(
            Context context, bool[,] occupied, Piece piece, TilingCell? requiredCell)
        {
            var rows = context.PlayableMask.GetLength(0);
            var columns = context.PlayableMask.GetLength(1);
            foreach (var rotation in piece.Rotations)
            for (var originRow = 0; originRow < rows; originRow++)
            for (var originColumn = 0; originColumn < columns; originColumn++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                var placement = new TilingCell[rotation.Length];
                var valid = true;
                var coversRequiredCell = !requiredCell.HasValue;
                for (var cellIndex = 0; cellIndex < rotation.Length; cellIndex++)
                {
                    var row = originRow + rotation[cellIndex].Row;
                    var column = originColumn + rotation[cellIndex].Column;
                    if (row < 0 || row >= rows || column < 0 || column >= columns ||
                        context.PlayableMask[row, column] == false || occupied[row, column])
                    {
                        valid = false;
                        break;
                    }
                    placement[cellIndex] = new TilingCell(row, column);
                    if (requiredCell.HasValue && row == requiredCell.Value.Row &&
                        column == requiredCell.Value.Column)
                        coversRequiredCell = true;
                }
                if (valid && coversRequiredCell) yield return placement;
            }
        }

        private static void ApplyPlacement(bool[,] occupied, IReadOnlyList<TilingCell> placement)
        {
            for (var index = 0; index < placement.Count; index++)
                occupied[placement[index].Row, placement[index].Column] = true;
        }

        private static bool IsBoardFull(Context context, bool[,] occupied)
        {
            for (var row = 0; row < occupied.GetLength(0); row++)
            for (var column = 0; column < occupied.GetLength(1); column++)
                if (context.PlayableMask[row, column] && !context.ObstacleMask[row, column] && !occupied[row, column])
                    return false;
            return true;
        }

        private static bool HasNoFreeCellBeforeMove(Context context)
        {
            for (var row = 0; row < context.PlayableMask.GetLength(0); row++)
            for (var column = 0; column < context.PlayableMask.GetLength(1); column++)
                if (context.PlayableMask[row, column] && !context.InitialOccupied[row, column])
                    return false;
            return true;
        }

        private static bool TryCreateContext(
            int[,] playableMask,
            int[,] occupiedMask,
            int[,] obstacleMask,
            IReadOnlyList<int[,]> remainingBlockSchemas,
            int activeHandCount,
            int maxSearchNodes,
            CancellationToken cancellationToken,
            out Context context)
        {
            context = null;
            if (!HasSameDimensions(playableMask, occupiedMask) ||
                !HasSameDimensions(playableMask, obstacleMask) ||
                remainingBlockSchemas == null || activeHandCount < 1 ||
                activeHandCount > remainingBlockSchemas.Count || maxSearchNodes < 1)
                return false;

            var rows = playableMask.GetLength(0);
            var columns = playableMask.GetLength(1);
            var playable = new bool[rows, columns];
            var occupied = new bool[rows, columns];
            var obstacles = new bool[rows, columns];
            var clearableCellCount = 0;
            for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
            {
                var playableValue = playableMask[row, column];
                var occupiedValue = occupiedMask[row, column];
                var obstacleValue = obstacleMask[row, column];
                if (playableValue is not (0 or 1) || occupiedValue is not (0 or 1) || obstacleValue is not (0 or 1))
                    return false;
                if (obstacleValue == 1 && (playableValue != 1 || occupiedValue != 1)) return false;
                playable[row, column] = playableValue == 1;
                occupied[row, column] = occupiedValue == 1;
                obstacles[row, column] = obstacleValue == 1;
                if (playable[row, column] && !obstacles[row, column]) clearableCellCount++;
            }

            var pieces = new List<Piece>(remainingBlockSchemas.Count);
            for (var index = 0; index < remainingBlockSchemas.Count; index++)
            {
                if (!TryCreatePiece(remainingBlockSchemas[index], out var piece)) return false;
                pieces.Add(piece);
            }

            context = new Context(playable, occupied, obstacles, pieces, clearableCellCount, maxSearchNodes,
                cancellationToken);
            return true;
        }

        private static bool HasSameDimensions(int[,] first, int[,] second) =>
            first != null && second != null && first.Length > 0 &&
            first.GetLength(0) == second.GetLength(0) && first.GetLength(1) == second.GetLength(1);

        private static bool TryCreatePiece(int[,] schema, out Piece piece)
        {
            piece = null;
            if (schema == null || schema.Length == 0) return false;
            var cells = new List<TilingCell>();
            for (var row = 0; row < schema.GetLength(0); row++)
            for (var column = 0; column < schema.GetLength(1); column++)
            {
                var value = schema[row, column];
                if (value != 0 && value != 1) return false;
                if (value == 1) cells.Add(new TilingCell(row, column));
            }
            if (cells.Count == 0) return false;

            var rotations = new List<TilingCell[]>(4);
            var rotationKeys = new HashSet<string>(StringComparer.Ordinal);
            var current = cells.ToArray();
            for (var turn = 0; turn < 4; turn++)
            {
                var normalized = Normalize(current);
                var key = ShapeKey(normalized);
                if (rotationKeys.Add(key)) rotations.Add(normalized);
                current = RotateClockwise(current);
            }
            var invariantKeys = new List<string>(rotationKeys);
            invariantKeys.Sort(StringComparer.Ordinal);
            piece = new Piece(cells.Count, rotations.ToArray(), string.Join("|", invariantKeys));
            return true;
        }

        private static TilingCell[] RotateClockwise(IReadOnlyList<TilingCell> cells)
        {
            var result = new TilingCell[cells.Count];
            for (var index = 0; index < cells.Count; index++)
                result[index] = new TilingCell(cells[index].Column, -cells[index].Row);
            return result;
        }

        private static TilingCell[] Normalize(IReadOnlyList<TilingCell> cells)
        {
            var minRow = int.MaxValue;
            var minColumn = int.MaxValue;
            for (var index = 0; index < cells.Count; index++)
            {
                minRow = Math.Min(minRow, cells[index].Row);
                minColumn = Math.Min(minColumn, cells[index].Column);
            }
            var result = new TilingCell[cells.Count];
            for (var index = 0; index < cells.Count; index++)
                result[index] = new TilingCell(cells[index].Row - minRow, cells[index].Column - minColumn);
            Array.Sort(result);
            return result;
        }

        private static string ShapeKey(IReadOnlyList<TilingCell> cells)
        {
            var values = new string[cells.Count];
            for (var index = 0; index < cells.Count; index++)
                values[index] = cells[index].Row + "," + cells[index].Column;
            return string.Join(";", values);
        }

        private static bool[,] Clone(bool[,] source) => (bool[,])source.Clone();

        private static HintSolutionResult Result(HintSolutionStatus status) =>
            new(status, null, 0);

        private sealed class Piece
        {
            public readonly int CellCount;
            public readonly TilingCell[][] Rotations;
            public readonly string RotationInvariantKey;

            public Piece(int cellCount, TilingCell[][] rotations, string rotationInvariantKey)
            {
                CellCount = cellCount;
                Rotations = rotations;
                RotationInvariantKey = rotationInvariantKey;
            }
        }

        private sealed class SupplyVariant
        {
            public readonly List<Piece> Pieces;
            public readonly bool RequiresSwap;

            public SupplyVariant(List<Piece> pieces, bool requiresSwap)
            {
                Pieces = pieces;
                RequiresSwap = requiresSwap;
            }
        }

        private sealed class Context
        {
            public readonly bool[,] PlayableMask;
            public readonly bool[,] InitialOccupied;
            public readonly bool[,] ObstacleMask;
            public readonly List<Piece> Pieces;
            public readonly int ClearableCellCount;
            public readonly int MaxSearchNodes;
            public readonly CancellationToken CancellationToken;
            public readonly List<HintSolutionMove> Path = new();
            public readonly HashSet<string> UnorderedSearchStates = new(StringComparer.Ordinal);
            public HintSolutionMove FirstMove;
            public int NodesVisited;
            public bool SearchLimitReached;

            public Context(bool[,] playableMask, bool[,] initialOccupied, bool[,] obstacleMask,
                List<Piece> pieces, int clearableCellCount, int maxSearchNodes, CancellationToken cancellationToken)
            {
                PlayableMask = playableMask;
                InitialOccupied = initialOccupied;
                ObstacleMask = obstacleMask;
                Pieces = pieces;
                ClearableCellCount = clearableCellCount;
                MaxSearchNodes = maxSearchNodes;
                CancellationToken = cancellationToken;
            }
        }
    }
}
