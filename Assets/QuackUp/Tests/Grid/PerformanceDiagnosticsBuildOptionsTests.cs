using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace FitMe.Grid.Tests
{
    public class PerformanceDiagnosticsBuildOptionsTests
    {
        private const string DiagnosticsDefine = "FITME_PERFORMANCE_DIAGNOSTICS";
        private const string DisableExplosionVfxDefine = "FITME_DISABLE_EXPLOSION_VFX";

        [Test]
        public void StandardDiagnosticsOptionsKeepExplosionVfxEnabled()
        {
            var options = ReadBuildOptions(disableExplosionVfx: false);

            Assert.That(options.Defines, Does.Contain(DiagnosticsDefine));
            Assert.That(options.Defines, Does.Not.Contain(DisableExplosionVfxDefine));
            Assert.That(options.OutputPath, Does.EndWith("FitMe-Performance-Diagnostics.apk"));
        }

        [Test]
        public void ExplosionVfxOffOptionsAddSuppressionDefineAndUseSeparateApkPath()
        {
            var options = ReadBuildOptions(disableExplosionVfx: true);

            Assert.That(options.Defines, Does.Contain(DiagnosticsDefine));
            Assert.That(options.Defines, Does.Contain(DisableExplosionVfxDefine));
            Assert.That(options.OutputPath, Does.EndWith("FitMe-Performance-Diagnostics-ExplosionVfxOff.apk"));
        }

        private static (string[] Defines, string OutputPath) ReadBuildOptions(bool disableExplosionVfx)
        {
            var editorAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .Single(assembly => assembly.GetName().Name == "Assembly-CSharp-Editor");
            var buildType = editorAssembly.GetType("QuackUp.Core.Editor.PerformanceDiagnosticsBuild");
            Assert.That(buildType, Is.Not.Null);
            if (buildType == null) return (Array.Empty<string>(), string.Empty);

            var createOptions = buildType.GetMethod("CreateBuildOptions", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(createOptions, Is.Not.Null,
                "The diagnostics builder should expose a side-effect-free options factory for verification.");
            if (createOptions == null) return (Array.Empty<string>(), string.Empty);

            var options = createOptions.Invoke(null, new object[] { Array.Empty<string>(), disableExplosionVfx });
            return (
                ReadBuildOptionMember<string[]>(options, "extraScriptingDefines"),
                ReadBuildOptionMember<string>(options, "locationPathName"));
        }

        private static T ReadBuildOptionMember<T>(object options, string memberName)
        {
            var optionsType = options.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
            var field = optionsType.GetField(memberName, flags);
            if (field != null) return (T)field.GetValue(options);

            var property = optionsType.GetProperty(memberName, flags);
            Assert.That(property, Is.Not.Null, $"BuildPlayerOptions.{memberName} should be readable.");
            return property == null ? default : (T)property.GetValue(options);
        }
    }
}