using System.Threading;
using NUnit.Framework;

namespace FitMe.Grid.Tests
{
    public sealed class HintSolutionFinderTests
    {
        [Test]
        public void FindFirstMove_UsesTheActualSingleBlockHandForTheWholeQueue()
        {
            var result = HintSolutionFinder.FindFirstMove(
                playableMask: new[,]
                {
                    { 1, 1, 1, 1, 1, 1, 1, 1 },
                    { 1, 1, 1, 1, 1, 1, 1, 1 },
                    { 0, 1, 1, 1, 1, 1, 1, 1 },
                    { 1, 1, 1, 1, 1, 1, 1, 1 },
                    { 1, 1, 1, 1, 1, 1, 1, 0 },
                    { 1, 1, 1, 1, 1, 1, 1, 1 }
                },
                occupiedMask: new[,]
                {
                    { 0, 1, 0, 0, 1, 0, 0, 0 },
                    { 0, 0, 0, 0, 0, 0, 0, 0 },
                    { 0, 0, 0, 0, 0, 0, 0, 0 },
                    { 0, 0, 0, 0, 0, 0, 0, 0 },
                    { 0, 0, 0, 0, 1, 0, 0, 0 },
                    { 0, 0, 0, 0, 0, 1, 0, 0 }
                },
                obstacleMask: new[,]
                {
                    { 0, 1, 0, 0, 1, 0, 0, 0 },
                    { 0, 0, 0, 0, 0, 0, 0, 0 },
                    { 0, 0, 0, 0, 0, 0, 0, 0 },
                    { 0, 0, 0, 0, 0, 0, 0, 0 },
                    { 0, 0, 0, 0, 1, 0, 0, 0 },
                    { 0, 0, 0, 0, 0, 1, 0, 0 }
                },
                remainingBlockSchemas: new[]
                {
                    new[,] { { 1, 0 }, { 1, 1 }, { 0, 1 } },
                    new[,] { { 1, 1 }, { 1, 1 } },
                    new[,] { { 1, 1 }, { 1, 1 } },
                    new[,] { { 0, 1, 0 }, { 1, 1, 1 } },
                    new[,] { { 1, 1 }, { 1, 1 } },
                    new[,] { { 1, 1 }, { 0, 1 }, { 0, 1 } },
                    new[,] { { 1, 1 }, { 0, 1 }, { 0, 1 } },
                    new[,] { { 1, 1 }, { 1, 0 }, { 1, 0 } },
                    new[,] { { 0, 1 }, { 1, 1 }, { 1, 0 } },
                    new[,] { { 0, 1 }, { 1, 1 }, { 1, 0 } }
                },
                activeHandCount: 1,
                maxSearchNodes: 1_000_000);

            Assert.That(result.Status, Is.EqualTo(HintSolutionStatus.Feasible));
            Assert.That(result.FirstMove, Is.Not.Null);
            Assert.That(result.NodesVisited, Is.LessThan(1_800), $"Visited {result.NodesVisited} nodes.");
        }

        [Test]
        public void FindFirstMove_FindsTheSixByEightSupplyWhenAllTrayPiecesAreSelectable()
        {
            var result = HintSolutionFinder.FindFirstMove(
                playableMask: new[,]
                {
                    { 1, 1, 1, 1, 1, 1, 1, 1 },
                    { 1, 1, 1, 1, 1, 1, 1, 1 },
                    { 0, 1, 1, 1, 1, 1, 1, 1 },
                    { 1, 1, 1, 1, 1, 1, 1, 1 },
                    { 1, 1, 1, 1, 1, 1, 1, 0 },
                    { 1, 1, 1, 1, 1, 1, 1, 1 }
                },
                occupiedMask: new[,]
                {
                    { 0, 1, 0, 0, 1, 0, 0, 0 },
                    { 0, 0, 0, 0, 0, 0, 0, 0 },
                    { 0, 0, 0, 0, 0, 0, 0, 0 },
                    { 0, 0, 0, 0, 0, 0, 0, 0 },
                    { 0, 0, 0, 0, 1, 0, 0, 0 },
                    { 0, 0, 0, 0, 0, 1, 0, 0 }
                },
                obstacleMask: new[,]
                {
                    { 0, 1, 0, 0, 1, 0, 0, 0 },
                    { 0, 0, 0, 0, 0, 0, 0, 0 },
                    { 0, 0, 0, 0, 0, 0, 0, 0 },
                    { 0, 0, 0, 0, 0, 0, 0, 0 },
                    { 0, 0, 0, 0, 1, 0, 0, 0 },
                    { 0, 0, 0, 0, 0, 1, 0, 0 }
                },
                remainingBlockSchemas: new[]
                {
                    new[,] { { 1, 0 }, { 1, 1 }, { 0, 1 } },
                    new[,] { { 1, 1 }, { 1, 1 } },
                    new[,] { { 1, 1 }, { 1, 1 } },
                    new[,] { { 0, 1, 0 }, { 1, 1, 1 } },
                    new[,] { { 1, 1 }, { 1, 1 } },
                    new[,] { { 1, 1 }, { 0, 1 }, { 0, 1 } },
                    new[,] { { 1, 1 }, { 0, 1 }, { 0, 1 } },
                    new[,] { { 1, 1 }, { 1, 0 }, { 1, 0 } },
                    new[,] { { 0, 1 }, { 1, 1 }, { 1, 0 } },
                    new[,] { { 0, 1 }, { 1, 1 }, { 1, 0 } }
                },
                activeHandCount: 10,
                maxSearchNodes: 1_000_000);

            Assert.That(result.Status, Is.EqualTo(HintSolutionStatus.Feasible));
            Assert.That(result.FirstMove, Is.Not.Null);
            Assert.That(result.FirstMove.RequiresSwap, Is.False);
            Assert.That(result.NodesVisited, Is.LessThan(1_800), $"Visited {result.NodesVisited} nodes.");
        }

        [Test]
        public void FindFirstMove_RejectsPrematureBoardClearBeforeTheWholeSupplyIsPlaced()
        {
            var result = HintSolutionFinder.FindFirstMove(
                playableMask: new[,] { { 1 } },
                occupiedMask: new[,] { { 0 } },
                obstacleMask: new[,] { { 0 } },
                remainingBlockSchemas: new[]
                {
                    new[,] { { 1 } },
                    new[,] { { 1 } },
                    new[,] { { 1 } }
                },
                activeHandCount: 2,
                maxSearchNodes: 100);

            Assert.That(result.Status, Is.EqualTo(HintSolutionStatus.Infeasible));
            Assert.That(result.FirstMove, Is.Null);
        }

        [Test]
        public void FindFirstMove_DoesNotSkipActiveSupplyEvenWhenPreviewCanFit()
        {
            var result = HintSolutionFinder.FindFirstMove(
                playableMask: new[,] { { 1, 1 } },
                occupiedMask: new[,] { { 1, 0 } },
                obstacleMask: new[,] { { 0, 0 } },
                remainingBlockSchemas: new[]
                {
                    new[,] { { 1, 1 } },
                    new[,] { { 1 } }
                },
                activeHandCount: 1,
                maxSearchNodes: 100);

            Assert.That(result.Status, Is.EqualTo(HintSolutionStatus.Infeasible));
            Assert.That(result.FirstMove, Is.Null);

            var cannotSkip = HintSolutionFinder.FindFirstMove(
                playableMask: new[,] { { 1, 1 } },
                occupiedMask: new[,] { { 1, 0 } },
                obstacleMask: new[,] { { 0, 0 } },
                remainingBlockSchemas: new[]
                {
                    new[,] { { 1, 1 } },
                    new[,] { { 1, 1 } },
                    new[,] { { 1, 1 } },
                    new[,] { { 1 } }
                },
                activeHandCount: 2,
                maxSearchNodes: 100);

            Assert.That(cannotSkip.Status, Is.EqualTo(HintSolutionStatus.Infeasible));
        }

        [Test]
        public void FindFirstMove_CanCompleteBoardOnTheFinalSupplyPieceWithAnObstaclePresent()
        {
            var result = HintSolutionFinder.FindFirstMove(
                playableMask: new[,] { { 1, 1 } },
                occupiedMask: new[,] { { 1, 0 } },
                obstacleMask: new[,] { { 1, 0 } },
                remainingBlockSchemas: new[] { new[,] { { 1 } } },
                activeHandCount: 1,
                maxSearchNodes: 100);

            Assert.That(result.Status, Is.EqualTo(HintSolutionStatus.Feasible));
            Assert.That(result.FirstMove.Cells.Count, Is.EqualTo(1));
            Assert.That(result.FirstMove.Cells[0], Is.EqualTo(new QuackUp.Utils.TilingCell(0, 1)));
        }

        [Test]
        public void FindFirstMove_TreatsEveryTrayPieceAsSelectableBeyondTheLegacyHandLimit()
        {
            var result = HintSolutionFinder.FindFirstMove(
                playableMask: new[,] { { 1, 1, 1 } },
                occupiedMask: new[,] { { 0, 0, 0 } },
                obstacleMask: new[,] { { 0, 0, 0 } },
                remainingBlockSchemas: new[]
                {
                    new[,] { { 1 } },
                    new[,] { { 1 } },
                    new[,] { { 1 } }
                },
                activeHandCount: 3,
                maxSearchNodes: 100);

            Assert.That(result.Status, Is.EqualTo(HintSolutionStatus.Feasible));
            Assert.That(result.FirstMove, Is.Not.Null);
            Assert.That(result.FirstMove.RequiresSwap, Is.False);
        }

        [Test]
        public void FindFirstMove_DeduplicatesEquivalentTrayStatesWhenProvingInfeasible()
        {
            var result = HintSolutionFinder.FindFirstMove(
                playableMask: new[,] { { 1, 1, 1 } },
                occupiedMask: new[,] { { 0, 0, 0 } },
                obstacleMask: new[,] { { 0, 0, 0 } },
                remainingBlockSchemas: new[]
                {
                    new[,] { { 1 } },
                    new[,] { { 1 } },
                    new[,] { { 1 } },
                    new[,] { { 1 } }
                },
                activeHandCount: 4,
                maxSearchNodes: 1_000);

            Assert.That(result.Status, Is.EqualTo(HintSolutionStatus.Infeasible));
            Assert.That(result.NodesVisited, Is.LessThanOrEqualTo(10), $"Visited {result.NodesVisited} equivalent supply states.");
        }

        [Test]
        public void FindFirstMove_StopsWhenSnapshotSearchIsCancelled()
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Assert.Throws<System.OperationCanceledException>(() => HintSolutionFinder.FindFirstMove(
                playableMask: new[,] { { 1 } },
                occupiedMask: new[,] { { 0 } },
                obstacleMask: new[,] { { 0 } },
                remainingBlockSchemas: new[] { new[,] { { 1 } } },
                activeHandCount: 1,
                maxSearchNodes: 100,
                cancellationToken: cancellation.Token));
        }

        [Test]
        public void FindFirstMove_DoesNotConfuseBudgetExhaustionWithNoSolution()
        {
            var result = HintSolutionFinder.FindFirstMove(
                playableMask: new[,] { { 1, 1, 1 } },
                occupiedMask: new[,] { { 0, 0, 0 } },
                obstacleMask: new[,] { { 0, 0, 0 } },
                remainingBlockSchemas: new[]
                {
                    new[,] { { 1 } }, new[,] { { 1 } }, new[,] { { 1 } }
                },
                activeHandCount: 2,
                maxSearchNodes: 1);

            Assert.That(result.Status, Is.EqualTo(HintSolutionStatus.SearchLimitReached));
            Assert.That(result.FirstMove, Is.Null);
        }

        [Test]
        public void FindFirstMove_FindsTheUntouchedSeededPlaysetWithinBudget()
        {
            // Mirrors Shape Playset Generation Profile_Seed_-1029988589.asset.
            var playableMask = new int[7, 9];
            for (var row = 0; row < playableMask.GetLength(0); row++)
            for (var column = 0; column < playableMask.GetLength(1); column++)
                playableMask[row, column] = 1;

            var obstacleMask = new int[7, 9];
            obstacleMask[2, 6] = 1;
            obstacleMask[3, 1] = 1;
            obstacleMask[5, 6] = 1;

            var schemas = new System.Collections.Generic.List<int[,]>(16);
            for (var index = 0; index < 12; index++)
                schemas.Add(new[,] { { 0, 1, 0 }, { 1, 1, 1 } });
            for (var index = 0; index < 2; index++)
                schemas.Add(new[,] { { 1 }, { 1 }, { 1 } });
            schemas.Add(new[,] { { 1 }, { 1 } });
            schemas.Add(new[,] { { 0, 1 }, { 1, 1 }, { 1, 0 } });

            var result = HintSolutionFinder.FindFirstMove(
                playableMask,
                (int[,])obstacleMask.Clone(),
                obstacleMask,
                schemas,
                activeHandCount: schemas.Count,
                maxSearchNodes: 100_000);

            Assert.That(result.Status, Is.EqualTo(HintSolutionStatus.Feasible));
            Assert.That(result.FirstMove, Is.Not.Null);
            Assert.That(result.NodesVisited, Is.LessThan(10_000),
                $"The untouched seeded playset should solve well below its 100,000-node budget; visited {result.NodesVisited}.");
        }

        [Test]
        public void FindFirstMove_DoesNotAnchorWhenTheSupplyDoesNotCoverEveryFreeCell()
        {
            var result = HintSolutionFinder.FindFirstMove(
                playableMask: new[,] { { 1, 0, 1, 1 } },
                occupiedMask: new[,] { { 0, 0, 0, 0 } },
                obstacleMask: new[,] { { 0, 0, 0, 0 } },
                remainingBlockSchemas: new[] { new[,] { { 1, 1 } } },
                activeHandCount: 1,
                maxSearchNodes: 100);

            Assert.That(result.Status, Is.EqualTo(HintSolutionStatus.Feasible));
            Assert.That(result.FirstMove.Cells, Is.EquivalentTo(new[]
            {
                new QuackUp.Utils.TilingCell(0, 2),
                new QuackUp.Utils.TilingCell(0, 3)
            }));
        }
    }
}
