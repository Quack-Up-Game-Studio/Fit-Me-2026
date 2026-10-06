using System.Collections.Generic;
using NUnit.Framework;

namespace QuackUp.GoogleAdMob.Tests
{
    public sealed class AdUnitIdBuildPolicyTests
    {
        private static readonly KeyValuePair<string, string>[] SampleTestUnit =
        {
            new("iosBannerProdId", "ca-app-pub-3940256099942544/2934735716")
        };

        private static readonly KeyValuePair<string, string>[] ProductionUnits =
        {
            new("androidBannerProdId", "ca-app-pub-1737894207840657/5082361310")
        };

        [Test]
        public void DevelopmentBuild_AllowsTestModeAndSampleUnitIds()
        {
            var violations = AdUnitIdBuildPolicy.GetProductionBuildViolations(
                isDevelopmentBuild: true,
                isTestMode: true,
                activeProductionUnitIds: SampleTestUnit);

            Assert.That(violations, Is.Empty);
        }

        [Test]
        public void NonDevelopmentBuild_RejectsTestMode()
        {
            var violations = AdUnitIdBuildPolicy.GetProductionBuildViolations(
                isDevelopmentBuild: false,
                isTestMode: true,
                activeProductionUnitIds: ProductionUnits);

            Assert.That(violations, Has.Some.Contains("Test mode"));
        }

        [Test]
        public void NonDevelopmentBuild_RejectsGoogleSampleTestUnitIds()
        {
            var violations = AdUnitIdBuildPolicy.GetProductionBuildViolations(
                isDevelopmentBuild: false,
                isTestMode: false,
                activeProductionUnitIds: SampleTestUnit);

            Assert.That(violations, Has.Some.Contains("iosBannerProdId"));
        }

        [Test]
        public void NonDevelopmentBuild_AllowsProductionUnitIds()
        {
            var violations = AdUnitIdBuildPolicy.GetProductionBuildViolations(
                isDevelopmentBuild: false,
                isTestMode: false,
                activeProductionUnitIds: ProductionUnits);

            Assert.That(violations, Is.Empty);
        }
    }
}
