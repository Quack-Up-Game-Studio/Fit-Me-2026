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
        public void Generate_ReturnsDeterministicExactCoverForEverySeedOnAcceptedBaseConfiguration()
        {
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 8, maxBoardColumns: 8,
                minBoardRows: 8, maxBoardRows: 8,
                minObstacleCells: 0, maxObstacleCells: 4,
                minGeneratedBlockColumns: 2, maxGeneratedBlockColumns: 2,
                minGeneratedBlockRows: 2, maxGeneratedBlockRows: 2);

            var first = ShapePlaysetGenerator.Generate(config, seed: 17);
            var second = ShapePlaysetGenerator.Generate(config, seed: 17);

            Assert.That(first.IsSuccess, Is.True,
                $"{first.Error}; config={config.MinBoardColumns}x{config.MinBoardRows}, obstacles={config.MinObstacleCells}-{config.MaxObstacleCells}");
            Assert.That(second.IsSuccess, Is.True, second.Error.ToString());
            Assert.That(first.Playset.ObstacleMask, Is.EqualTo(second.Playset.ObstacleMask));
            Assert.That(first.Playset.PlayerBlocks.Count, Is.EqualTo(second.Playset.PlayerBlocks.Count));
            for (var blockIndex = 0; blockIndex < first.Playset.PlayerBlocks.Count; blockIndex++)
                Assert.That(first.Playset.PlayerBlocks[blockIndex].Footprint,
                    Is.EqualTo(second.Playset.PlayerBlocks[blockIndex].Footprint));
            Assert.That(first.Playset.Difficulty.PlayerCellCount + first.Playset.Difficulty.ObstacleCellCount,
                Is.EqualTo(first.Playset.Difficulty.PlayableCellCount));
            Assert.That(first.Playset.IsSolvable, Is.True);
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
            }
            finally
            {
                Object.DestroyImmediate(preset);
            }
        }
    }
}
