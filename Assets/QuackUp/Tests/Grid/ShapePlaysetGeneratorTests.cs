using FitMe.GameData;
using NUnit.Framework;

namespace FitMe.Grid.Tests
{
    public sealed class ShapePlaysetGeneratorTests
    {
        [Test]
        public void ConfigurationValidation_AcceptsAFeasibleBaseShapeConfiguration()
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
    }
}
