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
            var expectedPlacementsPerBlock = obstacleCount == 0 ? 49 : 45;
            Assert.That(first.Playset.Difficulty.TotalLegalPlacements,
                Is.EqualTo(first.Playset.PlayerBlocks.Count * expectedPlacementsPerBlock));
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
        public void ValidateConfiguration_RejectsBoardDimensionsWithoutConstructiveTiling()
        {
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 8, maxBoardColumns: 8,
                minBoardRows: 7, maxBoardRows: 7,
                minObstacleCells: 0, maxObstacleCells: 4,
                minGeneratedBlockColumns: 2, maxGeneratedBlockColumns: 2,
                minGeneratedBlockRows: 2, maxGeneratedBlockRows: 2);

            var validation = ShapePlaysetGenerator.ValidateConfiguration(config);

            Assert.That(validation.IsValid, Is.False);
            Assert.That(validation.Errors, Does.Contain(ShapePlaysetConfigurationError.NoFeasibleConnectedTiling));
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
        public void Generate_BaseFallbackConfigurationProducesBothConstructiveObstacleVariants()
        {
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 8, maxBoardColumns: 8,
                minBoardRows: 8, maxBoardRows: 8,
                minObstacleCells: 0, maxObstacleCells: 4,
                minGeneratedBlockColumns: 2, maxGeneratedBlockColumns: 2,
                minGeneratedBlockRows: 2, maxGeneratedBlockRows: 2);

            var noObstacleResult = ShapePlaysetGenerator.Generate(config, seed: -1);
            var cornerTileObstacleResult = ShapePlaysetGenerator.Generate(config, seed: 17);

            Assert.That(noObstacleResult.IsSuccess, Is.True, noObstacleResult.Error.ToString());
            Assert.That(cornerTileObstacleResult.IsSuccess, Is.True, cornerTileObstacleResult.Error.ToString());
            Assert.That(CountMaskCells(noObstacleResult.Playset.ObstacleMask), Is.Zero);
            Assert.That(CountMaskCells(cornerTileObstacleResult.Playset.ObstacleMask), Is.EqualTo(4));
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
        public void Generate_FallsBackWhenPreferredPresetSupplyCannotTileTheBoard()
        {
            var preset = ScriptableObject.CreateInstance<BlockPreset>();
            typeof(BlockSchema).GetField("schema").SetValue(
                typeof(BlockPreset).GetProperty(nameof(BlockPreset.BlockSchema)).GetValue(preset),
                new[,] { { 1, 1 } });
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
                Assert.That(result.Playset.UsedPredefinedBlocks, Is.False);
                Assert.That(result.Playset.Difficulty.PlayerCellCount, Is.EqualTo(4));
            }
            finally
            {
                Object.DestroyImmediate(preset);
            }
        }

        [Test]
        public void ValidateConfiguration_RejectsPreferredSupplyAboveExpandedBlockLimit()
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

                Assert.That(validation.IsValid, Is.False);
                Assert.That(validation.Errors,
                    Does.Contain(ShapePlaysetConfigurationError.InvalidPreferredBlockSupply));
            }
            finally
            {
                Object.DestroyImmediate(preset);
            }
        }
    }
}
