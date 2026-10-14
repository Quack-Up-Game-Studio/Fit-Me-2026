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
        public void Solve_ReturnsBlockIndexedCellsForTheFoundTiling()
        {
            var playableMask = new[,] { { 1 }, { 1 }, { 1 } };
            var result = ShapeLevelTilingSolver.Solve(
                playableMask,
                new[] { new[,] { { 1, 1 } }, new[,] { { 1 } } },
                maxSearchNodes: 100);

            Assert.That(result.Status, Is.EqualTo(ShapeLevelTilingStatus.Feasible));
            Assert.That(result.Placements, Has.Count.EqualTo(2));

            var covered = new int[3, 1];
            var sawDomino = false;
            var sawSingleton = false;
            foreach (var placement in result.Placements)
            {
                var blockIndex = placement.BlockIndex;
                var cells = placement.Cells;
                Assert.That(blockIndex, Is.InRange(0, 1));
                Assert.That(blockIndex == 0 ? sawDomino : sawSingleton, Is.False,
                    "Each input block instance must appear exactly once in the solution.");
                if (blockIndex == 0)
                {
                    sawDomino = true;
                    Assert.That(cells, Has.Count.EqualTo(2));
                    Assert.That(cells[0].y, Is.EqualTo(cells[1].y),
                        "The horizontal input domino must be reported in its solved vertical orientation.");
                }
                else
                {
                    sawSingleton = true;
                    Assert.That(cells, Has.Count.EqualTo(1));
                }

                foreach (var cell in cells)
                {
                    Assert.That(cell.x, Is.InRange(0, 2));
                    Assert.That(cell.y, Is.Zero);
                    Assert.That(playableMask[cell.x, cell.y], Is.EqualTo(1));
                    Assert.That(covered[cell.x, cell.y], Is.Zero,
                        "A solved cell must not be covered by more than one block.");
                    covered[cell.x, cell.y] = 1;
                }
            }

            Assert.That(sawDomino && sawSingleton, Is.True);
            for (var row = 0; row < playableMask.GetLength(0); row++)
                Assert.That(covered[row, 0], Is.EqualTo(playableMask[row, 0]));
        }

        [Test]
        public void Solve_ProvesInfeasibleWhenEqualAreaPiecesCannotFitDisconnectedCells()
        {
            var result = ShapeLevelTilingSolver.Solve(
                new[,] { { 1, 0, 1 } },
                new[] { new[,] { { 1, 1 } } },
                maxSearchNodes: 100);

            Assert.That(result.Status, Is.EqualTo(ShapeLevelTilingStatus.Infeasible));
            Assert.That(result.Placements, Is.Empty);
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
            Assert.That(result.Placements, Is.Empty);
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
