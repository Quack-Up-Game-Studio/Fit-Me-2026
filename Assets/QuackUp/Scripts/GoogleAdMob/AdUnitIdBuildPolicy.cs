using System;
using System.Collections.Generic;

namespace QuackUp.GoogleAdMob
{
    /// <summary>
    /// Pure validation rules for preventing Google sample ad units from reaching non-development builds.
    /// </summary>
    public static class AdUnitIdBuildPolicy
    {
        public const string GoogleSampleTestUnitPrefix = "ca-app-pub-3940256099942544/";

        public static string[] GetProductionBuildViolations(
            bool isDevelopmentBuild,
            bool isTestMode,
            IEnumerable<KeyValuePair<string, string>> activeProductionUnitIds)
        {
            if (isDevelopmentBuild)
                return Array.Empty<string>();

            if (isTestMode)
                return new[] { "AdsSettings is in Test mode." };

            var violations = new List<string>();
            if (activeProductionUnitIds == null)
                return violations.ToArray();

            foreach (var (fieldName, unitId) in activeProductionUnitIds)
            {
                if (unitId?.Trim().StartsWith(GoogleSampleTestUnitPrefix, StringComparison.Ordinal) == true)
                    violations.Add($"{fieldName} uses a Google sample test ad unit ID.");
            }

            return violations.ToArray();
        }
    }
}
