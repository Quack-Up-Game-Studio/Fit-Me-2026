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
        private const string DisableExplosionVfxDefine = "FITME_DISABLE_EXPLOSION_VFX";
        private const string OutputPath = "Builds/Android/FitMe-Performance-Diagnostics.apk";
        private const string ExplosionVfxOffOutputPath = "Builds/Android/FitMe-Performance-Diagnostics-ExplosionVfxOff.apk";

        [MenuItem("Fit Me/Build Android Performance Diagnostics APK")]
        public static void BuildAndroid()
        {
            BuildAndroid(disableExplosionVfx: false);
        }

        [MenuItem("Fit Me/Build Android Performance Diagnostics APK (Explosion VFX Off)")]
        public static void BuildAndroidExplosionVfxOff()
        {
            BuildAndroid(disableExplosionVfx: true);
        }

        private static void BuildAndroid(bool disableExplosionVfx)
        {
            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
                throw new InvalidOperationException("No enabled scenes are configured in Build Settings.");

            var options = CreateBuildOptions(scenes, disableExplosionVfx);
            Directory.CreateDirectory(Path.GetDirectoryName(options.locationPathName));

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

            Debug.Log($"Android performance diagnostics APK built at {options.locationPathName} ({report.summary.totalSize} bytes).");
        }

        private static BuildPlayerOptions CreateBuildOptions(string[] scenes, bool disableExplosionVfx)
        {
            var defines = disableExplosionVfx
                ? new[] { DiagnosticsDefine, DisableExplosionVfxDefine }
                : new[] { DiagnosticsDefine };
            var outputPath = disableExplosionVfx ? ExplosionVfxOffOutputPath : OutputPath;

            return new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.GetFullPath(outputPath),
                target = BuildTarget.Android,
                options = BuildOptions.Development,
                extraScriptingDefines = defines
            };
        }
    }
}
#endif
