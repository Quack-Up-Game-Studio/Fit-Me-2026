#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace QuackUp.Core.Editor
{
    public static class PerformanceDiagnosticsBuild
    {
        private const string DiagnosticsDefine = "FITME_PERFORMANCE_DIAGNOSTICS";
        private const string OutputPath = "Builds/Android/FitMe-Performance-Diagnostics.apk";

        [MenuItem("Fit Me/Build Android Performance Diagnostics APK")]
        public static void BuildAndroid()
        {
            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
                throw new InvalidOperationException("No enabled scenes are configured in Build Settings.");

            string absoluteOutputPath = Path.GetFullPath(OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteOutputPath));

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = absoluteOutputPath,
                target = BuildTarget.Android,
                options = BuildOptions.Development,
                extraScriptingDefines = new[] { DiagnosticsDefine }
            };

            bool useCustomKeystore = PlayerSettings.Android.useCustomKeystore;
            BuildReport report;
            try
            {
                // This tester APK is debug-only. Avoid requiring release-signing secrets and
                // restore the project's configured signing choice immediately after the build.
                PlayerSettings.Android.useCustomKeystore = false;
                report = BuildPipeline.BuildPlayer(options);
            }
            finally
            {
                PlayerSettings.Android.useCustomKeystore = useCustomKeystore;
            }

            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Android diagnostics build failed: {report.summary.result}");

            Debug.Log($"Android performance diagnostics APK built at {absoluteOutputPath} ({report.summary.totalSize} bytes).");
        }
    }
}
#endif
