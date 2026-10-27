using System.Collections.Generic;
using FitMe.GameData;
using FitMe.Grid;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FitMe.Grid.Tests
{
    public sealed class ShapePlaysetGeneratorTests
    {
        [Test]
        public void GenerateForBoard_PartitionsSuppliedPlayableCellsWithoutChangingBoardOrObstacles()
        {
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 2, maxBoardColumns: 2,
                minBoardRows: 2, maxBoardRows: 2,
                minObstacleCells: 0, maxObstacleCells: 0,
                minGeneratedBlockColumns: 1, maxGeneratedBlockColumns: 2,
                minGeneratedBlockRows: 1, maxGeneratedBlockRows: 2,
                minPlayableRegionCells: 1,
                maxTilingSearchNodes: 1000,
                maxCandidateAttempts: 1,
                maxSingletonFallbackBlocks: 4);
            var board = new[,] { { 1, 1 }, { 1, 1 } };
            var obstacles = new[,] { { 0, 0 }, { 0, 0 } };

            var result = ShapePlaysetGenerator.GenerateForBoard(config, seed: 17, board, obstacles);

            Assert.That(result.IsSuccess, Is.True, result.Error.ToString());
            Assert.That(result.Playset.BoardMask, Is.EqualTo(board));
            Assert.That(result.Playset.ObstacleMask, Is.EqualTo(obstacles));
            var covered = new int[2, 2];
            foreach (var block in result.Playset.PlayerBlocks)
            foreach (var cell in block.SolvedPlacement)
            {
                Assert.That(board[cell.x, cell.y], Is.EqualTo(1));
                Assert.That(obstacles[cell.x, cell.y], Is.Zero);
                Assert.That(covered[cell.x, cell.y], Is.Zero);
                covered[cell.x, cell.y] = 1;
            }
            Assert.That(covered, Is.EqualTo(board));
            var storedSchemas = new List<int[,]>();
            foreach (var block in result.Playset.PlayerBlocks)
            {
                var schema = block.Footprint;
                for (var turn = 0; turn < block.PlacementRotationQuarterTurns; turn++)
                {
                    var rotated = new int[schema.GetLength(1), schema.GetLength(0)];
                    for (var row = 0; row < schema.GetLength(0); row++)
                    for (var column = 0; column < schema.GetLength(1); column++)
                        rotated[column, schema.GetLength(0) - row - 1] = schema[row, column];
                    schema = rotated;
                }
                storedSchemas.Add(schema);
            }
            Assert.That(ShapeLevelTilingSolver.Solve(board, storedSchemas, 1000).Status,
                Is.EqualTo(ShapeLevelTilingStatus.Feasible),
                "Saved fixed-orientation footprints must tile without relying on gameplay rotation.");
        }

        [Test]
        public void GenerateForBoard_RejectsObstacleOnVoidWithoutMutatingInputs()
        {
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 2, maxBoardColumns: 2,
                minBoardRows: 2, maxBoardRows: 2,
                minObstacleCells: 0, maxObstacleCells: 0,
                minGeneratedBlockColumns: 1, maxGeneratedBlockColumns: 2,
                minGeneratedBlockRows: 1, maxGeneratedBlockRows: 2,
                minPlayableRegionCells: 1,
                maxTilingSearchNodes: 1000,
                maxCandidateAttempts: 1,
                maxSingletonFallbackBlocks: 4);
            var board = new[,] { { 0, 1 }, { 1, 1 } };
            var obstacles = new[,] { { 1, 0 }, { 0, 0 } };
            var originalBoard = (int[,])board.Clone();
            var originalObstacles = (int[,])obstacles.Clone();

            var result = ShapePlaysetGenerator.GenerateForBoard(config, seed: 17, board, obstacles);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.Not.EqualTo(ShapePlaysetGenerationError.None));
            Assert.That(board, Is.EqualTo(originalBoard));
            Assert.That(obstacles, Is.EqualTo(originalObstacles));
        }

        [Test]
        public void ValidateConfiguration_AcceptsBaseRectangleConfiguration()
        {
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 8, maxBoardColumns: 8,
                minBoardRows: 8, maxBoardRows: 8,
                minObstacleCells: 0, maxObstacleCells: 12,
                minGeneratedBlockColumns: 2, maxGeneratedBlockColumns: 2,
                minGeneratedBlockRows: 2, maxGeneratedBlockRows: 2);

            var validation = ShapePlaysetGenerator.ValidateConfiguration(config);

            Assert.That(validation.IsValid, Is.True, string.Join(", ", validation.Errors));
        }

        [Test]
        public void ValidateConfiguration_AcceptsStylePreferenceEndpoints()
        {
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 2, maxBoardColumns: 2,
                minBoardRows: 2, maxBoardRows: 2,
                minObstacleCells: 0, maxObstacleCells: 0,
                minGeneratedBlockColumns: 1, maxGeneratedBlockColumns: 2,
                minGeneratedBlockRows: 1, maxGeneratedBlockRows: 2,
                minPlayableRegionCells: 1,
                maxTilingSearchNodes: 1000,
                maxCandidateAttempts: 1,
                maxSingletonFallbackBlocks: 4,
                rectangularPreference: 0f,
                repetitionPenalty: 1f);

            var validation = ShapePlaysetGenerator.ValidateConfiguration(config);

            Assert.That(validation.IsValid, Is.True, string.Join(", ", validation.Errors));
        }

        [Test]
        public void ValidateConfiguration_RejectsNonFiniteOrOutOfRangeStylePreferences()
        {
            ShapePlaysetGenerationConfig CreateConfig(float rectangularPreference, float repetitionPenalty) =>
                new ShapePlaysetGenerationConfig(
                    2, 2, 2, 2, 0, 0, 1, 2, 1, 2,
                    minPlayableRegionCells: 1,
                    maxTilingSearchNodes: 1000,
                    maxCandidateAttempts: 1,
                    maxSingletonFallbackBlocks: 4,
                    rectangularPreference: rectangularPreference,
                    repetitionPenalty: repetitionPenalty);

            var invalidRectangle = ShapePlaysetGenerator.ValidateConfiguration(CreateConfig(-0.01f, 0.5f));
            var invalidRepetition = ShapePlaysetGenerator.ValidateConfiguration(CreateConfig(0.5f, float.NaN));

            Assert.That(invalidRectangle.Errors,
                Does.Contain(ShapePlaysetConfigurationError.InvalidShapeStylePreference));
            Assert.That(invalidRepetition.Errors,
                Does.Contain(ShapePlaysetConfigurationError.InvalidShapeStylePreference));
        }

        [Test]
        public void DifficultyEvaluator_ReducesProvisionalScoreAsSolutionMultiplicityIncreases()
        {
            var playableMask = new[,] { { 1, 1 }, { 1, 1 } };
            var blocks = new List<int[,]>
            {
                new[,] { { 1, 1 } },
                new[,] { { 1, 1 } }
            };
            var unique = ShapePlaysetDifficultyEvaluator.Evaluate(
                playableMask, obstacleCellCount: 0, voidCellCount: 0, blockFootprints: blocks,
                solutionCount: 1, solutionCountIsExact: true);
            var many = ShapePlaysetDifficultyEvaluator.Evaluate(
                playableMask, obstacleCellCount: 0, voidCellCount: 0, blockFootprints: blocks,
                solutionCount: 8, solutionCountIsExact: false);

            Assert.That(unique.ScoreModelVersion, Is.EqualTo(2));
            Assert.That(unique.HasSolutionCount, Is.True);
            Assert.That(unique.IsSolutionCountExact, Is.True);
            Assert.That(many.SolutionCount, Is.EqualTo(8));
            Assert.That(many.IsSolutionCountExact, Is.False);
            Assert.That(unique.ProvisionalDifficultyScore,
                Is.GreaterThan(many.ProvisionalDifficultyScore));
        }

        [Test]
        public void GenerationProfile_ExposesVoidCellRangeWithZeroDefaults()
        {
            var profile = ScriptableObject.CreateInstance<ShapePlaysetGenerationProfile>();
            try
            {
                var flags = System.Reflection.BindingFlags.Instance |
                            System.Reflection.BindingFlags.NonPublic;
                var minVoidField = typeof(ShapePlaysetGenerationProfile).GetField("_minVoidCells", flags);
                var maxVoidField = typeof(ShapePlaysetGenerationProfile).GetField("_maxVoidCells", flags);
                Assert.That(minVoidField, Is.Not.Null, "The profile should expose a minimum void-cell setting.");
                Assert.That(maxVoidField, Is.Not.Null, "The profile should expose a maximum void-cell setting.");

                var config = profile.CreateConfig();
                var minVoidProperty = config.GetType().GetProperty("MinVoidCells");
                var maxVoidProperty = config.GetType().GetProperty("MaxVoidCells");
                Assert.That(minVoidProperty, Is.Not.Null);
                Assert.That(maxVoidProperty, Is.Not.Null);
                Assert.That(minVoidProperty.GetValue(config), Is.EqualTo(0));
                Assert.That(maxVoidProperty.GetValue(config), Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void Generate_ProfileVoidSettingsProduceExactDeterministicHolesWithoutObstacleOverlap()
        {
            var profile = ScriptableObject.CreateInstance<ShapePlaysetGenerationProfile>();
            try
            {
                SetProfileInt(profile, "_minBoardColumns", 4);
                SetProfileInt(profile, "_maxBoardColumns", 4);
                SetProfileInt(profile, "_minBoardRows", 4);
                SetProfileInt(profile, "_maxBoardRows", 4);
                SetProfileInt(profile, "_minObstacleCells", 2);
                SetProfileInt(profile, "_maxObstacleCells", 2);
                SetProfileInt(profile, "_minVoidCells", 2);
                SetProfileInt(profile, "_maxVoidCells", 2);
                SetProfileInt(profile, "_minGeneratedBlockColumns", 1);
                SetProfileInt(profile, "_maxGeneratedBlockColumns", 1);
                SetProfileInt(profile, "_minGeneratedBlockRows", 1);
                SetProfileInt(profile, "_maxGeneratedBlockRows", 1);
                SetProfileInt(profile, "_minPlayableRegionCells", 1);
                SetProfileInt(profile, "_maxSingletonFallbackBlocks", 14);
                SetProfileInt(profile, "_maxCandidateAttempts", 1);

                var config = profile.CreateConfig();
                var first = ShapePlaysetGenerator.Generate(config, seed: 41);
                var second = ShapePlaysetGenerator.Generate(config, seed: 41);

                Assert.That(first.IsSuccess, Is.True, first.Error.ToString());
                Assert.That(second.IsSuccess, Is.True, second.Error.ToString());
                var board = first.Playset.BoardMask;
                var obstacles = first.Playset.ObstacleMask;
                var secondBoard = second.Playset.BoardMask;
                var secondObstacles = second.Playset.ObstacleMask;
                var voidCount = 0;
                var obstacleCount = 0;
                var playableCount = 0;
                for (var row = 0; row < board.GetLength(0); row++)
                for (var column = 0; column < board.GetLength(1); column++)
                {
                    Assert.That(board[row, column], Is.EqualTo(secondBoard[row, column]));
                    Assert.That(obstacles[row, column], Is.EqualTo(secondObstacles[row, column]));
                    if (board[row, column] == 0)
                    {
                        voidCount++;
                        Assert.That(obstacles[row, column], Is.Zero,
                            $"Void at ({row}, {column}) must not contain an obstacle.");
                    }
                    else if (obstacles[row, column] == 1)
                        obstacleCount++;
                    else
                        playableCount++;
                }

                Assert.That(voidCount, Is.EqualTo(2));
                Assert.That(obstacleCount, Is.EqualTo(2));
                Assert.That(playableCount, Is.EqualTo(12));
                Assert.That(first.Playset.Difficulty.PlayableCellCount, Is.EqualTo(playableCount));
                Assert.That(GetProperty(first.Playset.Difficulty, "VoidCellCount"), Is.EqualTo(voidCount));
                Assert.That(GetProperty(first.Playset.Configuration, "MinVoidCells"), Is.EqualTo(2));
                Assert.That(GetProperty(first.Playset.Configuration, "MaxVoidCells"), Is.EqualTo(2));
                var covered = new int[board.GetLength(0), board.GetLength(1)];
                var expectedPlayable = new int[board.GetLength(0), board.GetLength(1)];
                for (var row = 0; row < board.GetLength(0); row++)
                for (var column = 0; column < board.GetLength(1); column++)
                    expectedPlayable[row, column] = board[row, column] == 1 && obstacles[row, column] == 0 ? 1 : 0;
                for (var blockIndex = 0; blockIndex < first.Playset.PlayerBlocks.Count; blockIndex++)
                foreach (var cell in first.Playset.PlayerBlocks[blockIndex].SolvedPlacement)
                {
                    Assert.That(board[cell.x, cell.y], Is.EqualTo(1));
                    Assert.That(obstacles[cell.x, cell.y], Is.Zero);
                    Assert.That(covered[cell.x, cell.y], Is.Zero,
                        $"Playable cell ({cell.x}, {cell.y}) is covered more than once.");
                    covered[cell.x, cell.y] = 1;
                }
                for (var row = 0; row < board.GetLength(0); row++)
                for (var column = 0; column < board.GetLength(1); column++)
                    Assert.That(covered[row, column], Is.EqualTo(expectedPlayable[row, column]));
                var components = GetPlayableComponentSizes(board, obstacles);
                foreach (var componentSize in components)
                    Assert.That(componentSize, Is.GreaterThanOrEqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void Generate_SamplesVoidCountWithinInclusiveProfileBounds()
        {
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 4, maxBoardColumns: 4,
                minBoardRows: 4, maxBoardRows: 4,
                minObstacleCells: 0, maxObstacleCells: 0,
                minGeneratedBlockColumns: 1, maxGeneratedBlockColumns: 1,
                minGeneratedBlockRows: 1, maxGeneratedBlockRows: 1,
                minPlayableRegionCells: 1,
                maxCandidateAttempts: 1,
                maxSingletonFallbackBlocks: 15,
                minVoidCells: 1, maxVoidCells: 3);
            var sawMinimum = false;
            var sawMiddle = false;
            var sawMaximum = false;

            for (var seed = 0; seed < 32; seed++)
            {
                var result = ShapePlaysetGenerator.Generate(config, seed);
                Assert.That(result.IsSuccess, Is.True, $"seed={seed}; error={result.Error}");
                var board = result.Playset.BoardMask;
                var voidCount = board.Length - CountMaskCells(board);
                Assert.That(voidCount, Is.InRange(1, 3));
                sawMinimum |= voidCount == 1;
                sawMiddle |= voidCount == 2;
                sawMaximum |= voidCount == 3;
            }

            Assert.That(sawMinimum && sawMiddle && sawMaximum, Is.True,
                "Fixed seeds should demonstrate that both configured bounds and an intermediate count are selectable.");
        }

        [Test]
        public void ObstacleRepairRelocatesClearedObstacleOnlyToPlayableBoardCells()
        {
            var boardMask = new[,]
            {
                { 1, 1, 1 },
                { 1, 0, 1 },
                { 1, 1, 1 }
            };
            var obstacleMask = new[,]
            {
                { 0, 1, 0 },
                { 0, 0, 0 },
                { 0, 1, 0 }
            };
            var repairMethod = typeof(ShapePlaysetGenerator).GetMethod(
                "TryRepairObstacleMask",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.That(repairMethod, Is.Not.Null);
            var arguments = new object[] { boardMask, obstacleMask, 4, 50000, false };

            var repaired = (bool)repairMethod.Invoke(null, arguments);

            Assert.That(repaired, Is.True);
            Assert.That(obstacleMask[1, 1], Is.Zero, "Obstacle repair must never place an obstacle on a void.");
            Assert.That(CountMaskCells(obstacleMask), Is.EqualTo(2),
                "Repair must preserve the original obstacle count.");
            var components = GetPlayableComponentSizes(boardMask, obstacleMask);
            Assert.That(components, Has.Count.EqualTo(1));
            Assert.That(components[0], Is.EqualTo(6));
        }

        [Test]
        public void Generate_ObstacleRepairCannotJoinRegionsAcrossVoidCells()
        {
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 3, maxBoardColumns: 3,
                minBoardRows: 3, maxBoardRows: 3,
                minObstacleCells: 0, maxObstacleCells: 0,
                minGeneratedBlockColumns: 1, maxGeneratedBlockColumns: 1,
                minGeneratedBlockRows: 1, maxGeneratedBlockRows: 1,
                minPlayableRegionCells: 4,
                maxCandidateAttempts: 1,
                maxSingletonFallbackBlocks: 6,
                minVoidCells: 3, maxVoidCells: 3);

            var rejectedVoidBarrier = false;
            for (var seed = 0; seed < 512 && !rejectedVoidBarrier; seed++)
            {
                var result = ShapePlaysetGenerator.Generate(config, seed);
                rejectedVoidBarrier = result.LastAttemptError == ShapePlaysetGenerationError.ObstacleRepairFailed;
            }

            Assert.That(rejectedVoidBarrier, Is.True,
                "A void-separated board with undersized regions must not be treated as traversable during repair.");
        }

        [Test]
        public void Generate_ReturnsDeterministicConnectedExactCoverOnAcceptedBaseConfiguration()
        {
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 8, maxBoardColumns: 8,
                minBoardRows: 8, maxBoardRows: 8,
                minObstacleCells: 0, maxObstacleCells: 4,
                minGeneratedBlockColumns: 2, maxGeneratedBlockColumns: 2,
                minGeneratedBlockRows: 2, maxGeneratedBlockRows: 2);

            var validation = ShapePlaysetGenerator.ValidateConfiguration(config);
            Assert.That(validation.IsValid, Is.True, string.Join(", ", validation.Errors));

            var first = ShapePlaysetGenerator.Generate(config, seed: 17);
            var second = ShapePlaysetGenerator.Generate(config, seed: 17);

            Assert.That(first.IsSuccess, Is.True,
                $"{first.Error}; config={config.MinBoardColumns}x{config.MinBoardRows}, obstacles={config.MinObstacleCells}-{config.MaxObstacleCells}");
            Assert.That(second.IsSuccess, Is.True, second.Error.ToString());
            Assert.That(first.Playset.ObstacleMask, Is.EqualTo(second.Playset.ObstacleMask));
            Assert.That(first.Playset.PlayerBlocks.Count, Is.EqualTo(second.Playset.PlayerBlocks.Count));
            Assert.That(first.Playset.AttemptIndex, Is.EqualTo(second.Playset.AttemptIndex));
            Assert.That(first.AttemptsTried, Is.EqualTo(first.Playset.AttemptIndex + 1));
            Assert.That(first.Playset.Configuration.MinBoardColumns, Is.EqualTo(config.MinBoardColumns));
            Assert.That(first.Playset.Configuration.MaxObstacleCells, Is.EqualTo(config.MaxObstacleCells));
            Assert.That(first.Playset.GeneratorVersion, Is.EqualTo(GeneratedShapePlayset.CurrentGeneratorVersion));
            Assert.That(first.Playset.GeneratorVersion, Is.EqualTo(6));
            Assert.That(first.Playset.Difficulty.ScoreModelVersion,
                Is.EqualTo(ShapePlaysetDifficultyEvaluator.CurrentScoreModelVersion));
            for (var blockIndex = 0; blockIndex < first.Playset.PlayerBlocks.Count; blockIndex++)
                Assert.That(first.Playset.PlayerBlocks[blockIndex].Footprint,
                    Is.EqualTo(second.Playset.PlayerBlocks[blockIndex].Footprint));
            var boardMask = first.Playset.BoardMask;
            var obstacleMask = first.Playset.ObstacleMask;
            var covered = new int[boardMask.GetLength(0), boardMask.GetLength(1)];
            var obstacleCount = 0;
            for (var row = 0; row < boardMask.GetLength(0); row++)
            for (var column = 0; column < boardMask.GetLength(1); column++)
            {
                Assert.That(obstacleMask[row, column] == 0 || boardMask[row, column] == 1, Is.True);
                if (obstacleMask[row, column] != 0)
                {
                    obstacleCount++;
                    covered[row, column] = 1;
                }
            }
            Assert.That(obstacleCount, Is.InRange(config.MinObstacleCells, config.MaxObstacleCells));
            Assert.That(first.Playset.Difficulty.TotalLegalPlacements, Is.GreaterThan(0));
            Assert.That(first.Playset.Difficulty.MinimumLegalPlacementsPerBlock, Is.GreaterThan(0));
            for (var blockIndex = 0; blockIndex < first.Playset.PlayerBlocks.Count; blockIndex++)
            foreach (var cell in first.Playset.PlayerBlocks[blockIndex].SolvedPlacement)
            {
                Assert.That(cell.x, Is.InRange(0, boardMask.GetLength(0) - 1));
                Assert.That(cell.y, Is.InRange(0, boardMask.GetLength(1) - 1));
                Assert.That(boardMask[cell.x, cell.y], Is.EqualTo(1));
                Assert.That(covered[cell.x, cell.y], Is.Zero);
                covered[cell.x, cell.y] = 1;
            }
            for (var row = 0; row < boardMask.GetLength(0); row++)
            for (var column = 0; column < boardMask.GetLength(1); column++)
                Assert.That(covered[row, column], Is.EqualTo(boardMask[row, column]));
            AssertPlayableCellsConnected(boardMask, obstacleMask);

            foreach (var seed in new[] { int.MinValue, -1, 0, 1, int.MaxValue })
            {
                var seededResult = ShapePlaysetGenerator.Generate(config, seed);
                Assert.That(seededResult.IsSuccess, Is.True, $"seed={seed}; error={seededResult.Error}");
                AssertPlayableCellsConnected(seededResult.Playset.BoardMask, seededResult.Playset.ObstacleMask);
                Assert.That(seededResult.Playset.Difficulty.PlayerCellCount,
                    Is.EqualTo(seededResult.Playset.Difficulty.PlayableCellCount));
            }
        }

        private static void AssertPlayableCellsConnected(int[,] boardMask, int[,] obstacleMask)
        {
            var visited = new bool[boardMask.GetLength(0), boardMask.GetLength(1)];
            var queue = new System.Collections.Generic.Queue<Vector2Int>();
            var expected = 0;
            for (var row = 0; row < boardMask.GetLength(0); row++)
            for (var column = 0; column < boardMask.GetLength(1); column++)
            {
                if (boardMask[row, column] == 0 || obstacleMask[row, column] != 0) continue;
                expected++;
                if (queue.Count == 0)
                {
                    queue.Enqueue(new Vector2Int(row, column));
                    visited[row, column] = true;
                }
            }

            var reached = 0;
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                reached++;
                AddPlayableNeighbor(cell.x - 1, cell.y, boardMask, obstacleMask, visited, queue);
                AddPlayableNeighbor(cell.x + 1, cell.y, boardMask, obstacleMask, visited, queue);
                AddPlayableNeighbor(cell.x, cell.y - 1, boardMask, obstacleMask, visited, queue);
                AddPlayableNeighbor(cell.x, cell.y + 1, boardMask, obstacleMask, visited, queue);
            }
            Assert.That(reached, Is.EqualTo(expected));
        }

        private static void AddPlayableNeighbor(int row, int column, int[,] boardMask, int[,] obstacleMask,
            bool[,] visited, System.Collections.Generic.Queue<Vector2Int> queue)
        {
            if (row < 0 || row >= boardMask.GetLength(0) || column < 0 || column >= boardMask.GetLength(1) ||
                boardMask[row, column] == 0 || obstacleMask[row, column] != 0 || visited[row, column]) return;
            visited[row, column] = true;
            queue.Enqueue(new Vector2Int(row, column));
        }

        private static ShapePlaysetGenerationConfig CreateRegionThresholdConfig(
            int minimumRegionCells, int repairNodeBudget = 50000, int candidateAttempts = 8) =>
            new ShapePlaysetGenerationConfig(
                minBoardColumns: 3, maxBoardColumns: 3,
                minBoardRows: 2, maxBoardRows: 2,
                minObstacleCells: 2, maxObstacleCells: 2,
                minGeneratedBlockColumns: 1, maxGeneratedBlockColumns: 1,
                minGeneratedBlockRows: 1, maxGeneratedBlockRows: 1,
                minPlayableRegionCells: minimumRegionCells,
                maxObstacleRepairNodes: repairNodeBudget,
                maxCandidateAttempts: candidateAttempts,
                maxSingletonFallbackBlocks: 4);

        private static int FindSeedWithTwoThresholdSizedZones()
        {
            var config = CreateRegionThresholdConfig(minimumRegionCells: 2);
            for (var seed = 0; seed < 256; seed++)
            {
                var result = ShapePlaysetGenerator.Generate(config, seed);
                if (!result.IsSuccess) continue;
                var components = GetPlayableComponentSizes(
                    result.Playset.BoardMask, result.Playset.ObstacleMask);
                if (components.Count == 2 && components[0] == 2 && components[1] == 2)
                    return seed;
            }

            Assert.Fail("Expected a deterministic seed with two threshold-sized playzones.");
            return -1;
        }

        private static System.Collections.Generic.List<int> GetPlayableComponentSizes(
            int[,] boardMask, int[,] obstacleMask)
        {
            var components = new System.Collections.Generic.List<int>();
            var visited = new bool[boardMask.GetLength(0), boardMask.GetLength(1)];
            var queue = new System.Collections.Generic.Queue<Vector2Int>();
            for (var row = 0; row < boardMask.GetLength(0); row++)
            for (var column = 0; column < boardMask.GetLength(1); column++)
            {
                if (boardMask[row, column] == 0 || obstacleMask[row, column] != 0 || visited[row, column])
                    continue;
                var componentSize = 0;
                visited[row, column] = true;
                queue.Enqueue(new Vector2Int(row, column));
                while (queue.Count > 0)
                {
                    var cell = queue.Dequeue();
                    componentSize++;
                    AddPlayableNeighbor(cell.x - 1, cell.y, boardMask, obstacleMask, visited, queue);
                    AddPlayableNeighbor(cell.x + 1, cell.y, boardMask, obstacleMask, visited, queue);
                    AddPlayableNeighbor(cell.x, cell.y - 1, boardMask, obstacleMask, visited, queue);
                    AddPlayableNeighbor(cell.x, cell.y + 1, boardMask, obstacleMask, visited, queue);
                }
                components.Add(componentSize);
            }
            return components;
        }

        [Test]
        public void ValidateConfiguration_RejectsInvalidObstacleRangeAsConfigurationError()
        {
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 8, maxBoardColumns: 8,
                minBoardRows: 8, maxBoardRows: 8,
                minObstacleCells: 64, maxObstacleCells: 64,
                minGeneratedBlockColumns: 2, maxGeneratedBlockColumns: 2,
                minGeneratedBlockRows: 2, maxGeneratedBlockRows: 2);

            var validation = ShapePlaysetGenerator.ValidateConfiguration(config);

            Assert.That(validation.IsValid, Is.False);
            Assert.That(validation.Errors, Does.Contain(ShapePlaysetConfigurationError.InvalidObstacleRange));
        }

        [Test]
        public void ValidateConfiguration_RejectsObstacleMaximumThatCannotFitLargestBoard()
        {
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 4, maxBoardColumns: 4,
                minBoardRows: 4, maxBoardRows: 4,
                minObstacleCells: 0, maxObstacleCells: 16,
                minGeneratedBlockColumns: 1, maxGeneratedBlockColumns: 2,
                minGeneratedBlockRows: 1, maxGeneratedBlockRows: 2);

            var validation = ShapePlaysetGenerator.ValidateConfiguration(config);

            Assert.That(validation.IsValid, Is.False);
            Assert.That(validation.Errors, Does.Contain(ShapePlaysetConfigurationError.InvalidObstacleRange));
        }

        [Test]
        public void ValidateConfiguration_RejectsInvalidVoidRangeAsConfigurationError()
        {
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 4, maxBoardColumns: 4,
                minBoardRows: 4, maxBoardRows: 4,
                minObstacleCells: 0, maxObstacleCells: 0,
                minGeneratedBlockColumns: 1, maxGeneratedBlockColumns: 2,
                minGeneratedBlockRows: 1, maxGeneratedBlockRows: 2,
                minVoidCells: -1, maxVoidCells: 0);

            var validation = ShapePlaysetGenerator.ValidateConfiguration(config);

            Assert.That(validation.IsValid, Is.False);
            Assert.That(validation.Errors, Does.Contain(ShapePlaysetConfigurationError.InvalidVoidRange));
        }

        [Test]
        public void ValidateConfiguration_RejectsVoidMaximumThatCannotFitLargestBoard()
        {
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 4, maxBoardColumns: 4,
                minBoardRows: 4, maxBoardRows: 4,
                minObstacleCells: 0, maxObstacleCells: 0,
                minGeneratedBlockColumns: 1, maxGeneratedBlockColumns: 2,
                minGeneratedBlockRows: 1, maxGeneratedBlockRows: 2,
                minVoidCells: 0, maxVoidCells: 16);

            var validation = ShapePlaysetGenerator.ValidateConfiguration(config);

            Assert.That(validation.IsValid, Is.False);
            Assert.That(validation.Errors, Does.Contain(ShapePlaysetConfigurationError.InvalidVoidRange));
        }

        [Test]
        public void ValidateConfiguration_RejectsMinimumVoidAndObstacleCountsThatConsumeLargestBoard()
        {
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 4, maxBoardColumns: 4,
                minBoardRows: 4, maxBoardRows: 4,
                minObstacleCells: 1, maxObstacleCells: 1,
                minGeneratedBlockColumns: 1, maxGeneratedBlockColumns: 2,
                minGeneratedBlockRows: 1, maxGeneratedBlockRows: 2,
                minVoidCells: 15, maxVoidCells: 15);

            var validation = ShapePlaysetGenerator.ValidateConfiguration(config);

            Assert.That(validation.IsValid, Is.False);
            Assert.That(validation.Errors, Does.Contain(ShapePlaysetConfigurationError.InvalidCombinedCellRange));
        }

        [Test]
        public void ValidateConfiguration_AcceptsVoidCountThatFitsLargerConfiguredBoard()
        {
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 3, maxBoardColumns: 4,
                minBoardRows: 3, maxBoardRows: 4,
                minObstacleCells: 0, maxObstacleCells: 0,
                minGeneratedBlockColumns: 1, maxGeneratedBlockColumns: 2,
                minGeneratedBlockRows: 1, maxGeneratedBlockRows: 2,
                minVoidCells: 9, maxVoidCells: 9);

            var validation = ShapePlaysetGenerator.ValidateConfiguration(config);

            Assert.That(validation.IsValid, Is.True, string.Join(", ", validation.Errors));
        }

        [Test]
        public void ValidateConfiguration_AcceptsObstacleCountThatFitsLargerConfiguredBoard()
        {
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 3, maxBoardColumns: 4,
                minBoardRows: 3, maxBoardRows: 4,
                minObstacleCells: 9, maxObstacleCells: 9,
                minGeneratedBlockColumns: 1, maxGeneratedBlockColumns: 2,
                minGeneratedBlockRows: 1, maxGeneratedBlockRows: 2);

            var validation = ShapePlaysetGenerator.ValidateConfiguration(config);

            Assert.That(validation.IsValid, Is.True, string.Join(", ", validation.Errors));
            Assert.That(validation.FeasibilityStatus, Is.EqualTo(ShapePlaysetFeasibilityStatus.Unproven));
        }

        [Test]
        public void ValidateConfiguration_RejectsNonPositivePlayableRegionThreshold()
        {
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 4, maxBoardColumns: 4,
                minBoardRows: 4, maxBoardRows: 4,
                minObstacleCells: 0, maxObstacleCells: 3,
                minGeneratedBlockColumns: 1, maxGeneratedBlockColumns: 2,
                minGeneratedBlockRows: 1, maxGeneratedBlockRows: 2,
                minPlayableRegionCells: 0);

            var validation = ShapePlaysetGenerator.ValidateConfiguration(config);

            Assert.That(validation.IsValid, Is.False);
            Assert.That(validation.Errors,
                Does.Contain(ShapePlaysetConfigurationError.InvalidPlayableRegionThreshold));
        }

        [Test]
        public void ValidateConfiguration_ReportsUnprovenTilingFeasibilitySeparately()
        {
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 8, maxBoardColumns: 8,
                minBoardRows: 7, maxBoardRows: 7,
                minObstacleCells: 0, maxObstacleCells: 4,
                minGeneratedBlockColumns: 2, maxGeneratedBlockColumns: 2,
                minGeneratedBlockRows: 2, maxGeneratedBlockRows: 2);

            var validation = ShapePlaysetGenerator.ValidateConfiguration(config);

            Assert.That(validation.IsValid, Is.True, string.Join(", ", validation.Errors));
            Assert.That(validation.FeasibilityStatus, Is.EqualTo(ShapePlaysetFeasibilityStatus.Unproven));
            var result = ShapePlaysetGenerator.Generate(config, seed: 17);
            Assert.That(result.Error, Is.Not.EqualTo(ShapePlaysetGenerationError.InvalidConfiguration));
        }

        [Test]
        public void Generate_RetriesCandidateSeedsAndReportsTheFinalCandidateCause()
        {
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 3, maxBoardColumns: 3,
                minBoardRows: 3, maxBoardRows: 3,
                minObstacleCells: 0, maxObstacleCells: 0,
                minGeneratedBlockColumns: 2, maxGeneratedBlockColumns: 2,
                minGeneratedBlockRows: 1, maxGeneratedBlockRows: 1,
                maxCandidateAttempts: 2);

            var first = ShapePlaysetGenerator.Generate(config, seed: 31);
            var second = ShapePlaysetGenerator.Generate(config, seed: 31);

            Assert.That(first.Error, Is.EqualTo(ShapePlaysetGenerationError.CandidateRetriesExhausted));
            Assert.That(first.AttemptsTried, Is.EqualTo(config.MaxCandidateAttempts));
            Assert.That(first.LastAttemptError, Is.EqualTo(ShapePlaysetGenerationError.TilingFailed));
            Assert.That(first.CandidateFailures.TilingFailures, Is.EqualTo(config.MaxCandidateAttempts));
            Assert.That(first.CandidateFailures.TilingBudgetExhaustions, Is.Zero);
            Assert.That(second.Error, Is.EqualTo(first.Error));
            Assert.That(second.AttemptsTried, Is.EqualTo(first.AttemptsTried));
            Assert.That(second.LastAttemptError, Is.EqualTo(first.LastAttemptError));
            Assert.That(second.CandidateFailures.TilingFailures,
                Is.EqualTo(first.CandidateFailures.TilingFailures));
        }

        [Test]
        public void Generate_ReportsTilingBudgetExhaustionDistinctFromNoTiling()
        {
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 2, maxBoardColumns: 2,
                minBoardRows: 2, maxBoardRows: 2,
                minObstacleCells: 0, maxObstacleCells: 0,
                minGeneratedBlockColumns: 1, maxGeneratedBlockColumns: 2,
                minGeneratedBlockRows: 1, maxGeneratedBlockRows: 2,
                maxTilingSearchNodes: 1,
                maxCandidateAttempts: 2);

            var result = ShapePlaysetGenerator.Generate(config, seed: 7);

            Assert.That(result.Error, Is.EqualTo(ShapePlaysetGenerationError.CandidateRetriesExhausted));
            Assert.That(result.LastAttemptError, Is.EqualTo(ShapePlaysetGenerationError.TilingBudgetExceeded));
            Assert.That(result.AttemptsTried, Is.EqualTo(2));
            Assert.That(result.CandidateFailures.TilingBudgetExhaustions, Is.EqualTo(2));
            Assert.That(result.CandidateFailures.TilingFailures, Is.Zero);
            Assert.That(result.ConfigurationValidation.FeasibilityStatus,
                Is.EqualTo(ShapePlaysetFeasibilityStatus.Unproven));
        }

        [Test]
        public void Generate_Failure2ProfileSeedProducesAUsablePlayset()
        {
            var preferredPresets = new[]
            {
                AssetDatabase.LoadAssetAtPath<BlockPreset>(
                    "Assets/QuackUp/Settings/Grid/Block/BlockPreset/1x2.asset"),
                AssetDatabase.LoadAssetAtPath<BlockPreset>(
                    "Assets/QuackUp/Settings/Grid/Block/BlockPreset/1x3.asset"),
                AssetDatabase.LoadAssetAtPath<BlockPreset>(
                    "Assets/QuackUp/Settings/Grid/Block/BlockPreset/S.asset")
            };
            foreach (var preset in preferredPresets) Assert.That(preset, Is.Not.Null);
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 6, maxBoardColumns: 10,
                minBoardRows: 6, maxBoardRows: 10,
                minObstacleCells: 1, maxObstacleCells: 8,
                minGeneratedBlockColumns: 1, maxGeneratedBlockColumns: 4,
                minGeneratedBlockRows: 1, maxGeneratedBlockRows: 4,
                preferredPlayerBlocks: new[]
                {
                    new ShapePlayerBlockEntry(preferredPresets[0], 1),
                    new ShapePlayerBlockEntry(preferredPresets[1], 1),
                    new ShapePlayerBlockEntry(preferredPresets[2], 1)
                },
                minPlayableRegionCells: 10,
                maxObstacleRepairNodes: 50000,
                maxTilingSearchNodes: 100000,
                maxCandidateAttempts: 16,
                maxSingletonFallbackBlocks: 20);

            var result = ShapePlaysetGenerator.Generate(config, seed: -215018798);

            Assert.That(result.IsSuccess, Is.True,
                $"Expected a usable playset for the Failure2 profile seed; error={result.Error}, " +
                $"last={result.LastAttemptError}, tiling budget exhaustions=" +
                $"{result.CandidateFailures.TilingBudgetExhaustions}.");
            Assert.That(result.Playset.Difficulty.PlayerCellCount,
                Is.EqualTo(result.Playset.Difficulty.PlayableCellCount),
                "A budget-limited result must still contain an exact, usable tiling.");
            var optimizationStatus = typeof(GeneratedShapePlayset).GetProperty("OptimizationStatus");
            Assert.That(optimizationStatus, Is.Not.Null,
                "A usable best-found layout must disclose whether preferred coverage was optimized fully.");
            Assert.That(optimizationStatus.GetValue(result.Playset).ToString(), Is.EqualTo("BudgetLimited"));
        }

        [Test]
        public void Generate_ReportsRotationForFallbackGeneratedInSwappedOrientation()
        {
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 1, maxBoardColumns: 1,
                minBoardRows: 2, maxBoardRows: 2,
                minObstacleCells: 0, maxObstacleCells: 0,
                minGeneratedBlockColumns: 2, maxGeneratedBlockColumns: 2,
                minGeneratedBlockRows: 1, maxGeneratedBlockRows: 1);

            var result = ShapePlaysetGenerator.Generate(config, seed: 13);

            Assert.That(result.IsSuccess, Is.True, result.Error.ToString());
            Assert.That(result.Playset.PlayerBlocks, Has.Count.EqualTo(1));
            var block = result.Playset.PlayerBlocks[0];
            Assert.That(block.Footprint.GetLength(0), Is.EqualTo(1));
            Assert.That(block.Footprint.GetLength(1), Is.EqualTo(2));
            Assert.That(block.SolvedPlacement, Is.EqualTo(new[] { new Vector2Int(0, 0), new Vector2Int(1, 0) }));
            Assert.That(block.PlacementRotationQuarterTurns, Is.EqualTo(3));
        }

        [Test]
        public void Generate_ReportsRotationAppliedToPreferredPresetPlacement()
        {
            var preset = CreatePreset(new[,] { { 1, 1 } });
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 1, maxBoardColumns: 1,
                minBoardRows: 2, maxBoardRows: 2,
                minObstacleCells: 0, maxObstacleCells: 0,
                minGeneratedBlockColumns: 2, maxGeneratedBlockColumns: 2,
                minGeneratedBlockRows: 1, maxGeneratedBlockRows: 1,
                preferredPlayerBlocks: new[] { new ShapePlayerBlockEntry(preset, 1) });

            try
            {
                var result = ShapePlaysetGenerator.Generate(config, seed: 19);

                Assert.That(result.IsSuccess, Is.True, result.Error.ToString());
                Assert.That(result.Playset.PlayerBlocks, Has.Count.EqualTo(1));
                var block = result.Playset.PlayerBlocks[0];
                Assert.That(block.IsPredefined, Is.True);
                Assert.That(block.SolvedPlacement,
                    Is.EqualTo(new[] { new Vector2Int(0, 0), new Vector2Int(1, 0) }));
                Assert.That(block.PlacementRotationQuarterTurns, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(preset);
            }
        }

        [Test]
        public void SetGeneratedContent_StoresSolvedRotationInAuthoredFootprint()
        {
            var preset = CreatePreset(new[,] { { 1, 1 } });
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 1, maxBoardColumns: 1,
                minBoardRows: 2, maxBoardRows: 2,
                minObstacleCells: 0, maxObstacleCells: 0,
                minGeneratedBlockColumns: 2, maxGeneratedBlockColumns: 2,
                minGeneratedBlockRows: 1, maxGeneratedBlockRows: 1,
                preferredPlayerBlocks: new[] { new ShapePlayerBlockEntry(preset, 1) });
            var level = ScriptableObject.CreateInstance<ShapeLevelDefinition>();

            try
            {
                var generated = ShapePlaysetGenerator.Generate(config, seed: 19);
                Assert.That(generated.IsSuccess, Is.True, generated.Error.ToString());
                Assert.That(generated.Playset.PlayerBlocks[0].PlacementRotationQuarterTurns, Is.EqualTo(1));

                level.SetGeneratedContent(generated.Playset);

                Assert.That(level.PlayerBlocks, Has.Count.EqualTo(1));
                var entry = level.PlayerBlocks[0];
                Assert.That(entry.Source, Is.EqualTo(ShapePlayerBlockSource.InlineFootprint));
                Assert.That(entry.AppearancePreset, Is.SameAs(preset), "Keep preset appearance for rotated inline geometry.");
                Assert.That(entry.InlineFootprint, Is.EqualTo(new[,] { { 1 }, { 1 } }));

                var runtimeEntry = level.ToRuntimeData().PlayerBlockEntries[0];
                Assert.That(runtimeEntry.Preset, Is.SameAs(preset));
                Assert.That(runtimeEntry.Schema, Is.EqualTo(new[,] { { 1 }, { 1 } }));
                var tiling = ShapeLevelTilingSolver.Solve(
                    generated.Playset.BoardMask,
                    new[] { runtimeEntry.Schema },
                    maxSearchNodes: 100);
                Assert.That(tiling.IsFeasible, Is.True,
                    "The authored runtime footprint must tile without player rotation.");
            }
            finally
            {
                Object.DestroyImmediate(level);
                Object.DestroyImmediate(preset);
            }
        }

        [Test]
        public void Generate_RespectsNonzeroObstacleMinimumWithConstructiveFallback()
        {
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 8, maxBoardColumns: 8,
                minBoardRows: 8, maxBoardRows: 8,
                minObstacleCells: 4, maxObstacleCells: 8,
                minGeneratedBlockColumns: 2, maxGeneratedBlockColumns: 2,
                minGeneratedBlockRows: 2, maxGeneratedBlockRows: 2);

            var result = ShapePlaysetGenerator.Generate(config, seed: int.MinValue);

            Assert.That(result.IsSuccess, Is.True, result.Error.ToString());
            var obstacleMask = result.Playset.ObstacleMask;
            var obstacleCount = 0;
            foreach (var value in obstacleMask) if (value != 0) obstacleCount++;
            Assert.That(obstacleCount, Is.InRange(config.MinObstacleCells, config.MaxObstacleCells));
            AssertPlayableCellsConnected(result.Playset.BoardMask, obstacleMask);
            Assert.That(result.Playset.Difficulty.PlayerCellCount,
                Is.EqualTo(result.Playset.Difficulty.PlayableCellCount));
        }

        [Test]
        public void Generate_SamplesArbitraryObstacleCellsDeterministically()
        {
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 4, maxBoardColumns: 4,
                minBoardRows: 4, maxBoardRows: 4,
                minObstacleCells: 1, maxObstacleCells: 1,
                minGeneratedBlockColumns: 1, maxGeneratedBlockColumns: 1,
                minGeneratedBlockRows: 1, maxGeneratedBlockRows: 1,
                maxSingletonFallbackBlocks: 15);

            var obstacleLocations = new System.Collections.Generic.HashSet<string>();
            for (var seed = -8; seed <= 8; seed++)
            {
                var first = ShapePlaysetGenerator.Generate(config, seed);
                var second = ShapePlaysetGenerator.Generate(config, seed);

                Assert.That(first.IsSuccess, Is.True, $"seed={seed}; error={first.Error}");
                Assert.That(second.IsSuccess, Is.True, $"seed={seed}; error={second.Error}");
                Assert.That(first.Playset.ObstacleMask, Is.EqualTo(second.Playset.ObstacleMask));
                Assert.That(CountMaskCells(first.Playset.ObstacleMask), Is.EqualTo(1));
                AssertPlayableCellsConnected(first.Playset.BoardMask, first.Playset.ObstacleMask);
                var mask = first.Playset.ObstacleMask;
                for (var row = 0; row < mask.GetLength(0); row++)
                for (var column = 0; column < mask.GetLength(1); column++)
                    if (mask[row, column] != 0) obstacleLocations.Add($"{row}:{column}");
            }

            Assert.That(obstacleLocations.Count, Is.GreaterThan(1));
        }

        [Test]
        public void Generate_ChoosesObstacleCountWithinConfiguredRange()
        {
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 4, maxBoardColumns: 4,
                minBoardRows: 4, maxBoardRows: 4,
                minObstacleCells: 1, maxObstacleCells: 3,
                minGeneratedBlockColumns: 1, maxGeneratedBlockColumns: 1,
                minGeneratedBlockRows: 1, maxGeneratedBlockRows: 1,
                maxSingletonFallbackBlocks: 15);

            var selectedCounts = new System.Collections.Generic.HashSet<int>();
            for (var seed = -8; seed <= 8; seed++)
            {
                var result = ShapePlaysetGenerator.Generate(config, seed);

                Assert.That(result.IsSuccess, Is.True, $"seed={seed}; error={result.Error}");
                var obstacleCount = CountMaskCells(result.Playset.ObstacleMask);
                Assert.That(obstacleCount, Is.InRange(config.MinObstacleCells, config.MaxObstacleCells));
                selectedCounts.Add(obstacleCount);
            }

            Assert.That(selectedCounts.Count, Is.GreaterThan(1));
        }

        [Test]
        public void Generate_RepairsUndersizedFourWayPlayableRegions()
        {
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 4, maxBoardColumns: 4,
                minBoardRows: 4, maxBoardRows: 4,
                minObstacleCells: 1, maxObstacleCells: 3,
                minGeneratedBlockColumns: 1, maxGeneratedBlockColumns: 1,
                minGeneratedBlockRows: 1, maxGeneratedBlockRows: 1,
                maxSingletonFallbackBlocks: 15);

            for (var seed = 0; seed < 32; seed++)
            {
                var result = ShapePlaysetGenerator.Generate(config, seed);

                Assert.That(result.IsSuccess, Is.True, $"seed={seed}; error={result.Error}");
                Assert.That(CountMaskCells(result.Playset.ObstacleMask),
                    Is.InRange(config.MinObstacleCells, config.MaxObstacleCells));
                AssertPlayableCellsConnected(result.Playset.BoardMask, result.Playset.ObstacleMask);
            }
        }

        [Test]
        public void Generate_AllowsSeparatedPlayableRegionsWhenEachMeetsTheThreshold()
        {
            var seed = FindSeedWithTwoThresholdSizedZones();
            var config = CreateRegionThresholdConfig(minimumRegionCells: 2);

            var result = ShapePlaysetGenerator.Generate(config, seed);

            Assert.That(result.IsSuccess, Is.True, result.Error.ToString());
            Assert.That(GetPlayableComponentSizes(
                result.Playset.BoardMask, result.Playset.ObstacleMask),
                Is.EqualTo(new[] { 2, 2 }));
            Assert.That(CountMaskCells(result.Playset.ObstacleMask), Is.EqualTo(2));
        }

        [Test]
        public void Generate_ReportsObstacleRepairBudgetExhaustionForUndersizedRegions()
        {
            var seed = FindSeedWithTwoThresholdSizedZones();
            var boundedConfig = CreateRegionThresholdConfig(
                minimumRegionCells: 3, repairNodeBudget: 1, candidateAttempts: 1);

            var bounded = ShapePlaysetGenerator.Generate(boundedConfig, seed);

            Assert.That(bounded.Error, Is.EqualTo(ShapePlaysetGenerationError.CandidateRetriesExhausted));
            Assert.That(bounded.LastAttemptError,
                Is.EqualTo(ShapePlaysetGenerationError.ObstacleRepairBudgetExceeded));
            Assert.That(bounded.CandidateFailures.ObstacleRepairBudgetExhaustions, Is.EqualTo(1));

            var repairableConfig = CreateRegionThresholdConfig(minimumRegionCells: 3);
            var repaired = ShapePlaysetGenerator.Generate(repairableConfig, seed);

            Assert.That(repaired.IsSuccess, Is.True, repaired.Error.ToString());
            AssertPlayableCellsConnected(repaired.Playset.BoardMask, repaired.Playset.ObstacleMask);
            Assert.That(CountMaskCells(repaired.Playset.ObstacleMask), Is.EqualTo(2));
        }

        [Test]
        public void Generate_UsesFourConnectedIrregularFallbackToCoverObstacleGaps()
        {
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 4, maxBoardColumns: 4,
                minBoardRows: 4, maxBoardRows: 4,
                minObstacleCells: 1, maxObstacleCells: 1,
                minGeneratedBlockColumns: 2, maxGeneratedBlockColumns: 2,
                minGeneratedBlockRows: 2, maxGeneratedBlockRows: 2,
                rectangularPreference: 0.5f, repetitionPenalty: 0f);

            var result = ShapePlaysetGenerator.Generate(config, seed: 5);

            Assert.That(result.IsSuccess, Is.True, result.Error.ToString());
            Assert.That(result.Playset.UsedPredefinedBlocks, Is.False);
            var irregularBlockCount = 0;
            for (var index = 0; index < result.Playset.PlayerBlocks.Count; index++)
            {
                var footprint = result.Playset.PlayerBlocks[index].Footprint;
                Assert.That(footprint.GetLength(0), Is.EqualTo(2));
                Assert.That(footprint.GetLength(1), Is.EqualTo(2));
                AssertFootprintFourConnected(footprint);
                if (CountMaskCells(footprint) == 3) irregularBlockCount++;
            }
            Assert.That(irregularBlockCount, Is.EqualTo(1));
        }

        private static void AssertFootprintFourConnected(int[,] footprint)
        {
            var visited = new bool[footprint.GetLength(0), footprint.GetLength(1)];
            var queue = new System.Collections.Generic.Queue<Vector2Int>();
            var expected = CountMaskCells(footprint);
            var startFound = false;
            for (var row = 0; row < footprint.GetLength(0) && !startFound; row++)
            for (var column = 0; column < footprint.GetLength(1); column++)
                if (footprint[row, column] != 0)
                {
                    visited[row, column] = true;
                    queue.Enqueue(new Vector2Int(row, column));
                    startFound = true;
                    break;
                }

            var reached = 0;
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                reached++;
                AddFootprintNeighbor(cell.x - 1, cell.y, footprint, visited, queue);
                AddFootprintNeighbor(cell.x + 1, cell.y, footprint, visited, queue);
                AddFootprintNeighbor(cell.x, cell.y - 1, footprint, visited, queue);
                AddFootprintNeighbor(cell.x, cell.y + 1, footprint, visited, queue);
            }
            Assert.That(reached, Is.EqualTo(expected));
        }

        private static void AddFootprintNeighbor(
            int row, int column, int[,] footprint, bool[,] visited,
            System.Collections.Generic.Queue<Vector2Int> queue)
        {
            if (row < 0 || row >= footprint.GetLength(0) ||
                column < 0 || column >= footprint.GetLength(1) ||
                footprint[row, column] == 0 || visited[row, column]) return;
            visited[row, column] = true;
            queue.Enqueue(new Vector2Int(row, column));
        }

        private static int CountMaskCells(int[,] mask)
        {
            var count = 0;
            foreach (var value in mask) if (value != 0) count++;
            return count;
        }

        [Test]
        public void Generate_UsesPreferredPresetWhenItTilesTheGeneratedBoard()
        {
            var preset = ScriptableObject.CreateInstance<BlockPreset>();
            typeof(BlockSchema).GetField("schema").SetValue(
                typeof(BlockPreset).GetProperty(nameof(BlockPreset.BlockSchema)).GetValue(preset),
                new[,] { { 1, 1 }, { 1, 1 } });
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 2, maxBoardColumns: 2,
                minBoardRows: 2, maxBoardRows: 2,
                minObstacleCells: 0, maxObstacleCells: 0,
                minGeneratedBlockColumns: 1, maxGeneratedBlockColumns: 2,
                minGeneratedBlockRows: 1, maxGeneratedBlockRows: 2,
                preferredPlayerBlocks: new[] { new ShapePlayerBlockEntry(preset, 1) });

            try
            {
                var result = ShapePlaysetGenerator.Generate(config, seed: 9);

                Assert.That(result.IsSuccess, Is.True, result.Error.ToString());
                Assert.That(result.Playset.UsedPredefinedBlocks, Is.True);
                Assert.That(result.Playset.PlayerBlocks, Has.Count.EqualTo(1));
                Assert.That(result.Playset.PlayerBlocks[0].PreferredPreset, Is.SameAs(preset));
                Assert.That(result.Playset.Configuration.PreferredPlayerBlocks, Has.Count.EqualTo(1));
                Assert.That(result.Playset.Configuration.PreferredPlayerBlocks[0].PresetId,
                    Is.EqualTo(preset.Id ?? string.Empty));
                Assert.That(result.Playset.Configuration.PreferredPlayerBlocks[0].Count, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(preset);
            }
        }

        [Test]
        public void ValidateConfiguration_RejectsDiagonalOnlyPreferredPreset()
        {
            var preset = ScriptableObject.CreateInstance<BlockPreset>();
            typeof(BlockSchema).GetField("schema").SetValue(
                typeof(BlockPreset).GetProperty(nameof(BlockPreset.BlockSchema)).GetValue(preset),
                new[,] { { 1, 0 }, { 0, 1 } });
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 2, maxBoardColumns: 2,
                minBoardRows: 2, maxBoardRows: 2,
                minObstacleCells: 0, maxObstacleCells: 0,
                minGeneratedBlockColumns: 1, maxGeneratedBlockColumns: 2,
                minGeneratedBlockRows: 1, maxGeneratedBlockRows: 2,
                preferredPlayerBlocks: new[] { new ShapePlayerBlockEntry(preset, 1) });

            try
            {
                var validation = ShapePlaysetGenerator.ValidateConfiguration(config);

                Assert.That(validation.IsValid, Is.False);
                Assert.That(validation.Errors,
                    Does.Contain(ShapePlaysetConfigurationError.InvalidPreferredBlockSupply));
            }
            finally
            {
                Object.DestroyImmediate(preset);
            }
        }

        [Test]
        public void ValidateConfiguration_RejectsInvalidValuesOutsidePreferredPresetFootprint()
        {
            var preset = CreatePreset(new[,] { { 1, 0 }, { 0, 2 } });
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 1, maxBoardColumns: 1,
                minBoardRows: 1, maxBoardRows: 1,
                minObstacleCells: 0, maxObstacleCells: 0,
                minGeneratedBlockColumns: 1, maxGeneratedBlockColumns: 1,
                minGeneratedBlockRows: 1, maxGeneratedBlockRows: 1,
                preferredPlayerBlocks: new[] { new ShapePlayerBlockEntry(preset, 1) });

            try
            {
                var validation = ShapePlaysetGenerator.ValidateConfiguration(config);

                Assert.That(validation.IsValid, Is.False);
                Assert.That(validation.Errors,
                    Does.Contain(ShapePlaysetConfigurationError.InvalidPreferredBlockSupply));
            }
            finally
            {
                Object.DestroyImmediate(preset);
            }
        }

        [Test]
        public void ValidateConfiguration_AcceptsFourConnectedPreferredPresetWithInternalEmptyCells()
        {
            var preset = ScriptableObject.CreateInstance<BlockPreset>();
            typeof(BlockSchema).GetField("schema").SetValue(
                typeof(BlockPreset).GetProperty(nameof(BlockPreset.BlockSchema)).GetValue(preset),
                new[,] { { 1, 0, 1 }, { 1, 1, 1 } });
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 3, maxBoardColumns: 3,
                minBoardRows: 2, maxBoardRows: 2,
                minObstacleCells: 0, maxObstacleCells: 0,
                minGeneratedBlockColumns: 2, maxGeneratedBlockColumns: 3,
                minGeneratedBlockRows: 2, maxGeneratedBlockRows: 2,
                preferredPlayerBlocks: new[] { new ShapePlayerBlockEntry(preset, 1) });

            try
            {
                var validation = ShapePlaysetGenerator.ValidateConfiguration(config);

                Assert.That(validation.IsValid, Is.True, string.Join(", ", validation.Errors));
            }
            finally
            {
                Object.DestroyImmediate(preset);
            }
        }

        [Test]
        public void Generate_MixesPreferredPresetWithGeneratedFallbackBlocks()
        {
            var preset = ScriptableObject.CreateInstance<BlockPreset>();
            typeof(BlockSchema).GetField("schema").SetValue(
                typeof(BlockPreset).GetProperty(nameof(BlockPreset.BlockSchema)).GetValue(preset),
                new[,] { { 1, 1 }, { 1, 0 } });
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 2, maxBoardColumns: 2,
                minBoardRows: 2, maxBoardRows: 2,
                minObstacleCells: 0, maxObstacleCells: 0,
                minGeneratedBlockColumns: 1, maxGeneratedBlockColumns: 1,
                minGeneratedBlockRows: 1, maxGeneratedBlockRows: 1,
                preferredPlayerBlocks: new[] { new ShapePlayerBlockEntry(preset, 1) },
                maxSingletonFallbackBlocks: 1);

            try
            {
                var result = ShapePlaysetGenerator.Generate(config, seed: 9);

                Assert.That(result.IsSuccess, Is.True, result.Error.ToString());
                Assert.That(result.Playset.UsedPredefinedBlocks, Is.True);
                Assert.That(result.Playset.PlayerBlocks, Has.Count.EqualTo(2));
                Assert.That(result.Playset.Configuration.PreferredPlayerBlocks[0].PresetSchema,
                    Is.EqualTo(new[,] { { 1, 1 }, { 1, 0 } }));
                var predefinedCount = 0;
                for (var index = 0; index < result.Playset.PlayerBlocks.Count; index++)
                    if (result.Playset.PlayerBlocks[index].IsPredefined) predefinedCount++;
                Assert.That(predefinedCount, Is.EqualTo(1));
                Assert.That(result.Playset.PlayerBlocks.Count - predefinedCount, Is.EqualTo(1));
                Assert.That(result.Playset.Difficulty.PlayerCellCount, Is.EqualTo(4));
            }
            finally
            {
                Object.DestroyImmediate(preset);
            }
        }

        [Test]
        public void Generate_ReusesPreferredPresetBeyondConfiguredCountAndFallbackDimensions()
        {
            var preset = CreatePreset(new[,] { { 1, 1 } });
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 4, maxBoardColumns: 4,
                minBoardRows: 2, maxBoardRows: 2,
                minObstacleCells: 0, maxObstacleCells: 0,
                minGeneratedBlockColumns: 1, maxGeneratedBlockColumns: 1,
                minGeneratedBlockRows: 1, maxGeneratedBlockRows: 1,
                preferredPlayerBlocks: new[] { new ShapePlayerBlockEntry(preset, 0) },
                minPlayableRegionCells: 1,
                rectangularPreference: 0.5f, repetitionPenalty: 0f);

            try
            {
                var result = ShapePlaysetGenerator.Generate(config, seed: 19);

                Assert.That(result.IsSuccess, Is.True,
                    "A preferred shape is reusable and its dimensions/count do not constrain fallback blocks.");
                Assert.That(result.Playset.PlayerBlocks, Has.Count.EqualTo(4));
                Assert.That(result.Playset.UsedPredefinedBlocks, Is.True);
                foreach (var block in result.Playset.PlayerBlocks)
                    Assert.That(block.PreferredPreset, Is.SameAs(preset));
                var level = ScriptableObject.CreateInstance<ShapeLevelDefinition>();
                level.SetGeneratedContent(result.Playset);
                Assert.That(level.PlayerBlocks, Has.Count.EqualTo(1));
                Assert.That(level.PlayerBlocks[0].Preset, Is.SameAs(preset));
                Assert.That(level.PlayerBlocks[0].Count, Is.EqualTo(4));
                Object.DestroyImmediate(level);
            }
            finally
            {
                Object.DestroyImmediate(preset);
            }
        }

        [Test]
        public void Generate_UsesSeededTieBreakingForEquivalentPreferredPresets()
        {
            var firstPreset = CreatePreset(new[,] { { 1, 1 }, { 1, 1 } });
            var secondPreset = CreatePreset(new[,] { { 1, 1 }, { 1, 1 } });
            typeof(BlockPreset).GetProperty(nameof(BlockPreset.Id)).GetSetMethod(true)
                .Invoke(firstPreset, new object[] { "preset-a" });
            typeof(BlockPreset).GetProperty(nameof(BlockPreset.Id)).GetSetMethod(true)
                .Invoke(secondPreset, new object[] { "preset-b" });
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 2, maxBoardColumns: 2,
                minBoardRows: 2, maxBoardRows: 2,
                minObstacleCells: 0, maxObstacleCells: 0,
                minGeneratedBlockColumns: 1, maxGeneratedBlockColumns: 2,
                minGeneratedBlockRows: 1, maxGeneratedBlockRows: 2,
                preferredPlayerBlocks: new[]
                {
                    new ShapePlayerBlockEntry(firstPreset, 1),
                    new ShapePlayerBlockEntry(secondPreset, 1)
                },
                minPlayableRegionCells: 1);

            try
            {
                var selectedPresetIds = new System.Collections.Generic.HashSet<string>();
                for (var seed = 0; seed < 16; seed++)
                {
                    var first = ShapePlaysetGenerator.Generate(config, seed);
                    var repeated = ShapePlaysetGenerator.Generate(config, seed);

                    Assert.That(first.IsSuccess, Is.True, $"seed={seed}; error={first.Error}");
                    Assert.That(repeated.IsSuccess, Is.True, $"seed={seed}; error={repeated.Error}");
                    Assert.That(first.Playset.PlayerBlocks, Has.Count.EqualTo(1));
                    Assert.That(repeated.Playset.PlayerBlocks[0].PreferredPreset.Id,
                        Is.EqualTo(first.Playset.PlayerBlocks[0].PreferredPreset.Id),
                        $"The same seed must choose the same preset; seed={seed}.");
                    selectedPresetIds.Add(first.Playset.PlayerBlocks[0].PreferredPreset.Id);
                }

                Assert.That(selectedPresetIds, Is.EquivalentTo(new[] { "preset-a", "preset-b" }),
                    "Different seeds should vary equivalent preferred choices without list-order bias.");
            }
            finally
            {
                Object.DestroyImmediate(firstPreset);
                Object.DestroyImmediate(secondPreset);
            }
        }

        [Test]
        public void Generate_PrefersBalancedPresetCoverageWhenPreferredCoverageTies()
        {
            var triomino = CreatePreset(new[,] { { 1, 1, 1 } });
            var domino = CreatePreset(new[,] { { 1, 1 } });
            var singleton = CreatePreset(new[,] { { 1 } });
            typeof(BlockPreset).GetProperty(nameof(BlockPreset.Id)).GetSetMethod(true)
                .Invoke(triomino, new object[] { "triomino" });
            typeof(BlockPreset).GetProperty(nameof(BlockPreset.Id)).GetSetMethod(true)
                .Invoke(domino, new object[] { "domino" });
            typeof(BlockPreset).GetProperty(nameof(BlockPreset.Id)).GetSetMethod(true)
                .Invoke(singleton, new object[] { "singleton" });
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 3, maxBoardColumns: 3,
                minBoardRows: 2, maxBoardRows: 2,
                minObstacleCells: 0, maxObstacleCells: 0,
                minGeneratedBlockColumns: 1, maxGeneratedBlockColumns: 1,
                minGeneratedBlockRows: 1, maxGeneratedBlockRows: 1,
                preferredPlayerBlocks: new[]
                {
                    new ShapePlayerBlockEntry(triomino, 1),
                    new ShapePlayerBlockEntry(domino, 1),
                    new ShapePlayerBlockEntry(singleton, 1)
                },
                minPlayableRegionCells: 1);

            try
            {
                var result = ShapePlaysetGenerator.Generate(config, seed: 31);

                Assert.That(result.IsSuccess, Is.True, result.Error.ToString());
                Assert.That(result.Playset.Difficulty.PlayerCellCount, Is.EqualTo(6));
                var usedPresetIds = new System.Collections.Generic.HashSet<string>();
                foreach (var block in result.Playset.PlayerBlocks)
                {
                    Assert.That(block.IsPredefined, Is.True);
                    usedPresetIds.Add(block.PreferredPreset.Id);
                }
                Assert.That(usedPresetIds, Is.EquivalentTo(new[] { "triomino", "domino", "singleton" }),
                    "Among full preferred covers, scoring should favor a more balanced preset mix.");
            }
            finally
            {
                Object.DestroyImmediate(triomino);
                Object.DestroyImmediate(domino);
                Object.DestroyImmediate(singleton);
            }
        }

        [Test]
        public void Generate_MaximizesPreferredCoverageBeforeProfileOrder()
        {
            var largerPreset = CreatePreset(new[,] { { 1, 0 }, { 1, 1 } });
            var smallerPreset = CreatePreset(new[,] { { 1, 1 } });
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 2, maxBoardColumns: 2,
                minBoardRows: 2, maxBoardRows: 2,
                minObstacleCells: 0, maxObstacleCells: 0,
                minGeneratedBlockColumns: 1, maxGeneratedBlockColumns: 2,
                minGeneratedBlockRows: 1, maxGeneratedBlockRows: 2,
                preferredPlayerBlocks: new[]
                {
                    new ShapePlayerBlockEntry(largerPreset, 1),
                    new ShapePlayerBlockEntry(smallerPreset, 1)
                },
                maxSingletonFallbackBlocks: 1,
                rectangularPreference: 0.5f, repetitionPenalty: 0f);

            try
            {
                var result = ShapePlaysetGenerator.Generate(config, seed: 41);

                Assert.That(result.IsSuccess, Is.True, result.Error.ToString());
                Assert.That(result.Playset.Difficulty.PlayerCellCount, Is.EqualTo(4));
                Assert.That(result.Playset.PlayerBlocks, Has.Count.EqualTo(2));
                Assert.That(result.Playset.PlayerBlocks[0].PreferredPreset, Is.SameAs(smallerPreset));
                Assert.That(result.Playset.PlayerBlocks[1].PreferredPreset, Is.SameAs(smallerPreset));
            }
            finally
            {
                Object.DestroyImmediate(largerPreset);
                Object.DestroyImmediate(smallerPreset);
            }
        }

        private static BlockPreset CreatePreset(int[,] schema)
        {
            var preset = ScriptableObject.CreateInstance<BlockPreset>();
            typeof(BlockSchema).GetField("schema").SetValue(
                typeof(BlockPreset).GetProperty(nameof(BlockPreset.BlockSchema)).GetValue(preset), schema);
            return preset;
        }

        [Test]
        public void Generate_UsesMixedPreferredPresetsForExactCover()
        {
            var squarePreset = CreatePreset(new[,] { { 1, 1 }, { 1, 1 } });
            var dominoPreset = CreatePreset(new[,] { { 1, 1 } });
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 3, maxBoardColumns: 3,
                minBoardRows: 2, maxBoardRows: 2,
                minObstacleCells: 0, maxObstacleCells: 0,
                minGeneratedBlockColumns: 1, maxGeneratedBlockColumns: 2,
                minGeneratedBlockRows: 1, maxGeneratedBlockRows: 2,
                preferredPlayerBlocks: new[]
                {
                    new ShapePlayerBlockEntry(squarePreset, 1),
                    new ShapePlayerBlockEntry(dominoPreset, 3)
                },
                rectangularPreference: 0.5f, repetitionPenalty: 0f);

            try
            {
                var result = ShapePlaysetGenerator.Generate(config, seed: 73);

                Assert.That(result.IsSuccess, Is.True, result.Error.ToString());
                Assert.That(result.Playset.Difficulty.PlayerCellCount, Is.EqualTo(6));
                Assert.That(result.Playset.PlayerBlocks, Has.Count.EqualTo(2));
                var usedPresets = new System.Collections.Generic.HashSet<BlockPreset>();
                foreach (var block in result.Playset.PlayerBlocks) usedPresets.Add(block.PreferredPreset);
                Assert.That(usedPresets, Is.EquivalentTo(new[] { squarePreset, dominoPreset }));
            }
            finally
            {
                Object.DestroyImmediate(squarePreset);
                Object.DestroyImmediate(dominoPreset);
            }
        }

        [Test]
        public void Generate_MinimizesSingletonsWhenACompleteFallbackExists()
        {
            var preset = CreatePreset(new[,] { { 1, 0 }, { 1, 1 } });
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 2, maxBoardColumns: 2,
                minBoardRows: 2, maxBoardRows: 2,
                minObstacleCells: 0, maxObstacleCells: 0,
                minGeneratedBlockColumns: 2, maxGeneratedBlockColumns: 2,
                minGeneratedBlockRows: 2, maxGeneratedBlockRows: 2,
                preferredPlayerBlocks: new[] { new ShapePlayerBlockEntry(preset, 1) },
                maxSingletonFallbackBlocks: 1,
                rectangularPreference: 0.5f, repetitionPenalty: 0f);

            try
            {
                var result = ShapePlaysetGenerator.Generate(config, seed: 101);

                Assert.That(result.IsSuccess, Is.True, result.Error.ToString());
                Assert.That(result.Playset.PlayerBlocks, Has.Count.EqualTo(1));
                var singletonCount = 0;
                for (var index = 0; index < result.Playset.PlayerBlocks.Count; index++)
                    if (!result.Playset.PlayerBlocks[index].IsPredefined &&
                        CountMaskCells(result.Playset.PlayerBlocks[index].Footprint) == 1)
                        singletonCount++;
                Assert.That(singletonCount, Is.Zero);
                Assert.That(CountMaskCells(result.Playset.PlayerBlocks[0].Footprint), Is.EqualTo(4));
            }
            finally
            {
                Object.DestroyImmediate(preset);
            }
        }

        [Test]
        public void ValidateConfiguration_AcceptsPreferredEntryRegardlessOfCount()
        {
            var preset = ScriptableObject.CreateInstance<BlockPreset>();
            typeof(BlockSchema).GetField("schema").SetValue(
                typeof(BlockPreset).GetProperty(nameof(BlockPreset.BlockSchema)).GetValue(preset),
                new[,] { { 1 } });
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 2, maxBoardColumns: 2,
                minBoardRows: 2, maxBoardRows: 2,
                minObstacleCells: 0, maxObstacleCells: 0,
                minGeneratedBlockColumns: 1, maxGeneratedBlockColumns: 1,
                minGeneratedBlockRows: 1, maxGeneratedBlockRows: 1,
                preferredPlayerBlocks: new[] { new ShapePlayerBlockEntry(preset, 65) });

            try
            {
                var validation = ShapePlaysetGenerator.ValidateConfiguration(config);

                Assert.That(validation.IsValid, Is.True, string.Join(", ", validation.Errors));
            }
            finally
            {
                Object.DestroyImmediate(preset);
            }
        }

        [Test]
        public void ShapeStyleMetrics_ClassifyLinesAndSolidRectanglesButNotSingletonsOrPolyominoes()
        {
            Assert.That(IsSolidRectangleFootprint(new[,] { { 1, 1, 1 } }), Is.True);
            Assert.That(IsSolidRectangleFootprint(new[,] { { 1 }, { 1 }, { 1 } }), Is.True);
            Assert.That(IsSolidRectangleFootprint(new[,] { { 1, 1 }, { 1, 1 } }), Is.True);
            Assert.That(IsSolidRectangleFootprint(new[,] { { 1, 1 }, { 1, 0 } }), Is.False);
            Assert.That(IsSolidRectangleFootprint(new[,] { { 1 } }), Is.False);
        }

        [Test]
        public void GenerateForBoard_RectangularPreferenceChangesCompletedCoverStyleDeterministically()
        {
            var board = new[,] { { 1, 1, 1 }, { 1, 1, 1 } };
            var avoidRectangles = GenerateStylePlayset(board, 0f, 0f, seed: 319);
            var favorRectangles = GenerateStylePlayset(board, 1f, 0f, seed: 319);

            AssertCompleteFixedOrientationCover(board, avoidRectangles);
            AssertCompleteFixedOrientationCover(board, favorRectangles);
            Assert.That(CountRectangles(avoidRectangles), Is.LessThan(CountRectangles(favorRectangles)));
            var repeated = GenerateStylePlayset(board, 0f, 0f, seed: 319);
            Assert.That(GetOutputKey(avoidRectangles), Is.EqualTo(GetOutputKey(repeated)));
        }

        [Test]
        public void GenerateForBoard_ExtremeStyleProfileFindsBestSmallBoardPartitionWithinBound()
        {
            var board = new[,] { { 1, 1, 1 }, { 1, 1, 1 } };
            var config = CreateStyleConfig(0f, 1f, searchNodes: 30,
                maxBlockRows: 2, maxBlockColumns: 2);

            var result = ShapePlaysetGenerator.GenerateForBoard(config, 319, board, new int[2, 3]);

            Assert.That(result.IsSuccess, Is.True, result.Error.ToString());
            Assert.That(result.Playset.OptimizationStatus, Is.EqualTo(ShapePlaysetOptimizationStatus.ProvenOptimal));
            var nodesVisited = (int)GetProperty(result.Playset, "OptimizationNodesVisited");
            TestContext.WriteLine($"Extreme style optimum found in {nodesVisited}/{config.MaxTilingSearchNodes} nodes.");
            Assert.That(nodesVisited, Is.LessThan(config.MaxTilingSearchNodes));
            AssertCompleteFixedOrientationCover(board, result.Playset);
            Assert.That(CountRectangles(result.Playset), Is.Zero);
            Assert.That(CountDuplicatePairs(result.Playset), Is.Zero);
        }

        [Test]
        public void GenerateForBoard_RepetitionPenaltyReducesDuplicateFootprints()
        {
            var board = new int[4, 4];
            for (var row = 0; row < board.GetLength(0); row++)
            for (var column = 0; column < board.GetLength(1); column++)
                board[row, column] = 1;

            var allowRepeats = GenerateStylePlayset(
                board, 0.5f, 0f, seed: 517, searchNodes: 300000, maxBlockRows: 2, maxBlockColumns: 2);
            var discourageRepeats = GenerateStylePlayset(
                board, 0.5f, 1f, seed: 517, searchNodes: 300000, maxBlockRows: 2, maxBlockColumns: 2);

            AssertCompleteFixedOrientationCover(board, allowRepeats);
            AssertCompleteFixedOrientationCover(board, discourageRepeats);
            Assert.That(CountDuplicatePairs(discourageRepeats),
                Is.LessThan(CountDuplicatePairs(allowRepeats)));
        }

        [Test]
        public void GenerateForBoard_StyleCanOutrankPreferredCoverageWithoutBreakingFullCoverage()
        {
            var board = new[,] { { 1, 1, 1 }, { 1, 1, 1 } };
            var rectanglePreset = CreatePreset(new[,] { { 1, 1, 1 }, { 1, 1, 1 } });
            var config = CreateStyleConfig(0f, 0f, searchNodes: 100000,
                preferred: new[] { new ShapePlayerBlockEntry(rectanglePreset, 1) },
                maxBlockRows: 2, maxBlockColumns: 2);

            try
            {
                var result = ShapePlaysetGenerator.GenerateForBoard(config, 811, board, new int[2, 3]);

                Assert.That(result.IsSuccess, Is.True, result.Error.ToString());
                AssertCompleteFixedOrientationCover(board, result.Playset);
                var hasPredefinedBlock = false;
                foreach (var block in result.Playset.PlayerBlocks)
                    hasPredefinedBlock |= block.IsPredefined;
                Assert.That(hasPredefinedBlock, Is.False,
                    "Style score should be allowed to trade away preferred coverage.");
            }
            finally
            {
                Object.DestroyImmediate(rectanglePreset);
            }
        }

        [Test]
        public void GenerateForBoard_StyleUpperBoundStopsSearchBeforeBudgetAndProvesOptimal()
        {
            var board = new[,] { { 1, 1, 1 }, { 1, 1, 1 } };
            var config = CreateStyleConfig(0.5f, 0.5f, searchNodes: 30);

            var result = ShapePlaysetGenerator.GenerateForBoard(config, 112, board, new int[2, 3]);

            Assert.That(result.IsSuccess, Is.True, result.Error.ToString());
            AssertCompleteFixedOrientationCover(board, result.Playset);
            Assert.That(result.Playset.OptimizationStatus, Is.EqualTo(ShapePlaysetOptimizationStatus.ProvenOptimal));
            var nodesVisited = (int)GetProperty(result.Playset, "OptimizationNodesVisited");
            TestContext.WriteLine($"Early optimal stop used {nodesVisited}/{config.MaxTilingSearchNodes} nodes.");
            Assert.That(nodesVisited, Is.LessThan(config.MaxTilingSearchNodes));
        }

        [Test]
        public void GenerateForBoard_BudgetTooSmallToFindSolutionRemainsAnInconclusiveFailure()
        {
            var board = new[,] { { 1, 1 }, { 1, 1 } };
            var config = CreateStyleConfig(0.5f, 0.5f, searchNodes: 1,
                maxBlockRows: 2, maxBlockColumns: 2);

            var result = ShapePlaysetGenerator.GenerateForBoard(config, 112, board, new int[2, 2]);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(ShapePlaysetGenerationError.TilingBudgetExceeded));
            Assert.That(result.ConfigurationValidation.FeasibilityStatus,
                Is.EqualTo(ShapePlaysetFeasibilityStatus.Unproven));
        }

        private static ShapePlaysetGenerationConfig CreateStyleConfig(
            float rectangularPreference, float repetitionPenalty, int searchNodes = 100000,
            IReadOnlyList<ShapePlayerBlockEntry> preferred = null,
            int maxBlockRows = 3, int maxBlockColumns = 3) =>
            new ShapePlaysetGenerationConfig(
                2, 4, 2, 4, 0, 0, 1, maxBlockColumns, 1, maxBlockRows,
                preferredPlayerBlocks: preferred,
                minPlayableRegionCells: 1,
                maxTilingSearchNodes: searchNodes,
                maxCandidateAttempts: 1,
                maxSingletonFallbackBlocks: 0,
                rectangularPreference: rectangularPreference,
                repetitionPenalty: repetitionPenalty);

        private static GeneratedShapePlayset GenerateStylePlayset(
            int[,] board, float rectangularPreference, float repetitionPenalty,
            int seed, int searchNodes = 100000,
            int maxBlockRows = 3, int maxBlockColumns = 3)
        {
            var config = CreateStyleConfig(rectangularPreference, repetitionPenalty, searchNodes,
                maxBlockRows: maxBlockRows, maxBlockColumns: maxBlockColumns);
            var result = ShapePlaysetGenerator.GenerateForBoard(
                config, seed, board, new int[board.GetLength(0), board.GetLength(1)]);
            Assert.That(result.IsSuccess, Is.True, result.Error.ToString());
            return result.Playset;
        }

        private static bool IsSolidRectangleFootprint(int[,] footprint)
        {
            var method = typeof(ShapePlaysetGenerator).GetMethod(
                "IsSolidRectangleFootprint",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "Expected the generator's solid-rectangle classifier.");
            return (bool)method.Invoke(null, new object[] { footprint });
        }

        private static int CountRectangles(GeneratedShapePlayset playset)
        {
            var count = 0;
            foreach (var block in playset.PlayerBlocks)
                if (IsSolidRectangleFootprint(GetStoredFootprint(block))) count++;
            return count;
        }

        private static int CountDuplicatePairs(GeneratedShapePlayset playset)
        {
            var frequencies = new Dictionary<string, int>();
            foreach (var block in playset.PlayerBlocks)
            {
                var key = CreateFootprintKey(GetStoredFootprint(block));
                frequencies.TryGetValue(key, out var count);
                frequencies[key] = count + 1;
            }
            var pairs = 0;
            foreach (var count in frequencies.Values) pairs += count * (count - 1) / 2;
            return pairs;
        }

        private static int[,] GetStoredFootprint(GeneratedShapeBlock block)
        {
            var schema = block.Footprint;
            for (var turn = 0; turn < block.PlacementRotationQuarterTurns; turn++)
            {
                var rotated = new int[schema.GetLength(1), schema.GetLength(0)];
                for (var row = 0; row < schema.GetLength(0); row++)
                for (var column = 0; column < schema.GetLength(1); column++)
                    rotated[column, schema.GetLength(0) - row - 1] = schema[row, column];
                schema = rotated;
            }
            return schema;
        }

        private static string CreateFootprintKey(int[,] footprint)
        {
            var parts = new List<string>();
            for (var row = 0; row < footprint.GetLength(0); row++)
            for (var column = 0; column < footprint.GetLength(1); column++)
                if (footprint[row, column] != 0) parts.Add($"{row},{column}");
            return footprint.GetLength(0) + "x" + footprint.GetLength(1) + ":" + string.Join(";", parts);
        }

        private static string GetOutputKey(GeneratedShapePlayset playset)
        {
            var parts = new List<string>();
            foreach (var block in playset.PlayerBlocks)
            {
                var placement = new List<string>();
                foreach (var cell in block.SolvedPlacement) placement.Add($"{cell.x},{cell.y}");
                parts.Add(CreateFootprintKey(GetStoredFootprint(block)) + "@" + string.Join(";", placement));
            }
            return string.Join("|", parts);
        }

        private static void AssertCompleteFixedOrientationCover(int[,] board, GeneratedShapePlayset playset)
        {
            var covered = new int[board.GetLength(0), board.GetLength(1)];
            var footprints = new List<int[,]>();
            foreach (var block in playset.PlayerBlocks)
            {
                footprints.Add(GetStoredFootprint(block));
                foreach (var cell in block.SolvedPlacement)
                {
                    Assert.That(board[cell.x, cell.y], Is.EqualTo(1));
                    Assert.That(covered[cell.x, cell.y], Is.Zero);
                    covered[cell.x, cell.y] = 1;
                }
            }
            Assert.That(covered, Is.EqualTo(board));
            Assert.That(ShapeLevelTilingSolver.Solve(board, footprints, 300000).Status,
                Is.EqualTo(ShapeLevelTilingStatus.Feasible));
        }

        private static void SetProfileInt(ShapePlaysetGenerationProfile profile, string fieldName, int value)
        {
            var field = typeof(ShapePlaysetGenerationProfile).GetField(
                fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Expected profile setting {fieldName}.");
            field.SetValue(profile, value);
        }

        private static object GetProperty(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(
                propertyName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"Expected property {propertyName}.");
            return property.GetValue(target);
        }
    }
}
