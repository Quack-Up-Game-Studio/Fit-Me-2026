using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace FitMe.Grid.Tests
{
    public class DeterministicTilingSolverTests
    {
        [Test]
        public void TrySolve_FindsDeterministicSolutionForRotatedBlock()
        {
            IReadOnlyList<Vector2Int>[] blocks =
            {
                new[] { Vector2Int.zero, Vector2Int.right },
                new[] { Vector2Int.zero, Vector2Int.right }
            };
            var board = new[,] { { 1, 1 }, { 1, 1 } };

            Assert.That(DeterministicTilingSolver.TrySolve(board, blocks, out var first), Is.True);
            Assert.That(DeterministicTilingSolver.TrySolve(board, blocks, out var second), Is.True);
            Assert.That(second, Is.EqualTo(first));
            Assert.That(first[0], Has.Length.EqualTo(2));
            Assert.That(first[1], Has.Length.EqualTo(2));

            var coveredCells = new HashSet<Vector2Int>();
            foreach (var blockPlacement in first)
            foreach (var cell in blockPlacement)
            {
                Assert.That(board[cell.x, cell.y], Is.Not.Zero, "placements must stay on playable cells");
                Assert.That(coveredCells.Add(cell), Is.True, "each playable cell must be covered once");
            }
            Assert.That(coveredCells.Count, Is.EqualTo(4));
        }

        [Test]
        public void TrySolve_RejectsImpossibleGeometryAndCapacityMismatch()
        {
            IReadOnlyList<Vector2Int>[] blocks = { new[] { Vector2Int.zero, Vector2Int.right, Vector2Int.left, Vector2Int.up } };
            Assert.That(DeterministicTilingSolver.TrySolve(new[,] { { 1, 0 }, { 0, 1 } }, blocks, out _), Is.False);
            Assert.That(DeterministicTilingSolver.TrySolve(new[,] { { 1, 1 } }, new IReadOnlyList<Vector2Int>[] { new[] { Vector2Int.zero } }, out _), Is.False);
        }

        [Test]
        public void TrySolve_TreatsZeroBoardCellsAsUnavailableObstacles()
        {
            IReadOnlyList<Vector2Int>[] blocks = { new[] { Vector2Int.zero, Vector2Int.right } };
            Assert.That(DeterministicTilingSolver.TrySolve(new[,] { { 1, 0, 1 } }, blocks, out _), Is.False);
        }

        [Test]
        public void TrySolve_RejectsDuplicateCellsWithinBlock()
        {
            IReadOnlyList<Vector2Int>[] blocks = { new[] { Vector2Int.zero, Vector2Int.zero } };
            Assert.That(DeterministicTilingSolver.TrySolve(new[,] { { 1, 1 } }, blocks, out _), Is.False);
        }
    }
}
