using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using FitMe.GameData;
using FitMe.Grid;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace FitMe.Grid.Tests
{
    public sealed class ShapePlaysetGenerationProfileTests
    {
        private string _assetFolderPath;
        private readonly List<UnityEngine.Object> _transientAssets = new();

        [TearDown]
        public void TearDown()
        {
            for (var index = _transientAssets.Count - 1; index >= 0; index--)
                if (_transientAssets[index]) Object.DestroyImmediate(_transientAssets[index]);
            _transientAssets.Clear();
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

        [UnityTest]
        public IEnumerator GeneratedPlaysetConvertsToEditableMixedLevelAndRetainsOriginProvenance()
        {
            _assetFolderPath = $"Assets/__ShapePlaysetGenerationProfileTests_{System.Guid.NewGuid():N}";
            AssetDatabase.CreateFolder("Assets", _assetFolderPath.Substring("Assets/".Length));
            var presetPath = $"{_assetFolderPath}/PreferredBlock.asset";
            var levelPath = $"{_assetFolderPath}/GeneratedLevel.asset";
            var reloadedLevelPath = $"{_assetFolderPath}/ReloadedGeneratedLevel.asset";

            var presetAsset = ScriptableObject.CreateInstance<BlockPreset>();
            presetAsset.BlockSchema.schema = new[,] { { 1, 1 } };
            AssetDatabase.CreateAsset(presetAsset, presetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(presetPath, ImportAssetOptions.ForceUpdate);
            yield return null;
            AssetDatabase.SaveAssets();

            var preset = AssetDatabase.LoadAssetAtPath<BlockPreset>(presetPath);
            Assert.That(preset, Is.Not.Null);
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 2,
                maxBoardColumns: 2,
                minBoardRows: 2,
                maxBoardRows: 2,
                minObstacleCells: 0,
                maxObstacleCells: 0,
                minGeneratedBlockColumns: 1,
                maxGeneratedBlockColumns: 2,
                minGeneratedBlockRows: 1,
                maxGeneratedBlockRows: 2,
                preferredPlayerBlocks: new[] { new ShapePlayerBlockEntry(preset, 1) },
                minPlayableRegionCells: 1);
            var generation = ShapePlaysetGenerator.Generate(config, seed: 9);
            Assert.That(generation.IsSuccess, Is.True, generation.Error.ToString());
            Assert.That(generation.Playset.PlayerBlocks, Has.Count.EqualTo(2));
            Assert.That(generation.Playset.UsedPredefinedBlocks, Is.True);

            var level = Keep(ScriptableObject.CreateInstance<ShapeLevelDefinition>());
            var setter = typeof(ShapeLevelDefinition).GetMethod(
                "SetGeneratedContent",
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(GeneratedShapePlayset) },
                modifiers: null);
            Assert.That(setter, Is.Not.Null,
                "Generated output needs a conversion path into an editable ShapeLevelDefinition.");
            setter.Invoke(level, new object[] { generation.Playset });

            Assert.That(level.BoardSource, Is.EqualTo(ShapeBoardSource.Generated));
            AssertMatricesEqual(generation.Playset.BoardMask, level.GeneratedBoardMask);
            AssertMatricesEqual(generation.Playset.ObstacleMask, level.GeneratedObstacleMask);
            Assert.That(level.PlayerBlocks, Has.Count.EqualTo(generation.Playset.PlayerBlocks.Count));
            var presetBlockCount = 0;
            var inlineBlockCount = 0;
            for (var index = 0; index < generation.Playset.PlayerBlocks.Count; index++)
            {
                var generatedBlock = generation.Playset.PlayerBlocks[index];
                var authoredEntry = level.PlayerBlocks[index];
                Assert.That(authoredEntry.Count, Is.EqualTo(1));
                if (generatedBlock.IsPredefined)
                {
                    presetBlockCount++;
                    Assert.That(authoredEntry.Source, Is.EqualTo(ShapePlayerBlockSource.Preset));
                    Assert.That(authoredEntry.Preset, Is.SameAs(preset));
                }
                else
                {
                    inlineBlockCount++;
                    Assert.That(authoredEntry.Source, Is.EqualTo(ShapePlayerBlockSource.InlineFootprint));
                    AssertMatricesEqual(generatedBlock.Footprint, authoredEntry.InlineFootprint);
                }
            }

            Assert.That(presetBlockCount, Is.EqualTo(1));
            Assert.That(inlineBlockCount, Is.EqualTo(1));
            Assert.That(ShapeLevelDataValidator.Validate(level).IsValid, Is.True);
            var runtimeData = level.ToRuntimeData();
            Assert.That(runtimeData.PlayerBlockEntries, Has.Count.EqualTo(level.PlayerBlocks.Count));
            for (var index = 0; index < runtimeData.PlayerBlockEntries.Count; index++)
            {
                var authoredEntry = level.PlayerBlocks[index];
                var runtimeEntry = runtimeData.PlayerBlockEntries[index];
                Assert.That(runtimeEntry.Preset, Is.SameAs(authoredEntry.Preset));
                AssertMatricesEqual(
                    authoredEntry.Source == ShapePlayerBlockSource.Preset
                        ? authoredEntry.Preset.BlockSchema.schema
                        : authoredEntry.InlineFootprint,
                    runtimeEntry.Schema);
            }

            var provenanceProperty = typeof(ShapeLevelDefinition).GetProperty(
                "GenerationProvenance", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(provenanceProperty, Is.Not.Null,
                "Saved generated levels need explicit origin provenance, not just a profile reference.");
            var provenance = provenanceProperty.GetValue(level);
            Assert.That(provenance, Is.Not.Null);
            AssertProperty(provenance, "Seed", generation.Playset.Seed);
            AssertProperty(provenance, "AttemptIndex", generation.Playset.AttemptIndex);
            AssertProperty(provenance, "GeneratorVersion", generation.Playset.GeneratorVersion);
            AssertProperty(provenance, "IsOriginOnly", true);
            var provenanceConfiguration = GetProperty(provenance, "Configuration");
            AssertProperty(provenanceConfiguration, "MinBoardColumns", config.MinBoardColumns);
            AssertProperty(provenanceConfiguration, "MaxBoardRows", config.MaxBoardRows);
            AssertProperty(provenanceConfiguration, "MaxTilingSearchNodes", config.MaxTilingSearchNodes);
            var preferredSnapshots = GetProperty(provenanceConfiguration, "PreferredPlayerBlocks") as ICollection;
            Assert.That(preferredSnapshots, Is.Not.Null);
            Assert.That(preferredSnapshots.Count, Is.EqualTo(1));
            var preferredSnapshot = GetFirst(preferredSnapshots);
            AssertProperty(preferredSnapshot, "PresetId", preset.Id);
            AssertProperty(preferredSnapshot, "Count", 1);
            AssertMatricesEqual(preset.BlockSchema.schema, (int[,])GetProperty(preferredSnapshot, "PresetSchema"));
            var difficulty = GetProperty(provenance, "Difficulty");
            AssertProperty(difficulty, "IsProvisional", true);
            AssertProperty(difficulty, "PlayableCellCount", generation.Playset.Difficulty.PlayableCellCount);
            AssertProperty(difficulty, "ProvisionalDifficultyScore",
                generation.Playset.Difficulty.ProvisionalDifficultyScore);

            AssetDatabase.CreateAsset(level, levelPath);
            _transientAssets.Remove(level);
            AssetDatabase.SaveAssets();
            Assert.That(AssetDatabase.CopyAsset(levelPath, reloadedLevelPath), Is.True);
            AssetDatabase.ImportAsset(reloadedLevelPath, ImportAssetOptions.ForceUpdate);
            var reloadedLevel = AssetDatabase.LoadAssetAtPath<ShapeLevelDefinition>(reloadedLevelPath);
            Assert.That(reloadedLevel, Is.Not.Null);
            Assert.That(reloadedLevel.BoardSource, Is.EqualTo(ShapeBoardSource.Generated));
            AssertMatricesEqual(generation.Playset.BoardMask, reloadedLevel.GeneratedBoardMask);
            AssertMatricesEqual(generation.Playset.ObstacleMask, reloadedLevel.GeneratedObstacleMask);
            Assert.That(reloadedLevel.PlayerBlocks, Has.Count.EqualTo(level.PlayerBlocks.Count));
            Assert.That(reloadedLevel.PlayerBlocks[0].Source, Is.EqualTo(level.PlayerBlocks[0].Source));
            Assert.That(reloadedLevel.PlayerBlocks[1].Source, Is.EqualTo(level.PlayerBlocks[1].Source));
            Assert.That(reloadedLevel.PlayerBlocks[0].Preset, Is.SameAs(level.PlayerBlocks[0].Preset));
            Assert.That(reloadedLevel.PlayerBlocks[1].Preset, Is.SameAs(level.PlayerBlocks[1].Preset));
            Assert.That(reloadedLevel.ToRuntimeData().PlayerBlockEntries, Has.Count.EqualTo(2));
            var reloadedProvenance = provenanceProperty.GetValue(reloadedLevel);
            Assert.That(reloadedProvenance, Is.Not.Null);
            AssertProperty(reloadedProvenance, "Seed", generation.Playset.Seed);
            AssertProperty(reloadedProvenance, "GeneratorVersion", generation.Playset.GeneratorVersion);
            AssertProperty(GetProperty(reloadedProvenance, "Difficulty"), "IsProvisional", true);
            Assert.That(AssetDatabase.FindAssets("t:BlockPreset", new[] { _assetFolderPath }),
                Has.Length.EqualTo(1), "Saving a generated level must not create new BlockPreset assets.");
        }

        [Test]
        public void GenerationDoesNotMutateProfileSettingsOrPreferredBlockSchemas()
        {
            var profileType = typeof(ShapeLevelDefinition).Assembly.GetType(
                "FitMe.GameData.ShapePlaysetGenerationProfile");
            Assert.That(profileType, Is.Not.Null);
            var profile = Keep(ScriptableObject.CreateInstance(profileType));
            var preset = Keep(ScriptableObject.CreateInstance<BlockPreset>());
            var originalSchema = new[,] { { 1, 1 } };
            preset.BlockSchema.schema = (int[,])originalSchema.Clone();
            SetPrivateField(profile, "_minBoardColumns", 2);
            SetPrivateField(profile, "_maxBoardColumns", 2);
            SetPrivateField(profile, "_minBoardRows", 2);
            SetPrivateField(profile, "_maxBoardRows", 2);
            SetPrivateField(profile, "_minObstacleCells", 0);
            SetPrivateField(profile, "_maxObstacleCells", 0);
            SetPrivateField(profile, "_minGeneratedBlockColumns", 1);
            SetPrivateField(profile, "_maxGeneratedBlockColumns", 2);
            SetPrivateField(profile, "_minGeneratedBlockRows", 1);
            SetPrivateField(profile, "_maxGeneratedBlockRows", 2);
            SetPrivateField(profile, "_minPlayableRegionCells", 1);
            SetPrivateField(profile, "_maxCandidateAttempts", 1);
            SetPrivateField(profile, "_preferredPlayerBlocks", new List<ShapePlayerBlockEntry>
            {
                new(preset, 1)
            });

            var createConfig = profileType.GetMethod("CreateConfig", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(createConfig, Is.Not.Null);
            var config = (ShapePlaysetGenerationConfig)createConfig.Invoke(profile, null);
            var result = ShapePlaysetGenerator.Generate(config, seed: 9);

            Assert.That(result.IsSuccess, Is.True, result.Error.ToString());
            Assert.That(GetPrivateField(profile, "_minBoardColumns"), Is.EqualTo(2));
            Assert.That(GetPrivateField(profile, "_maxBoardColumns"), Is.EqualTo(2));
            var preferredBlocks = (List<ShapePlayerBlockEntry>)GetPrivateField(profile, "_preferredPlayerBlocks");
            Assert.That(preferredBlocks, Has.Count.EqualTo(1));
            Assert.That(preferredBlocks[0].Preset, Is.SameAs(preset));
            Assert.That(preferredBlocks[0].Count, Is.EqualTo(1));
            AssertMatricesEqual(originalSchema, preset.BlockSchema.schema);
        }

        [Test]
        public void EditorAssetCreatorCreatesInlineLevelAndNeverOverwritesExistingAsset()
        {
            _assetFolderPath = $"Assets/__ShapePlaysetGenerationProfileTests_{System.Guid.NewGuid():N}";
            AssetDatabase.CreateFolder("Assets", _assetFolderPath.Substring("Assets/".Length));
            var levelPath = $"{_assetFolderPath}/GeneratedLevel.asset";
            var config = new ShapePlaysetGenerationConfig(
                minBoardColumns: 2,
                maxBoardColumns: 2,
                minBoardRows: 2,
                maxBoardRows: 2,
                minObstacleCells: 0,
                maxObstacleCells: 0,
                minGeneratedBlockColumns: 1,
                maxGeneratedBlockColumns: 2,
                minGeneratedBlockRows: 1,
                maxGeneratedBlockRows: 2,
                minPlayableRegionCells: 1);
            var generation = ShapePlaysetGenerator.Generate(config, seed: 9);
            Assert.That(generation.IsSuccess, Is.True, generation.Error.ToString());

            var creatorType = System.Type.GetType(
                "FitMe.GameData.Editor.ShapePlaysetGeneratedLevelAssetCreator, FitMe.GameData.Editor");
            Assert.That(creatorType, Is.Not.Null,
                "Saving generated content must be implemented in an Editor-only asset creator.");
            var createAsset = creatorType.GetMethod(
                "CreateNewLevelAsset",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(GeneratedShapePlayset), typeof(string) },
                modifiers: null);
            Assert.That(createAsset, Is.Not.Null);

            var createdLevel = (ShapeLevelDefinition)createAsset.Invoke(
                null, new object[] { generation.Playset, levelPath });
            Assert.That(createdLevel, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(createdLevel), Is.EqualTo(levelPath));
            Assert.That(AssetDatabase.LoadAssetAtPath<ShapeLevelDefinition>(levelPath), Is.SameAs(createdLevel));
            Assert.That(ShapeLevelDataValidator.Validate(createdLevel).IsValid, Is.True);
            Assert.That(createdLevel.BoardSource, Is.EqualTo(ShapeBoardSource.Generated));
            Assert.That(createdLevel.PlayerBlocks, Is.Not.Empty);
            Assert.That(AssetDatabase.FindAssets("t:BlockPreset", new[] { _assetFolderPath }),
                Is.Empty, "Saving generated levels must not create BlockPreset assets.");

            var overwriteError = Assert.Throws<TargetInvocationException>(() =>
                createAsset.Invoke(null, new object[] { generation.Playset, levelPath }));
            Assert.That(overwriteError.InnerException, Is.TypeOf<System.InvalidOperationException>());
            Assert.That(AssetDatabase.LoadAssetAtPath<ShapeLevelDefinition>(levelPath), Is.SameAs(createdLevel));
        }

        private T Keep<T>(T asset) where T : UnityEngine.Object
        {
            _transientAssets.Add(asset);
            return asset;
        }

        private static object GetProperty(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"Expected property {propertyName} on {target.GetType().Name}.");
            return property.GetValue(target);
        }

        private static void AssertProperty(object target, string propertyName, object expected)
        {
            Assert.That(GetProperty(target, propertyName), Is.EqualTo(expected),
                $"Unexpected {propertyName} on {target.GetType().Name}.");
        }

        private static object GetFirst(ICollection collection)
        {
            foreach (var item in collection) return item;
            Assert.Fail("Expected at least one item.");
            return null;
        }

        private static void AssertMatricesEqual(int[,] expected, int[,] actual)
        {
            Assert.That(actual, Is.Not.Null);
            Assert.That(actual.GetLength(0), Is.EqualTo(expected.GetLength(0)));
            Assert.That(actual.GetLength(1), Is.EqualTo(expected.GetLength(1)));
            for (var row = 0; row < expected.GetLength(0); row++)
            for (var column = 0; column < expected.GetLength(1); column++)
                Assert.That(actual[row, column], Is.EqualTo(expected[row, column]),
                    $"Matrix mismatch at row {row}, column {column}.");
        }

        private static object GetPrivateField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Expected serialized field {fieldName}.");
            return field.GetValue(target);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Expected serialized field {fieldName}.");
            field.SetValue(target, value);
        }
    }
}
