using FitMe.GameData;
using NUnit.Framework;

namespace FitMe.Grid.Tests
{
    public sealed class ShapeLevelTilingSolverTests
    {
        [Test]
        public void Solve_FindsTilingWhenOnlyRotatedBlockFits()
        {
            var result = ShapeLevelTilingSolver.Solve(
                new[,] { { 1 }, { 1 } },
                new[] { new[,] { { 1, 1 } } },
                maxSearchNodes: 100);

            Assert.That(result.Status, Is.EqualTo(ShapeLevelTilingStatus.Feasible));
            Assert.That(result.NodesVisited, Is.GreaterThan(0));
        }

        [Test]
        public void Solve_ProvesInfeasibleWhenEqualAreaPiecesCannotFitDisconnectedCells()
        {
            var result = ShapeLevelTilingSolver.Solve(
                new[,] { { 1, 0, 1 } },
                new[] { new[,] { { 1, 1 } } },
                maxSearchNodes: 100);

            Assert.That(result.Status, Is.EqualTo(ShapeLevelTilingStatus.Infeasible));
        }

        [Test]
        public void Solve_ReturnsSearchLimitInsteadOfClaimingInfeasible()
        {
            var result = ShapeLevelTilingSolver.Solve(
                new[,] { { 1, 1 }, { 1, 1 } },
                new[] { new[,] { { 1, 1 } }, new[,] { { 1, 1 } } },
                maxSearchNodes: 1);

            Assert.That(result.Status, Is.EqualTo(ShapeLevelTilingStatus.SearchLimitReached));
            Assert.That(result.NodesVisited, Is.EqualTo(1));
        }

        [Test]
        public void Solve_RejectsCellCountMismatchWithoutSearching()
        {
            var result = ShapeLevelTilingSolver.Solve(
                new[,] { { 1, 1, 1 } },
                new[] { new[,] { { 1, 1 } } },
                maxSearchNodes: 100);

            Assert.That(result.Status, Is.EqualTo(ShapeLevelTilingStatus.Infeasible));
            Assert.That(result.NodesVisited, Is.Zero);
        }

        [Test]
        public void Solve_EmptyBlockListIsInfeasibleForNonEmptyBoard()
        {
            var result = ShapeLevelTilingSolver.Solve(
                new[,] { { 1 } },
                System.Array.Empty<int[,]>(),
                maxSearchNodes: 100);

            Assert.That(result.Status, Is.EqualTo(ShapeLevelTilingStatus.Infeasible));
            Assert.That(result.NodesVisited, Is.Zero);
        }

        [Test]
        public void Solve_RejectsInvalidSchemasAndMasks()
        {
            var invalidSchema = ShapeLevelTilingSolver.Solve(
                new[,] { { 1 } },
                new[] { new[,] { { 2 } } },
                maxSearchNodes: 100);
            var invalidMask = ShapeLevelTilingSolver.Solve(
                new[,] { { 2 } },
                new[] { new[,] { { 1 } } },
                maxSearchNodes: 100);

            Assert.That(invalidSchema.Status, Is.EqualTo(ShapeLevelTilingStatus.InvalidInput));
            Assert.That(invalidMask.Status, Is.EqualTo(ShapeLevelTilingStatus.InvalidInput));
        }
    }
}
