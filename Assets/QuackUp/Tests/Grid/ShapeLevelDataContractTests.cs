using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using FitMe.GameData;
using FitMe.Grid;
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
        public void Validate_AcceptsExactCapacityAndObstacleOccupancy()
        {
            var level = CreateValidLevel("shape-001", 1);

            var result = ShapeLevelDataValidator.Validate(level);

            Assert.That(result.IsValid, Is.True, string.Join("\n", result.Errors));
        }

        [Test]
        public void Validate_RejectsObstacleOnVoidCellAndOverlappingObstacles()
        {
            var level = CreateValidLevel("shape-001", 1);
            level.Board.customGrid[0, 0] = 0;

            var voidCellResult = ShapeLevelDataValidator.Validate(level);
            Assert.That(voidCellResult.Errors, Does.Contain(ShapeLevelValidationError.ObstacleOnVoidCell));

            level.Board.customGrid[0, 0] = 1;
            SetPrivateField(level, "_obstacles", new List<ShapeObstaclePlacement>
            {
                new(new ShapeFootprint(new ShapeCell(0, 0)), 0, 0, BlockColor.Red),
                new(new ShapeFootprint(new ShapeCell(0, 0)), 0, 0, BlockColor.Blue)
            });

            var overlapResult = ShapeLevelDataValidator.Validate(level);
            Assert.That(overlapResult.Errors, Does.Contain(ShapeLevelValidationError.ObstacleOverlap));
        }

        [Test]
        public void Validate_RejectsBlockCapacityMismatch()
        {
            var level = CreateValidLevel("shape-001", 1);
            SetPrivateField(level, "_playerBlocks", new List<ShapePlayerBlockDefinition>
            {
                new(new ShapeFootprint(new ShapeCell(0, 0), new ShapeCell(0, 1)), BlockColor.Green)
            });

            var result = ShapeLevelDataValidator.Validate(level);

            Assert.That(result.Errors, Does.Contain(ShapeLevelValidationError.BlockCapacityMismatch));
        }

        [Test]
        public void Catalog_LookupIsStableByIdAndOrderAndPreservesAuthoredBlockOrder()
        {
            var first = CreateValidLevel("shape-001", 1);
            var second = CreateValidLevel("shape-002", 2);
            var catalog = CreateCatalog(second, first);

            Assert.That(catalog.TryGetById("shape-001", out var byId), Is.True);
            Assert.That(byId, Is.SameAs(first));
            Assert.That(catalog.TryGetByOrder(2, out var byOrder), Is.True);
            Assert.That(byOrder, Is.SameAs(second));
            Assert.That(first.PlayerBlocks[0].Color, Is.EqualTo(BlockColor.Green));
            Assert.That(first.PlayerBlocks[1].Color, Is.EqualTo(BlockColor.Purple));
        }

        [Test]
        public void Catalog_ValidationRejectsDuplicateIdsAndOrders()
        {
            var first = CreateValidLevel("shape-001", 1);
            var duplicate = CreateValidLevel("shape-001", 1);
            var catalog = CreateCatalog(first, duplicate);

            var result = catalog.Validate();

            Assert.That(result.Errors, Does.Contain(ShapeLevelValidationError.DuplicateLevelId));
            Assert.That(result.Errors, Does.Contain(ShapeLevelValidationError.DuplicateLevelOrder));
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

        private ShapeLevelDefinition CreateValidLevel(string id, int order)
        {
            var board = Keep(ScriptableObject.CreateInstance<GridPreset>());
            board.PresetGridType = GridType.Custom;
            board.GridSize = new Vector2Int(3, 2);
            board.customGrid = new[,] { { 1, 1, 1 }, { 1, 1, 1 } };

            var definition = Keep(ScriptableObject.CreateInstance<ShapeLevelDefinition>());
            SetPrivateField(definition, "_levelId", id);
            SetPrivateField(definition, "_order", order);
            SetPrivateField(definition, "_board", board);
            SetPrivateField(definition, "_obstacles", new List<ShapeObstaclePlacement>
            {
                new(new ShapeFootprint(new ShapeCell(0, 0), new ShapeCell(1, 0)), 0, 0, BlockColor.Red)
            });
            SetPrivateField(definition, "_playerBlocks", new List<ShapePlayerBlockDefinition>
            {
                new(new ShapeFootprint(new ShapeCell(0, 0), new ShapeCell(0, 1)), BlockColor.Green),
                new(new ShapeFootprint(new ShapeCell(0, 0), new ShapeCell(0, 1)), BlockColor.Purple)
            });
            return definition;
        }

        private ShapeLevelCatalog CreateCatalog(params ShapeLevelDefinition[] levels)
        {
            var catalog = Keep(ScriptableObject.CreateInstance<ShapeLevelCatalog>());
            SetPrivateField(catalog, "_levels", new List<ShapeLevelDefinition>(levels));
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
