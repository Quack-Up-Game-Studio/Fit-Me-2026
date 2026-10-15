using System;
using System.Collections.Generic;
using UnityEngine;

namespace FitMe.GameData
{
    /// <summary>Calculates the generator's provisional placement-count heuristic for a board and block list.</summary>
    public static class ShapePlaysetDifficultyEvaluator
    {
        public const int CurrentScoreModelVersion = 2;

        public static ShapePlaysetDifficultyMetrics Evaluate(
            int[,] playableMask,
            int obstacleCellCount,
            int voidCellCount,
            IReadOnlyList<int[,]> blockFootprints,
            int solutionCount = 0,
            bool solutionCountIsExact = false)
        {
            if (playableMask == null) throw new ArgumentNullException(nameof(playableMask));
            if (blockFootprints == null) throw new ArgumentNullException(nameof(blockFootprints));
            if (obstacleCellCount < 0) throw new ArgumentOutOfRangeException(nameof(obstacleCellCount));
            if (voidCellCount < 0) throw new ArgumentOutOfRangeException(nameof(voidCellCount));
            if (solutionCount < 0) throw new ArgumentOutOfRangeException(nameof(solutionCount));
            if (solutionCount == 0 && solutionCountIsExact)
                throw new ArgumentException("An exact solution count must be positive for a feasible playset.",
                    nameof(solutionCountIsExact));

            var playableCellCount = CountCells(playableMask);
            var totalPlacements = 0;
            var minimumPlacements = int.MaxValue;
            var maximumPlacements = 0;
            var playerCellCount = 0;
            var score = 0;
            for (var index = 0; index < blockFootprints.Count; index++)
            {
                var footprint = blockFootprints[index];
                if (footprint == null)
                    throw new ArgumentException($"Block footprint at index {index} is null.", nameof(blockFootprints));

                var cellCount = CountCells(footprint);
                var boundingBoxArea = footprint.Length;
                var legalPlacements = CountLegalPlacements(playableMask, footprint);
                totalPlacements += legalPlacements;
                minimumPlacements = Math.Min(minimumPlacements, legalPlacements);
                maximumPlacements = Math.Max(maximumPlacements, legalPlacements);
                playerCellCount += cellCount;
                score += (boundingBoxArea - cellCount) * 2 + Math.Max(0, 8 - legalPlacements);
            }

            var averagePlacements = blockFootprints.Count == 0
                ? 0f
                : (float)totalPlacements / blockFootprints.Count;
            var obstacleRatio = playableCellCount + obstacleCellCount == 0
                ? 0f
                : (float)obstacleCellCount / (playableCellCount + obstacleCellCount);
            score = Math.Max(0, score - CalculateSolutionMultiplicityPenalty(solutionCount));
            return new ShapePlaysetDifficultyMetrics(
                playableCellCount, obstacleCellCount, voidCellCount, playerCellCount, blockFootprints.Count,
                totalPlacements, averagePlacements,
                minimumPlacements == int.MaxValue ? 0 : minimumPlacements,
                maximumPlacements, obstacleRatio, score, CurrentScoreModelVersion,
                solutionCount, solutionCountIsExact);
        }

        private static int CalculateSolutionMultiplicityPenalty(int solutionCount)
        {
            if (solutionCount <= 1) return 0;
            var ambiguityLevel = (int)Math.Ceiling(Math.Log(solutionCount, 2d));
            return 3 * ambiguityLevel;
        }

        private static int CountLegalPlacements(int[,] playableMask, int[,] footprint)
        {
            var source = GetCoordinates(footprint);
            var uniqueRotations = new HashSet<string>();
            var rows = playableMask.GetLength(0);
            var columns = playableMask.GetLength(1);
            var count = 0;
            for (var rotation = 0; rotation < 4; rotation++)
            {
                var rotated = new Vector2Int[source.Count];
                var minRow = int.MaxValue;
                var minColumn = int.MaxValue;
                for (var index = 0; index < source.Count; index++)
                {
                    var cell = source[index];
                    for (var turn = 0; turn < rotation; turn++)
                        cell = new Vector2Int(cell.y, -cell.x);
                    rotated[index] = cell;
                    minRow = Math.Min(minRow, cell.x);
                    minColumn = Math.Min(minColumn, cell.y);
                }
                for (var index = 0; index < rotated.Length; index++)
                    rotated[index] -= new Vector2Int(minRow, minColumn);
                Array.Sort(rotated, (left, right) => left.x != right.x
                    ? left.x.CompareTo(right.x)
                    : left.y.CompareTo(right.y));
                var shapeKey = string.Join(";", Array.ConvertAll(rotated,
                    cell => cell.x + "," + cell.y));
                if (!uniqueRotations.Add(shapeKey)) continue;

                for (var originRow = 0; originRow < rows; originRow++)
                for (var originColumn = 0; originColumn < columns; originColumn++)
                {
                    if (playableMask[originRow, originColumn] == 0) continue;
                    var fits = true;
                    for (var index = 0; index < rotated.Length; index++)
                    {
                        var row = originRow + rotated[index].x;
                        var column = originColumn + rotated[index].y;
                        if (row >= rows || column >= columns || playableMask[row, column] == 0)
                        {
                            fits = false;
                            break;
                        }
                    }
                    if (fits) count++;
                }
            }
            return count;
        }

        private static IReadOnlyList<Vector2Int> GetCoordinates(int[,] footprint)
        {
            var cells = new List<Vector2Int>();
            for (var row = 0; row < footprint.GetLength(0); row++)
            for (var column = 0; column < footprint.GetLength(1); column++)
                if (footprint[row, column] != 0) cells.Add(new Vector2Int(row, column));
            return cells;
        }

        private static int CountCells(int[,] mask)
        {
            var count = 0;
            foreach (var value in mask)
                if (value != 0) count++;
            return count;
        }
    }
}
