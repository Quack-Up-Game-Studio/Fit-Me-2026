using System;
using System.Collections.Generic;
using QuackUp.GoogleAdMob;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class AdUnitIdBuildGuard : IPreprocessBuildWithReport
{
    private static readonly string[] AdFormats = { "Banner", "AdaptiveBanner", "Rewarded", "Interstitial" };

    // Run before other project processors that may mutate assets while preparing a build.
    public int callbackOrder => int.MinValue;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report == null)
            throw new BuildFailedException("Cannot validate Google Mobile Ads settings without a build report.");

        var isDevelopmentBuild = (report.summary.options & BuildOptions.Development) != 0;
        if (isDevelopmentBuild)
            return;

        var platformPrefix = GetPlatformPrefix(report.summary.platform);

        var assetGuids = AssetDatabase.FindAssets("t:AdsSettings");
        if (assetGuids.Length == 0)
        {
            Debug.LogWarning("[AdUnitIdBuildGuard] No AdsSettings asset found; no ad-unit IDs were checked.");
            return;
        }

        var buildViolations = new List<string>();
        foreach (var assetGuid in assetGuids)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
            var settings = AssetDatabase.LoadAssetAtPath<AdsSettings>(assetPath);
            if (settings == null)
                continue;

            var serializedSettings = new SerializedObject(settings);
            serializedSettings.Update();
            var activeProductionUnitIds = new List<KeyValuePair<string, string>>();

            foreach (var format in AdFormats)
            {
                var fieldName = $"{platformPrefix}{format}ProdId";
                var property = serializedSettings.FindProperty(fieldName);
                if (property == null || property.propertyType != SerializedPropertyType.String)
                {
                    buildViolations.Add($"{assetPath}: expected string field '{fieldName}' was not found; the guard cannot validate this asset.");
                    continue;
                }

                activeProductionUnitIds.Add(new KeyValuePair<string, string>(fieldName, property.stringValue));
            }

            var assetViolations = AdUnitIdBuildPolicy.GetProductionBuildViolations(
                isDevelopmentBuild: false,
                isTestMode: settings.IsTestMode,
                activeProductionUnitIds: activeProductionUnitIds);

            foreach (var violation in assetViolations)
                buildViolations.Add($"{assetPath}: {violation}");
        }

        if (buildViolations.Count > 0)
        {
            throw new BuildFailedException(
                "Non-development build blocked because Google Mobile Ads settings contain test configuration:\n - " +
                string.Join("\n - ", buildViolations) +
                "\nReplace the active platform's sample test ad-unit IDs or switch AdsSettings to Production mode.");
        }
    }

    private static string GetPlatformPrefix(BuildTarget target)
    {
        switch (target)
        {
            case BuildTarget.Android:
                return "android";
            case BuildTarget.iOS:
                return "ios";
            default:
                // AdsSettings.GetUnitId falls back to Android IDs on other targets.
                return "android";
        }
    }
}
