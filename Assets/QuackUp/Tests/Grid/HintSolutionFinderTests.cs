using System.Threading;
using FitMe.GameData;
using NUnit.Framework;
using UnityEditor;

namespace FitMe.Grid.Tests
{
    public sealed class HintSolutionFinderTests
    {
        [Test]
        public void FindFirstMove_RejectsPlacementOnlyPossibleAfterRotation()
        {
            var result = HintSolutionFinder.FindFirstMove(
                playableMask: new[,] { { 1 }, { 1 } },
                occupiedMask: new[,] { { 0 }, { 0 } },
                obstacleMask: new[,] { { 0 }, { 0 } },
                remainingBlockSchemas: new[] { new[,] { { 1, 1 } } },
                activeHandCount: 1,
                maxSearchNodes: 100);

            Assert.That(result.Status, Is.EqualTo(HintSolutionStatus.Infeasible));
        }

        [Test]
        public void FindFirstMove_UsesTheActualSingleBlockHandForTheWholeQueue()
        {
            var result = HintSolutionFinder.FindFirstMove(
                playableMask: new[,] { { 1, 1, 1 } },
                occupiedMask: new[,] { { 0, 0, 0 } },
                obstacleMask: new[,] { { 0, 0, 0 } },
                remainingBlockSchemas: new[]
                {
                    new[,] { { 1 } }, new[,] { { 1 } }, new[,] { { 1 } }
                },
                activeHandCount: 1,
                maxSearchNodes: 100);

            Assert.That(result.Status, Is.EqualTo(HintSolutionStatus.Feasible));
            Assert.That(result.FirstMove, Is.Not.Null);
            Assert.That(result.FirstMove.BlockIndex, Is.Zero);
            Assert.That(result.FirstMove.RequiresSwap, Is.False);
        }

        [Test]
        public void FindFirstMove_FindsTheFourPieceSupplyWhenAllTrayPiecesAreSelectable()
        {
            var result = HintSolutionFinder.FindFirstMove(
                playableMask: new[,] { { 1, 1, 1, 1 } },
                occupiedMask: new[,] { { 0, 0, 0, 0 } },
                obstacleMask: new[,] { { 0, 0, 0, 0 } },
                remainingBlockSchemas: new[]
                {
                    new[,] { { 1 } }, new[,] { { 1 } },
                    new[,] { { 1 } }, new[,] { { 1 } }
                },
                activeHandCount: 4,
                maxSearchNodes: 100);

            Assert.That(result.Status, Is.EqualTo(HintSolutionStatus.Feasible));
            Assert.That(result.FirstMove, Is.Not.Null);
            Assert.That(result.FirstMove.RequiresSwap, Is.False);
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
        [TestCase("Assets/QuackUp/Settings/ShapeMode/LevelShape/LevelDefinition/Shape Playset Generation Profile_Seed_162953893.asset")]
        [TestCase("Assets/QuackUp/Settings/ShapeMode/LevelShape/LevelDefinition/Shape Playset Generation Profile_Seed_-1029988589.asset")]
        public void FindFirstMove_FindsRegeneratedActivePlaysetWithinBudget(string assetPath)
        {
            var definition = AssetDatabase.LoadAssetAtPath<ShapeLevelDefinition>(assetPath);
            Assert.That(definition, Is.Not.Null, $"Expected active Shape level at {assetPath}.");
            var runtimeData = definition.ToRuntimeData();
            var playableMask = runtimeData.PlayableMask;
            var obstacleMask = new int[playableMask.GetLength(0), playableMask.GetLength(1)];
            foreach (var obstacle in runtimeData.Obstacles)
            {
                var schema = obstacle.Schema?.schema;
                if (schema == null) continue;
                for (var row = 0; row < schema.GetLength(0); row++)
                for (var column = 0; column < schema.GetLength(1); column++)
                {
                    if (schema[row, column] == 0) continue;
                    var boardRow = obstacle.Anchor.x + row;
                    var boardColumn = obstacle.Anchor.y + column;
                    obstacleMask[boardRow, boardColumn] = 1;
                }
            }
            var schemas = new System.Collections.Generic.List<int[,]>(runtimeData.PlayerBlockEntries.Count);
            foreach (var entry in runtimeData.PlayerBlockEntries) schemas.Add(entry.Schema);

            var result = HintSolutionFinder.FindFirstMove(
                playableMask,
                (int[,])obstacleMask.Clone(),
                obstacleMask,
                schemas,
                activeHandCount: schemas.Count,
                maxSearchNodes: 100_000);

            Assert.That(result.Status, Is.EqualTo(HintSolutionStatus.Feasible));
            Assert.That(result.FirstMove, Is.Not.Null);
            Assert.That(result.NodesVisited, Is.LessThan(100_000), $"Visited {result.NodesVisited} nodes.");
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
