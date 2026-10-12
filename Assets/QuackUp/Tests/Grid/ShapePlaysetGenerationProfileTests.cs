using System.Collections.Generic;
using System.Reflection;
using FitMe.GameData;
using FitMe.Grid;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FitMe.Grid.Tests
{
    public sealed class ShapePlaysetGenerationProfileTests
    {
        private string _assetFolderPath;

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(_assetFolderPath)) AssetDatabase.DeleteAsset(_assetFolderPath);
            _assetFolderPath = null;
        }

        [Test]
        public void ReloadedProfileBuildsConfigFromSavedRangesBudgetsAndPreferredPresets()
        {
            _assetFolderPath = $"Assets/__ShapePlaysetGenerationProfileTests_{System.Guid.NewGuid():N}";
            AssetDatabase.CreateFolder("Assets", _assetFolderPath.Substring("Assets/".Length));
            var presetPath = $"{_assetFolderPath}/PreferredBlock.asset";
            var profilePath = $"{_assetFolderPath}/Profile.asset";
            var reloadedProfilePath = $"{_assetFolderPath}/ReloadedProfile.asset";

            var profileType = typeof(ShapeLevelDefinition).Assembly.GetType(
                "FitMe.GameData.ShapePlaysetGenerationProfile");
            Assert.That(profileType, Is.Not.Null,
                "Reusable generation settings must be represented by a profile ScriptableObject.");

            var preset = ScriptableObject.CreateInstance<BlockPreset>();
            preset.BlockSchema.schema = new[,] { { 1, 1 } };
            AssetDatabase.CreateAsset(preset, presetPath);
            AssetDatabase.SaveAssets();

            var profile = ScriptableObject.CreateInstance(profileType);
            SetPrivateField(profile, "_minBoardColumns", 4);
            SetPrivateField(profile, "_maxBoardColumns", 5);
            SetPrivateField(profile, "_minBoardRows", 3);
            SetPrivateField(profile, "_maxBoardRows", 4);
            SetPrivateField(profile, "_minObstacleCells", 1);
            SetPrivateField(profile, "_maxObstacleCells", 2);
            SetPrivateField(profile, "_minGeneratedBlockColumns", 1);
            SetPrivateField(profile, "_maxGeneratedBlockColumns", 3);
            SetPrivateField(profile, "_minGeneratedBlockRows", 1);
            SetPrivateField(profile, "_maxGeneratedBlockRows", 2);
            SetPrivateField(profile, "_minPlayableRegionCells", 6);
            SetPrivateField(profile, "_maxObstacleRepairNodes", 2500);
            SetPrivateField(profile, "_maxTilingSearchNodes", 4000);
            SetPrivateField(profile, "_maxCandidateAttempts", 7);
            SetPrivateField(profile, "_maxSingletonFallbackBlocks", 2);
            SetPrivateField(profile, "_preferredPlayerBlocks", new List<ShapePlayerBlockEntry>
            {
                new(preset, 2)
            });

            AssetDatabase.CreateAsset(profile, profilePath);
            AssetDatabase.SaveAssets();
            Assert.That(AssetDatabase.CopyAsset(profilePath, reloadedProfilePath), Is.True);
            AssetDatabase.ImportAsset(reloadedProfilePath, ImportAssetOptions.ForceUpdate);

            var reloadedProfile = AssetDatabase.LoadAssetAtPath(reloadedProfilePath, profileType);
            Assert.That(reloadedProfile, Is.Not.Null);
            var createConfig = profileType.GetMethod("CreateConfig", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(createConfig, Is.Not.Null,
                "The profile must expose an immutable runtime-config projection for the generator.");
            var config = (ShapePlaysetGenerationConfig)createConfig.Invoke(reloadedProfile, null);

            Assert.That(config.MinBoardColumns, Is.EqualTo(4));
            Assert.That(config.MaxBoardColumns, Is.EqualTo(5));
            Assert.That(config.MinBoardRows, Is.EqualTo(3));
            Assert.That(config.MaxBoardRows, Is.EqualTo(4));
            Assert.That(config.MinObstacleCells, Is.EqualTo(1));
            Assert.That(config.MaxObstacleCells, Is.EqualTo(2));
            Assert.That(config.MinGeneratedBlockColumns, Is.EqualTo(1));
            Assert.That(config.MaxGeneratedBlockColumns, Is.EqualTo(3));
            Assert.That(config.MinGeneratedBlockRows, Is.EqualTo(1));
            Assert.That(config.MaxGeneratedBlockRows, Is.EqualTo(2));
            Assert.That(config.MinPlayableRegionCells, Is.EqualTo(6));
            Assert.That(config.MaxObstacleRepairNodes, Is.EqualTo(2500));
            Assert.That(config.MaxTilingSearchNodes, Is.EqualTo(4000));
            Assert.That(config.MaxCandidateAttempts, Is.EqualTo(7));
            Assert.That(config.MaxSingletonFallbackBlocks, Is.EqualTo(2));
            Assert.That(config.PreferredPlayerBlocks, Has.Count.EqualTo(1));
            Assert.That(config.PreferredPlayerBlocks[0].Preset, Is.SameAs(
                AssetDatabase.LoadAssetAtPath<BlockPreset>(presetPath)));
            Assert.That(config.PreferredPlayerBlocks[0].Count, Is.EqualTo(2));
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Expected serialized field {fieldName}.");
            field.SetValue(target, value);
        }
    }
}
