using System;
using System.Collections.Generic;
using UnityEngine;

namespace FitMe.Grid
{
    /// <summary>Finds a deterministic exact tiling of playable board cells with unordered freeform blocks.</summary>
    public static class DeterministicTilingSolver
    {
        /// <summary>
        /// Each nonzero board cell is playable; each block is a list of local Vector2Int coordinates.
        /// The output contains one array of absolute board coordinates per input block.
        /// Vector2Int.x is the board row and Vector2Int.y is the board column.
        /// </summary>
        public static bool TrySolve(int[,] boardMask, IReadOnlyList<IReadOnlyList<Vector2Int>> blocks, out Vector2Int[][] blockPlacements)
        {
            blockPlacements = Array.Empty<Vector2Int[]>();
            if (boardMask == null || blocks == null || blocks.Count == 0) return false;

            var board = new HashSet<Vector2Int>();
            for (var row = 0; row < boardMask.GetLength(0); row++)
                for (var column = 0; column < boardMask.GetLength(1); column++)
                    if (boardMask[row, column] != 0) board.Add(new Vector2Int(row, column));

            var total = 0;
            for (var i = 0; i < blocks.Count; i++)
            {
                if (blocks[i] == null || blocks[i].Count == 0) return false;
                var unique = new HashSet<Vector2Int>(blocks[i]);
                if (unique.Count != blocks[i].Count) return false;
                total += unique.Count;
            }
            if (total != board.Count || board.Count == 0) return false;

            var placements = new List<Placement>[blocks.Count];
            for (var i = 0; i < blocks.Count; i++)
            {
                placements[i] = BuildPlacements(blocks[i], board);
                if (placements[i].Count == 0) return false;
            }

            var occupied = new HashSet<Vector2Int>();
            var used = new bool[blocks.Count];
            var solution = new Placement[blocks.Count];
            if (!Search(board, placements, occupied, used, solution, 0)) return false;

            blockPlacements = new Vector2Int[blocks.Count][];
            for (var i = 0; i < blocks.Count; i++) blockPlacements[i] = solution[i].Cells;
            return true;
        }

        private static List<Placement> BuildPlacements(IReadOnlyList<Vector2Int> block, HashSet<Vector2Int> board)
        {
            var result = new List<Placement>();
            var unique = new HashSet<string>();
            for (var rotation = 0; rotation < 4; rotation++)
            {
                var rotated = new Vector2Int[block.Count];
                var minRow = int.MaxValue; var minColumn = int.MaxValue;
                for (var i = 0; i < block.Count; i++)
                {
                    var p = block[i];
                    for (var r = 0; r < rotation; r++) p = new Vector2Int(p.y, -p.x);
                    rotated[i] = p;
                    minRow = Math.Min(minRow, p.x); minColumn = Math.Min(minColumn, p.y);
                }
                for (var i = 0; i < rotated.Length; i++) rotated[i] -= new Vector2Int(minRow, minColumn);
                Array.Sort(rotated, (a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
                var shapeKey = string.Join(";", Array.ConvertAll(rotated, p => p.x + "," + p.y));
                if (!unique.Add(shapeKey)) continue;
                foreach (var origin in SortedCells(board))
                {
                    var cells = new Vector2Int[rotated.Length];
                    var fits = true;
                    for (var i = 0; i < rotated.Length; i++)
                    {
                        cells[i] = origin + rotated[i];
                        if (!board.Contains(cells[i])) { fits = false; break; }
                    }
                    if (fits) result.Add(new Placement(cells));
                }
            }
            return result;
        }

        private static List<Vector2Int> SortedCells(HashSet<Vector2Int> cells)
        {
            var result = new List<Vector2Int>(cells);
            result.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            return result;
        }

        private static bool Search(HashSet<Vector2Int> board, List<Placement>[] placements, HashSet<Vector2Int> occupied,
            bool[] used, Placement[] solution, int placedCount)
        {
            if (placedCount == used.Length) return occupied.Count == board.Count;
            var selectedBlock = -1;
            List<Placement> candidates = null;
            var bestCount = int.MaxValue;
            for (var block = 0; block < used.Length; block++)
            {
                if (used[block]) continue;
                var available = new List<Placement>();
                foreach (var placement in placements[block])
                {
                    var clear = true;
                    foreach (var cell in placement.Cells) if (occupied.Contains(cell)) { clear = false; break; }
                    if (clear) available.Add(placement);
                }
                if (available.Count < bestCount) { selectedBlock = block; candidates = available; bestCount = available.Count; }
            }
            if (selectedBlock < 0 || candidates == null || candidates.Count == 0) return false;
            foreach (var placement in candidates)
            {
                used[selectedBlock] = true; solution[selectedBlock] = placement;
                foreach (var cell in placement.Cells) occupied.Add(cell);
                if (Search(board, placements, occupied, used, solution, placedCount + 1)) return true;
                foreach (var cell in placement.Cells) occupied.Remove(cell);
                used[selectedBlock] = false;
            }
            return false;
        }

        private sealed class Placement
        {
            public readonly Vector2Int[] Cells;
            public Placement(Vector2Int[] cells) { Cells = cells; }
        }
    }
}
