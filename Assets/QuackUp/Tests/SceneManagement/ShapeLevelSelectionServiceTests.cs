using System;
using System.Collections.Generic;
using System.Reflection;
using FitMe.GameData;
using FitMe.Grid;
using FitMe.Scene;
using FitMe.Shared;
using NUnit.Framework;
using UnityEngine;

namespace QuackUp.SceneManagement.Tests
{
    public sealed class ShapeLevelSelectionServiceTests
    {
        private readonly List<ScriptableObject> _createdAssets = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _createdAssets)
            {
                if (asset) UnityEngine.Object.DestroyImmediate(asset);
            }

            _createdAssets.Clear();
        }

        [Test]
        public void TryDispatchSelection_UsesCatalogIdAndSelectedLevelPayload()
        {
            var first = CreateLevel();
            var second = CreateLevel();
            var catalog = CreateCatalog(("shape-intro", first), ("shape-next", second));
            var service = new ShapeLevelSelectionService(catalog);
            LevelSessionRequest dispatchedRequest = default;
            var dispatchCount = 0;

            var dispatched = service.TryDispatchSelection("shape-next", 2, request =>
            {
                dispatchedRequest = request;
                dispatchCount++;
            });

            Assert.That(dispatched, Is.True);
            Assert.That(dispatchCount, Is.EqualTo(1));
            Assert.That(dispatchedRequest.GameMode, Is.EqualTo(GameMode.LevelShape));
            Assert.That(dispatchedRequest.ShapeLevelId, Is.EqualTo("shape-next"));
            Assert.That(dispatchedRequest.ShapeLevel, Is.SameAs(second));
            Assert.That(dispatchedRequest.GridPreset, Is.SameAs(second.Board));
        }

        [Test]
        public void TryDispatchSelection_UsesPersistedUnlockProgress()
        {
            var first = CreateLevel();
            var second = CreateLevel();
            var service = new ShapeLevelSelectionService(
                CreateCatalog(("shape-intro", first), ("shape-next", second)));
            var progress = new PlayerRecordSaveData();
            var dispatchCount = 0;

            Assert.That(service.TryDispatchSelection("shape-next", progress, _ => dispatchCount++), Is.False);
            Assert.That(dispatchCount, Is.Zero);

            progress.RecordShapeLevelPass(1, "shape-intro", 100);
            Assert.That(service.TryDispatchSelection("shape-next", progress, _ => dispatchCount++), Is.True);
            Assert.That(dispatchCount, Is.EqualTo(1));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("unknown-level")]
        public void TryDispatchSelection_RejectsInvalidIdWithoutDispatch(string levelId)
        {
            var catalog = CreateCatalog(("shape-intro", CreateLevel()));
            var service = new ShapeLevelSelectionService(catalog);
            var dispatchCount = 0;

            var dispatched = service.TryDispatchSelection(levelId, 1, _ => dispatchCount++);

            Assert.That(dispatched, Is.False);
            Assert.That(dispatchCount, Is.Zero);
        }

        [Test]
        public void TryDispatchSelection_RejectsLockedLevelWithoutGameplayDispatch()
        {
            var catalog = CreateCatalog(
                ("shape-intro", CreateLevel()),
                ("shape-locked", CreateLevel()));
            var service = new ShapeLevelSelectionService(catalog);
            var dispatchCount = 0;

            var dispatched = service.TryDispatchSelection("shape-locked", 1, _ => dispatchCount++);

            Assert.That(dispatched, Is.False);
            Assert.That(dispatchCount, Is.Zero);
        }

        [Test]
        public void TryDispatchSelection_RejectsInvalidDefinitionWithoutGameplayDispatch()
        {
            var invalidLevel = Keep(ScriptableObject.CreateInstance<ShapeLevelDefinition>());
            var service = new ShapeLevelSelectionService(CreateCatalog(("shape-invalid", invalidLevel)));
            var dispatchCount = 0;

            var dispatched = service.TryDispatchSelection("shape-invalid", 1, _ => dispatchCount++);

            Assert.That(dispatched, Is.False);
            Assert.That(dispatchCount, Is.Zero);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void TryDispatchSelection_RejectsInvalidUnlockProgress(int highestUnlockedOrder)
        {
            var service = new ShapeLevelSelectionService(CreateCatalog(("shape-intro", CreateLevel())));
            var dispatchCount = 0;

            var dispatched = service.TryDispatchSelection("shape-intro", highestUnlockedOrder, _ => dispatchCount++);

            Assert.That(dispatched, Is.False);
            Assert.That(dispatchCount, Is.Zero);
        }

        [Test]
        public void TryDispatchSelection_RejectsMissingCatalogAndDispatchCallback()
        {
            var missingCatalogService = new ShapeLevelSelectionService(null);
            var validService = new ShapeLevelSelectionService(CreateCatalog(("shape-intro", CreateLevel())));
            var dispatchCount = 0;

            Assert.That(missingCatalogService.TryDispatchSelection("shape-intro", 1, _ => dispatchCount++), Is.False);
            Assert.That(validService.TryDispatchSelection("shape-intro", 1, null), Is.False);
            Assert.That(dispatchCount, Is.Zero);
        }

        private ShapeLevelDefinition CreateLevel()
        {
            var board = Keep(ScriptableObject.CreateInstance<GridPreset>());
            board.PresetGridType = GridType.Rectangle;
            board.GridSize = Vector2Int.one;

            var preset = Keep(ScriptableObject.CreateInstance<BlockPreset>());
            preset.BlockSchema.schema = new[,] { { 1 } };

            var definition = Keep(ScriptableObject.CreateInstance<ShapeLevelDefinition>());
            SetPrivateField(definition, "_board", board);
            SetPrivateField(definition, "_playerBlocks", new List<ShapePlayerBlockEntry>
            {
                new(preset, 1)
            });
            return definition;
        }

        private ShapeLevelCatalog CreateCatalog(params (string id, ShapeLevelDefinition level)[] entries)
        {
            var catalog = Keep(ScriptableObject.CreateInstance<ShapeLevelCatalog>());
            var levelsField = typeof(ShapeLevelCatalog).GetField("_levels", BindingFlags.Instance | BindingFlags.NonPublic);
            var levels = Activator.CreateInstance(levelsField.FieldType);
            var add = levelsField.FieldType.GetMethod("Add", new[] { typeof(string), typeof(ShapeLevelDefinition) });
            foreach (var (id, level) in entries) add.Invoke(levels, new object[] { id, level });
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
