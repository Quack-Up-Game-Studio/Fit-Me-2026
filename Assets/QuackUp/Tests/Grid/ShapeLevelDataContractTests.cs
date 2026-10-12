using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using FitMe.GameData;
using FitMe.Grid;
using FitMe.Scene;
using FitMe.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace FitMe.Grid.Tests
{
    public sealed class ShapeLevelDataContractTests
    {
        private readonly List<ScriptableObject> _createdAssets = new();
        private string _assetFolderPath;

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _createdAssets)
            {
                if (asset) UnityEngine.Object.DestroyImmediate(asset);
            }

            _createdAssets.Clear();
            if (!string.IsNullOrEmpty(_assetFolderPath)) AssetDatabase.DeleteAsset(_assetFolderPath);
            _assetFolderPath = null;
        }

        [Test]
        public void ShapeLevelDefinition_PlayerBlockEntriesSerializePresetAndCount()
        {
            var playerBlocksType = typeof(ShapeLevelDefinition).GetProperty("PlayerBlocks")?.PropertyType;

            Assert.That(playerBlocksType, Is.Not.Null);
            var entryType = playerBlocksType.GetGenericArguments()[0];
            Assert.That(entryType, Is.EqualTo(typeof(ShapePlayerBlockEntry)));
            Assert.That(entryType.IsSerializable, Is.True);
            Assert.That(entryType.GetProperty("Preset")?.PropertyType, Is.EqualTo(typeof(BlockPreset)));
            Assert.That(entryType.GetProperty("Count")?.PropertyType, Is.EqualTo(typeof(int)));
            Assert.That(entryType.GetField("_preset", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.IsDefined(typeof(SerializeField), inherit: true), Is.True);
            Assert.That(entryType.GetField("_count", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.IsDefined(typeof(SerializeField), inherit: true), Is.True);
        }

        [Test]
        public void ShapeLevelDefinition_InlineFootprintsRemainEditableAndOrderedInRuntimeData()
        {
            var inlineEntryConstructor = typeof(ShapePlayerBlockEntry).GetConstructor(
                new[] { typeof(int[,]), typeof(int) });
            Assert.That(inlineEntryConstructor, Is.Not.Null,
                "Inline footprint entries need the same direct schema authoring model as preset-backed entries.");

            var level = CreateValidLevel();
            var inlineEntry = (ShapePlayerBlockEntry)inlineEntryConstructor.Invoke(new object[]
            {
                new[,] { { 1, 1 }, { 1, 0 } },
                1
            });
            var singleCellPreset = CreateBlockPreset(new[,] { { 1 } });
            SetPrivateField(level, "_playerBlocks", new List<ShapePlayerBlockEntry>
            {
                inlineEntry,
                new ShapePlayerBlockEntry(singleCellPreset, 1)
            });

            var runtimeData = level.ToRuntimeData();
            var entriesProperty = runtimeData.GetType().GetProperty("PlayerBlockEntries");
            Assert.That(entriesProperty, Is.Not.Null,
                "The normalized runtime contract must retain ordered inline and preset-backed block schemas.");
            var entries = (IList)entriesProperty.GetValue(runtimeData);

            Assert.That(entries, Has.Count.EqualTo(2));
            Assert.That(entries[0].GetType().GetProperty("Schema").GetValue(entries[0]),
                Is.EqualTo(new[,] { { 1, 1 }, { 1, 0 } }));
            Assert.That(entries[0].GetType().GetProperty("Preset").GetValue(entries[0]), Is.Null);
            Assert.That(entries[1].GetType().GetProperty("Preset").GetValue(entries[1]), Is.SameAs(singleCellPreset));
        }

        [Test]
        public void InlineFootprintInspectorUsesOdinMatrixAndPreservingResizeAction()
        {
            var entry = new ShapePlayerBlockEntry(new[,] { { 1, 1 }, { 1, 0 } }, 1);
            var footprintProperty = typeof(ShapePlayerBlockEntry).GetProperty(nameof(ShapePlayerBlockEntry.InlineFootprint));
            Assert.That(footprintProperty, Is.Not.Null);
            Assert.That(Array.Exists(footprintProperty.GetCustomAttributes(false),
                attribute => attribute.GetType().Name == "TableMatrixAttribute"), Is.True,
                "Inline footprints need the Odin cell-matrix editor.");

            var sizeBackingField = typeof(ShapePlayerBlockEntry).GetField(
                "<InlineFootprintSize>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(sizeBackingField, Is.Not.Null);
            Assert.That(sizeBackingField.IsDefined(typeof(SerializeField), inherit: true), Is.True,
                "Current footprint dimensions must survive asset serialization.");
            Assert.That(Array.Exists(sizeBackingField.GetCustomAttributes(false),
                attribute => attribute.GetType().Name == "ReadOnlyAttribute"), Is.True,
                "Current dimensions must not be edited before the resize action runs.");
            var targetProperty = typeof(ShapePlayerBlockEntry).GetProperty(
                nameof(ShapePlayerBlockEntry.InlineFootprintResizeTarget));
            Assert.That(targetProperty, Is.Not.Null);
            var targetBackingField = typeof(ShapePlayerBlockEntry).GetField(
                "<InlineFootprintResizeTarget>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(targetBackingField, Is.Not.Null);
            Assert.That(targetBackingField.IsDefined(typeof(SerializeField), inherit: true), Is.True,
                "Requested dimensions must survive asset serialization.");
            Assert.That(Array.Exists(targetBackingField.GetCustomAttributes(false),
                attribute => attribute.GetType().Name == "ReadOnlyAttribute"), Is.False,
                "Requested resize dimensions must be editable in the Inspector.");
            var resizeMethod = typeof(ShapePlayerBlockEntry).GetMethod(
                "RefreshInlineFootprint", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(resizeMethod, Is.Not.Null);
            Assert.That(Array.Exists(resizeMethod.GetCustomAttributes(false),
                attribute => attribute.GetType().Name == "ButtonAttribute"), Is.True,
                "The Inspector must expose a resize/refresh action.");

            var setSize = targetProperty.GetSetMethod(nonPublic: true);
            Assert.That(setSize, Is.Not.Null);
            setSize.Invoke(entry, new object[] { new Vector2Int(3, 1) });
            resizeMethod.Invoke(entry, null);

            Assert.That(entry.InlineFootprintSize, Is.EqualTo(new Vector2Int(3, 1)));
            Assert.That(entry.InlineFootprintResizeTarget, Is.EqualTo(new Vector2Int(3, 1)));
            Assert.That(entry.InlineFootprint, Is.EqualTo(new[,] { { 1, 1, 0 } }));

            var legacyEntry = new ShapePlayerBlockEntry(new[,] { { 1, 1 }, { 1, 0 } }, 1);
            setSize.Invoke(legacyEntry, new object[] { Vector2Int.zero });
            resizeMethod.Invoke(legacyEntry, null);
            Assert.That(legacyEntry.InlineFootprintSize, Is.EqualTo(new Vector2Int(2, 2)));
            Assert.That(legacyEntry.InlineFootprint, Is.EqualTo(new[,] { { 1, 1 }, { 1, 0 } }));
        }

        [Test]
        public void Validate_RejectsDiagonalOnlyInlinePlayerBlockFootprint()
        {
            var level = CreateValidLevel();
            SetPrivateField(level, "_playerBlocks", new List<ShapePlayerBlockEntry>
            {
                new ShapePlayerBlockEntry(new[,] { { 1, 0 }, { 0, 1 } }, 1)
            });

            var result = ShapeLevelDataValidator.Validate(level);

            Assert.That(result.Errors, Does.Contain(ShapeLevelValidationError.InvalidPlayerBlockSchema));
        }

        [Test]
        public void ShapeLevelDefinition_UsesOdinSerializationWithoutChangingManualDefault()
        {
            Assert.That(typeof(ShapeLevelDefinition).BaseType.FullName,
                Is.EqualTo("Sirenix.OdinInspector.SerializedScriptableObject"));

            var level = CreateValidLevel();
            var boardSourceProperty = typeof(ShapeLevelDefinition).GetProperty("BoardSource");
            Assert.That(boardSourceProperty, Is.Not.Null,
                "The level Inspector must expose the Manual/Generated board-source dropdown.");
            Assert.That(boardSourceProperty.GetValue(level).ToString(), Is.EqualTo("Manual"));
        }

        [Test]
        public void ShapeLevelDefinition_ExpandsGroupedCountsToIndividualRuntimeBlocksInOrder()
        {
            var level = CreateValidLevel();
            var singleCellPreset = CreateBlockPreset(new[,] { { 1 } });
            var twoCellPreset = CreateBlockPreset(new[,] { { 1, 1 } });
            SetPrivateField(level, "_playerBlocks", new List<ShapePlayerBlockEntry>
            {
                new ShapePlayerBlockEntry(singleCellPreset, 2),
                new ShapePlayerBlockEntry(twoCellPreset, 1)
            });

            var runtimeData = level.ToRuntimeData();

            Assert.That(runtimeData.PlayerBlocks, Has.Count.EqualTo(3));
            Assert.That(runtimeData.PlayerBlocks[0], Is.SameAs(singleCellPreset));
            Assert.That(runtimeData.PlayerBlocks[1], Is.SameAs(singleCellPreset));
            Assert.That(runtimeData.PlayerBlocks[2], Is.SameAs(twoCellPreset));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Validate_RejectsNonPositivePlayerBlockEntryCount(int count)
        {
            var level = CreateValidLevel();
            var preset = CreateBlockPreset(new[,] { { 1, 1 } });
            SetPrivateField(level, "_playerBlocks", new List<ShapePlayerBlockEntry>
            {
                new ShapePlayerBlockEntry(preset, count)
            });

            var result = ShapeLevelDataValidator.Validate(level);
            Assert.That(result.Errors, Does.Contain(ShapeLevelValidationError.InvalidPlayerBlockCount));
        }

        [Test]
        public void Validate_RejectsDiagonalOnlyPlayerBlockPreset()
        {
            var level = CreateValidLevel();
            SetPrivateField(level, "_playerBlocks", new List<ShapePlayerBlockEntry>
            {
                new ShapePlayerBlockEntry(CreateBlockPreset(new[,] { { 1, 0 }, { 0, 1 } }), 1)
            });

            var result = ShapeLevelDataValidator.Validate(level);

            Assert.That(result.Errors, Does.Contain(ShapeLevelValidationError.InvalidPlayerBlockSchema));
        }

        [Test]
        public void Validate_AcceptsFourConnectedPresetWithInternalEmptyCells()
        {
            var level = CreateValidLevel();
            SetPrivateField(level, "_playerBlocks", new List<ShapePlayerBlockEntry>
            {
                new ShapePlayerBlockEntry(
                    CreateBlockPreset(new[,] { { 1, 1, 1 }, { 1, 0, 0 } }), 1)
            });

            var result = ShapeLevelDataValidator.Validate(level);

            Assert.That(result.IsValid, Is.True, string.Join("\n", result.Errors));
        }

        [Test]
        public void ShapeLevelDefinition_DoesNotStoreSeparateIdOrOrder()
        {
            Assert.That(typeof(ShapeLevelDefinition).GetProperty("LevelId"), Is.Null);
            Assert.That(typeof(ShapeLevelDefinition).GetProperty("Order"), Is.Null);
        }

        [Test]
        public void ShapeLevelCatalog_UsesOdinSerializedOrderedDictionaryAsItsSource()
        {
            Assert.That(typeof(ShapeLevelCatalog).BaseType.FullName,
                Is.EqualTo("Sirenix.OdinInspector.SerializedScriptableObject"));

            var levelsField = typeof(ShapeLevelCatalog).GetField("_levels", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(levelsField, Is.Not.Null);
            Assert.That(levelsField.FieldType.Name, Is.EqualTo("OrderedDictionary`2"));
            Assert.That(Array.Exists(levelsField.GetCustomAttributes(false), attribute =>
                attribute.GetType().Name == "OdinSerializeAttribute"), Is.True);
        }

        [Test]
        public void Catalog_KeyAndInsertionPositionAreAuthoritative()
        {
            var first = Keep(ScriptableObject.CreateInstance<ShapeLevelDefinition>());
            var second = Keep(ScriptableObject.CreateInstance<ShapeLevelDefinition>());
            var catalog = Keep(ScriptableObject.CreateInstance<ShapeLevelCatalog>());
            var levelsField = typeof(ShapeLevelCatalog).GetField("_levels", BindingFlags.Instance | BindingFlags.NonPublic);
            var levels = Activator.CreateInstance(levelsField.FieldType);
            var add = levelsField.FieldType.GetMethod("Add", new[] { typeof(string), typeof(ShapeLevelDefinition) });
            add.Invoke(levels, new object[] { "authoritative-key-1", first });
            add.Invoke(levels, new object[] { "authoritative-key-2", second });
            levelsField.SetValue(catalog, levels);

            Assert.That(catalog.TryGetById("authoritative-key-1", out var byKey), Is.True);
            Assert.That(byKey, Is.SameAs(first));
            Assert.That(catalog.GetOrderedLevels()[0], Is.SameAs(first));
            Assert.That(catalog.GetOrderedLevels()[1], Is.SameAs(second));
        }

        [Test]
        public void ShapeLevelDefinition_GeneratedInlineContentSurvivesAssetSaveAndReload()
        {
            _assetFolderPath = $"Assets/__ShapeLevelSerializationTests_{Guid.NewGuid():N}";
            AssetDatabase.CreateFolder("Assets", _assetFolderPath.Substring("Assets/".Length));
            var assetPath = $"{_assetFolderPath}/Generated.asset";
            var reloadedAssetPath = $"{_assetFolderPath}/Reloaded.asset";
            var boardMask = new[,] { { 1, 1, 1 }, { 1, 1, 1 } };
            var obstacleMask = new int[2, 3];
            var inlineFootprint = new[,] { { 1, 1, 1 }, { 1, 1, 1 } };
            var level = ScriptableObject.CreateInstance<ShapeLevelDefinition>();
            level.SetGeneratedContent(boardMask, obstacleMask, new[]
            {
                new ShapePlayerBlockEntry(inlineFootprint, 1)
            });

            AssetDatabase.CreateAsset(level, assetPath);
            AssetDatabase.SaveAssets();
            Assert.That(AssetDatabase.CopyAsset(assetPath, reloadedAssetPath), Is.True);
            AssetDatabase.ImportAsset(reloadedAssetPath, ImportAssetOptions.ForceUpdate);

            var reloaded = AssetDatabase.LoadAssetAtPath<ShapeLevelDefinition>(reloadedAssetPath);
            Assert.That(reloaded, Is.Not.Null);
            Assert.That(reloaded.BoardSource, Is.EqualTo(ShapeBoardSource.Generated));
            Assert.That(reloaded.GeneratedBoardMask, Is.EqualTo(boardMask));
            Assert.That(reloaded.GeneratedObstacleMask, Is.EqualTo(obstacleMask));
            Assert.That(reloaded.PlayerBlocks, Has.Count.EqualTo(1));
            Assert.That(reloaded.PlayerBlocks[0].InlineFootprint, Is.EqualTo(inlineFootprint));
            Assert.That(reloaded.PlayerBlocks[0].InlineFootprintSize, Is.EqualTo(new Vector2Int(3, 2)));
            Assert.That(reloaded.PlayerBlocks[0].InlineFootprintResizeTarget, Is.EqualTo(new Vector2Int(3, 2)));
            Assert.That(reloaded.ToRuntimeData().PlayableMask, Is.EqualTo(boardMask));
        }

        [Test]
        public void ShapeLevelCatalog_SurvivesAssetSaveAndReload()
        {
            _assetFolderPath = $"Assets/__ShapeLevelCatalogSerializationTests_{Guid.NewGuid():N}";
            AssetDatabase.CreateFolder("Assets", _assetFolderPath.Substring("Assets/".Length));
            var assetPath = $"{_assetFolderPath}/Catalog.asset";
            var reloadedAssetPath = $"{_assetFolderPath}/ReloadedCatalog.asset";
            var catalog = ScriptableObject.CreateInstance<ShapeLevelCatalog>();

            AssetDatabase.CreateAsset(catalog, assetPath);
            AssetDatabase.SaveAssets();
            Assert.That(AssetDatabase.CopyAsset(assetPath, reloadedAssetPath), Is.True);
            AssetDatabase.ImportAsset(reloadedAssetPath, ImportAssetOptions.ForceUpdate);

            var reloaded = AssetDatabase.LoadAssetAtPath<ShapeLevelCatalog>(reloadedAssetPath);
            Assert.That(reloaded, Is.Not.Null);
            Assert.That(reloaded.Levels, Is.Not.Null);
            Assert.That(reloaded.GetOrderedLevels(), Is.Empty);
        }

        [Test]
        public void ShapeLevelDefinition_UsesGridPresetObstacleMatrixForRuntimeData()
        {
            var level = CreateValidLevel();
            typeof(GridPreset).GetProperty(nameof(GridPreset.ObstacleMode))
                .SetValue(level.Board, ObstacleMode.Custom);
            level.Board.GridSize = new Vector2Int(4, 4);
            level.Board.customGrid = new int[4, 4];
            for (var row = 0; row < 4; row++)
            for (var column = 0; column < 4; column++)
                level.Board.customGrid[row, column] = 1;
            level.Board.CustomObstacleData = new ObstacleData[4, 4];
            for (var row = 0; row < 4; row++)
            for (var column = 0; column < 4; column++)
            {
                level.Board.CustomObstacleData[row, column] = new ObstacleData
                {
                    hasCell = true,
                    hasObstacle = row == 0 && column == 0 ||
                                  row == 1 && (column == 0 || column == 1) ||
                                  row == 2 && column == 2 ||
                                  row == 3 && (column == 2 || column == 3),
                    shape = (BlockShape)((row + column) % Enum.GetValues(typeof(BlockShape)).Length),
                    id = row * 10 + column,
                    randomColor = (row + column) % 2 == 0,
                    color = BlockColor.Blue,
                    petrified = true
                };
            }
            SetPrivateField(level, "_playerBlocks", new List<ShapePlayerBlockEntry>
            {
                CreatePlayerBlockEntry(new[,] { { 1, 1 } }, 5)
            });

            var runtimeData = level.ToRuntimeData();

            Assert.That(runtimeData.Obstacles, Has.Count.EqualTo(2), "Diagonally touching congruent components remain separate 4-connected obstacles.");
            Assert.That(runtimeData.Obstacles[0].Schema.schema, Is.EqualTo(new[,] { { 1, 0 }, { 1, 1 } }));
            Assert.That(runtimeData.Obstacles[0].Anchor, Is.EqualTo(Vector2Int.zero));
            Assert.That(runtimeData.Obstacles[0].Color, Is.EqualTo(BlockColor.Blue));
            Assert.That(runtimeData.Obstacles[0].Petrified, Is.True);
            Assert.That(runtimeData.Obstacles[1].Schema.schema, Is.EqualTo(new[,] { { 1, 0 }, { 1, 1 } }));
            Assert.That(runtimeData.Obstacles[1].Anchor, Is.EqualTo(new Vector2Int(2, 2)));
        }

        private BlockPreset CreateBlockPreset(int[,] schema)
        {
            var preset = Keep(ScriptableObject.CreateInstance<BlockPreset>());
            preset.BlockSchema.schema = schema;
            return preset;
        }

        private ShapePlayerBlockEntry CreatePlayerBlockEntry(int[,] schema, int count)
        {
            return new ShapePlayerBlockEntry(CreateBlockPreset(schema), count);
        }

        [Test]
        public void Validate_AcceptsExactCapacityAndObstacleOccupancy()
        {
            var level = CreateValidLevel();

            var result = ShapeLevelDataValidator.Validate(level);

            Assert.That(result.IsValid, Is.True, string.Join("\n", result.Errors));
        }

        [Test]
        public void Validate_RejectsObstacleOnVoidCell()
        {
            var level = CreateValidLevel();
            level.Board.customGrid[0, 0] = 0;

            var voidCellResult = ShapeLevelDataValidator.Validate(level);
            Assert.That(voidCellResult.Errors, Does.Contain(ShapeLevelValidationError.ObstacleOnVoidCell));
        }

        [Test]
        public void Validate_RejectsBlockCapacityMismatch()
        {
            var level = CreateValidLevel();
            SetPrivateField(level, "_playerBlocks", new List<ShapePlayerBlockEntry>
            {
                new ShapePlayerBlockEntry(CreateBlockPreset(new[,] { { 1, 1 } }), 1)
            });

            var result = ShapeLevelDataValidator.Validate(level);

            Assert.That(result.Errors, Does.Contain(ShapeLevelValidationError.BlockCapacityMismatch));
        }

        [Test]
        public void LevelSessionRequest_PreservesShapeDefinitionAndUsesItsBoard()
        {
            var board = Keep(ScriptableObject.CreateInstance<GridPreset>());
            var definition = Keep(ScriptableObject.CreateInstance<ShapeLevelDefinition>());
            SetPrivateField(definition, "_board", board);

            var request = LevelSessionRequest.ForShapeLevel(definition);

            Assert.That(request.ShapeLevel, Is.SameAs(definition));
            Assert.That(request.GridPreset, Is.SameAs(board));
        }

        [Test]
        public void LevelSessionRequest_CreatesRuntimeDataFromSelectedShapeDefinition()
        {
            var level = CreateValidLevel();
            var request = LevelSessionRequest.ForShapeLevel(level);
            var method = typeof(LevelSessionRequest).GetMethod("CreateShapeRuntimeData");
            Assert.That(method, Is.Not.Null, "Session request must expose its selected Shape Mode runtime data");

            var runtimeData = (ShapeLevelRuntimeData)method.Invoke(request, null);

            Assert.That(runtimeData.Board, Is.SameAs(level.Board));
            Assert.That(runtimeData.PlayerBlocks, Has.Count.EqualTo(2));
            Assert.That(runtimeData.Obstacles, Has.Count.EqualTo(1));
        }

        [Test]
        public void LevelSessionRequest_DoesNotApplyShapeDataToClassicMode()
        {
            var level = CreateValidLevel();
            var request = new LevelSessionRequest(GameMode.Classic, level.Board, level);

            Assert.That(request.ShapeLevel, Is.Null);
            Assert.That(request.CreateShapeRuntimeData(), Is.Null);
        }

        [Test]
        public void ShapeLevelDefinition_ToRuntimeDataCarriesBoardAndAuthoredGeometry()
        {
            var level = CreateValidLevel();

            var runtimeData = level.ToRuntimeData();

            Assert.That(runtimeData.Board, Is.SameAs(level.Board));
            Assert.That(runtimeData.PlayerBlocks, Has.Count.EqualTo(2));
            Assert.That(runtimeData.PlayerBlocks[0], Is.SameAs(level.PlayerBlocks[0].Preset));
            Assert.That(runtimeData.Obstacles, Has.Count.EqualTo(1));
            Assert.That(runtimeData.Obstacles[0].Schema.schema, Is.EqualTo(new[,] { { 1 }, { 1 } }));
            Assert.That(runtimeData.Obstacles[0].Anchor, Is.EqualTo(Vector2Int.zero));
            Assert.That(runtimeData.Obstacles[0].Color, Is.EqualTo(BlockColor.Red));
            Assert.That(runtimeData.Obstacles[0].Petrified, Is.True);
        }

        [Test]
        public void Catalog_LookupUsesKeyAndInsertionPositionForLevelIdentityAndProgression()
        {
            var first = CreateValidLevel();
            var second = CreateValidLevel();
            var catalog = CreateCatalog(("shape-001", first), ("shape-002", second));

            Assert.That(catalog.TryGetById("shape-001", out var byId), Is.True);
            Assert.That(byId, Is.SameAs(first));
            Assert.That(catalog.GetOrderedLevels()[0], Is.SameAs(first));
            Assert.That(catalog.GetOrderedLevels()[1], Is.SameAs(second));
        }

        [Test]
        public void Catalog_ValidationAllowsSameDefinitionUnderDistinctAuthoritativeKeys()
        {
            var level = CreateValidLevel();
            var catalog = CreateCatalog(("first-key", level), ("second-key", level));

            var result = catalog.Validate();

            Assert.That(result.IsValid, Is.True, string.Join("\n", result.Errors));
        }

        [UnityTest]
        public IEnumerator BlockPreset_IdTracksAssetGuidAndIsDifferentAfterDuplication()
        {
            _assetFolderPath = $"Assets/__ShapeLevelDataTests_{Guid.NewGuid():N}";
            AssetDatabase.CreateFolder("Assets", _assetFolderPath.Substring("Assets/".Length));

            var sourcePath = $"{_assetFolderPath}/Source.asset";
            var duplicatePath = $"{_assetFolderPath}/Duplicate.asset";
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<BlockPreset>(), sourcePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(sourcePath, ImportAssetOptions.ForceUpdate);
            yield return null;
            AssetDatabase.SaveAssets();

            var source = AssetDatabase.LoadAssetAtPath<BlockPreset>(sourcePath);
            var sourceId = AssetDatabase.AssetPathToGUID(sourcePath);
            Assert.That(source.Id, Is.EqualTo(sourceId));

            Assert.That(AssetDatabase.CopyAsset(sourcePath, duplicatePath), Is.True);
            AssetDatabase.ImportAsset(duplicatePath, ImportAssetOptions.ForceUpdate);
            var duplicate = AssetDatabase.LoadAssetAtPath<BlockPreset>(duplicatePath);
            yield return null;
            AssetDatabase.SaveAssets();
            duplicate = AssetDatabase.LoadAssetAtPath<BlockPreset>(duplicatePath);

            Assert.That(duplicate.Id, Is.EqualTo(AssetDatabase.AssetPathToGUID(duplicatePath)));
            Assert.That(duplicate.Id, Is.Not.EqualTo(source.Id));
            typeof(BlockPreset).GetProperty(nameof(BlockPreset.DisplayName))
                .SetValue(duplicate, "Readable label");
            Assert.That(duplicate.Id, Is.EqualTo(AssetDatabase.AssetPathToGUID(duplicatePath)));
            Assert.That(duplicate.DisplayName, Is.EqualTo("Readable label"));
        }

        private ShapeLevelDefinition CreateValidLevel()
        {
            var board = Keep(ScriptableObject.CreateInstance<GridPreset>());
            board.PresetGridType = GridType.Custom;
            board.GridSize = new Vector2Int(3, 2);
            board.customGrid = new[,] { { 1, 1, 1 }, { 1, 1, 1 } };
            typeof(GridPreset).GetProperty(nameof(GridPreset.ObstacleMode))
                .SetValue(board, ObstacleMode.Custom);
            board.CustomObstacleData = new ObstacleData[2, 3];
            for (var row = 0; row < 2; row++)
            for (var column = 0; column < 3; column++)
            {
                board.CustomObstacleData[row, column] = new ObstacleData
                {
                    hasCell = true,
                    hasObstacle = column == 0,
                    shape = (BlockShape)((row + column) % Enum.GetValues(typeof(BlockShape)).Length),
                    id = row + column,
                    randomColor = true,
                    color = BlockColor.Red,
                    petrified = true
                };
            }

            var definition = Keep(ScriptableObject.CreateInstance<ShapeLevelDefinition>());
            SetPrivateField(definition, "_board", board);
            SetPrivateField(definition, "_playerBlocks", new List<ShapePlayerBlockEntry>
            {
                CreatePlayerBlockEntry(new[,] { { 1, 1 } }, 2)
            });
            return definition;
        }

        private ShapeLevelCatalog CreateCatalog(params (string key, ShapeLevelDefinition level)[] entries)
        {
            var catalog = Keep(ScriptableObject.CreateInstance<ShapeLevelCatalog>());
            var levelsField = typeof(ShapeLevelCatalog).GetField("_levels", BindingFlags.Instance | BindingFlags.NonPublic);
            var levels = Activator.CreateInstance(levelsField.FieldType);
            var add = levelsField.FieldType.GetMethod("Add", new[] { typeof(string), typeof(ShapeLevelDefinition) });
            foreach (var (key, level) in entries) add.Invoke(levels, new object[] { key, level });
            levelsField.SetValue(catalog, levels);
            return catalog;
        }

        private T Keep<T>(T asset) where T : ScriptableObject
        {
            _createdAssets.Add(asset);
            return asset;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Expected serialized field {fieldName}.");
            field.SetValue(target, value);
        }
    }
}
