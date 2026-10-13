using FitMe.GameData;
using FitMe.Grid;
using NUnit.Framework;
using UnityEngine;

namespace FitMe.Grid.Tests
{
    public sealed class ShapePlaysetGeneratorTests
    {
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
            Assert.That(first.Playset.Difficulty.ScoreModelVersion, Is.EqualTo(1));
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
                minGeneratedBlockRows: 2, maxGeneratedBlockRows: 2);

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
                minPlayableRegionCells: 1);

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
        public void Generate_UsesPreferredOrderToBreakEqualCoverageTies()
        {
            var firstPreset = CreatePreset(new[,] { { 1, 1 } });
            var secondPreset = CreatePreset(new[,] { { 1 }, { 1 } });
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 2, maxBoardColumns: 2,
                minBoardRows: 2, maxBoardRows: 2,
                minObstacleCells: 0, maxObstacleCells: 0,
                minGeneratedBlockColumns: 1, maxGeneratedBlockColumns: 1,
                minGeneratedBlockRows: 1, maxGeneratedBlockRows: 1,
                preferredPlayerBlocks: new[]
                {
                    new ShapePlayerBlockEntry(firstPreset, 1),
                    new ShapePlayerBlockEntry(secondPreset, 1)
                },
                minPlayableRegionCells: 1);

            try
            {
                var result = ShapePlaysetGenerator.Generate(config, seed: 23);

                Assert.That(result.IsSuccess, Is.True, result.Error.ToString());
                Assert.That(result.Playset.PlayerBlocks, Has.Count.EqualTo(2));
                Assert.That(result.Playset.PlayerBlocks[0].PreferredPreset, Is.SameAs(firstPreset));
                Assert.That(result.Playset.PlayerBlocks[1].PreferredPreset, Is.SameAs(firstPreset));
            }
            finally
            {
                Object.DestroyImmediate(firstPreset);
                Object.DestroyImmediate(secondPreset);
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
                maxSingletonFallbackBlocks: 1);

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
        public void Generate_PrefersEarlierProfileEntryAfterEqualPreferredCoverage()
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
                });

            try
            {
                var result = ShapePlaysetGenerator.Generate(config, seed: 73);

                Assert.That(result.IsSuccess, Is.True, result.Error.ToString());
                Assert.That(result.Playset.Difficulty.PlayerCellCount, Is.EqualTo(6));
                Assert.That(result.Playset.PlayerBlocks, Has.Count.EqualTo(2));
                Assert.That(result.Playset.PlayerBlocks[0].PreferredPreset, Is.SameAs(squarePreset));
                Assert.That(result.Playset.PlayerBlocks[1].PreferredPreset, Is.SameAs(dominoPreset));
            }
            finally
            {
                Object.DestroyImmediate(squarePreset);
                Object.DestroyImmediate(dominoPreset);
            }
        }

        [Test]
        public void Generate_SingletonCapOverridesFallbackMinimumDimensions()
        {
            var preset = CreatePreset(new[,] { { 1, 0 }, { 1, 1 } });
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 2, maxBoardColumns: 2,
                minBoardRows: 2, maxBoardRows: 2,
                minObstacleCells: 0, maxObstacleCells: 0,
                minGeneratedBlockColumns: 2, maxGeneratedBlockColumns: 2,
                minGeneratedBlockRows: 2, maxGeneratedBlockRows: 2,
                preferredPlayerBlocks: new[] { new ShapePlayerBlockEntry(preset, 1) },
                maxSingletonFallbackBlocks: 1);

            try
            {
                var result = ShapePlaysetGenerator.Generate(config, seed: 101);

                Assert.That(result.IsSuccess, Is.True, result.Error.ToString());
                Assert.That(result.Playset.PlayerBlocks, Has.Count.EqualTo(2));
                var singletonCount = 0;
                for (var index = 0; index < result.Playset.PlayerBlocks.Count; index++)
                    if (!result.Playset.PlayerBlocks[index].IsPredefined &&
                        CountMaskCells(result.Playset.PlayerBlocks[index].Footprint) == 1)
                        singletonCount++;
                Assert.That(singletonCount, Is.EqualTo(1));
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
    }
}
