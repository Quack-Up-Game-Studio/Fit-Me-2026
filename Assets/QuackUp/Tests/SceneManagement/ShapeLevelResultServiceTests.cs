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
    public sealed class ShapeLevelResultServiceTests
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
        public void Retry_ReturnsSameLevelRequestWithoutChangingProgress()
        {
            var first = CreateLevel();
            var second = CreateLevel();
            var catalog = CreateCatalog(("shape-one", first), ("shape-two", second));
            var active = LevelSessionRequest.ForShapeLevel("shape-one", first);
            var progress = new PlayerRecordSaveData();
            ShapeLevelResultOutcome outcome = default;
            var service = new ShapeLevelResultService(catalog);

            var resolved = service.TryResolve(
                ShapeLevelResultAction.Retry, active, wasPassed: false, progress, out outcome);

            Assert.That(resolved, Is.True);
            Assert.That(outcome.Action, Is.EqualTo(ShapeLevelResultAction.Retry));
            Assert.That(outcome.SessionRequest.ShapeLevelId, Is.EqualTo("shape-one"));
            Assert.That(outcome.SessionRequest.ShapeLevel, Is.SameAs(first));
            Assert.That(progress.HighestUnlockedShapeOrder, Is.EqualTo(1));
            Assert.That(progress.ShapeLevelHighScores, Is.Empty);
        }

        [Test]
        public void NextLevel_RejectsFailedRunWithoutReturningSessionRequest()
        {
            var first = CreateLevel();
            var second = CreateLevel();
            var service = new ShapeLevelResultService(CreateCatalog(("shape-one", first), ("shape-two", second)));
            var active = LevelSessionRequest.ForShapeLevel("shape-one", first);
            var progress = new PlayerRecordSaveData();

            var resolved = service.TryResolve(
                ShapeLevelResultAction.NextLevel, active, wasPassed: false, progress, out var outcome);

            Assert.That(resolved, Is.False);
            Assert.That(outcome.Action, Is.EqualTo(ShapeLevelResultAction.None));
            Assert.That(progress.HighestUnlockedShapeOrder, Is.EqualTo(1));
        }

        [Test]
        public void NextLevel_DispatchesImmediatelyFollowingUnlockedCatalogEntryAfterPass()
        {
            var first = CreateLevel();
            var second = CreateLevel();
            var service = new ShapeLevelResultService(CreateCatalog(("shape-one", first), ("shape-two", second)));
            var active = LevelSessionRequest.ForShapeLevel("shape-one", first);
            var progress = new PlayerRecordSaveData();
            progress.RecordShapeLevelPass(1, "shape-one", 100);

            var resolved = service.TryResolve(
                ShapeLevelResultAction.NextLevel, active, wasPassed: true, progress, out var outcome);

            Assert.That(resolved, Is.True);
            Assert.That(outcome.SessionRequest.ShapeLevelId, Is.EqualTo("shape-two"));
            Assert.That(outcome.SessionRequest.ShapeLevel, Is.SameAs(second));
        }

        [Test]
        public void ReturnToLevelSelect_ProducesSelectionNavigationOutcome()
        {
            var first = CreateLevel();
            var service = new ShapeLevelResultService(CreateCatalog(("shape-one", first)));
            var active = LevelSessionRequest.ForShapeLevel("shape-one", first);

            var resolved = service.TryResolve(
                ShapeLevelResultAction.ReturnToLevelSelect, active, wasPassed: false,
                new PlayerRecordSaveData(), out var outcome);

            Assert.That(resolved, Is.True);
            Assert.That(outcome.Action, Is.EqualTo(ShapeLevelResultAction.ReturnToLevelSelect));
            Assert.That(outcome.SessionRequest.ShapeLevel, Is.Null);
        }

        [Test]
        public void NextLevel_RejectsWhenFollowingLevelIsStillLocked()
        {
            var first = CreateLevel();
            var second = CreateLevel();
            var service = new ShapeLevelResultService(CreateCatalog(("shape-one", first), ("shape-two", second)));
            var active = LevelSessionRequest.ForShapeLevel("shape-one", first);

            var resolved = service.TryResolve(
                ShapeLevelResultAction.NextLevel, active, wasPassed: true,
                new PlayerRecordSaveData(), out var outcome);

            Assert.That(resolved, Is.False);
            Assert.That(outcome.Action, Is.EqualTo(ShapeLevelResultAction.None));
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
            SetPrivateField(definition, "_playerBlocks", new List<ShapePlayerBlockEntry> { new(preset, 1) });
            return definition;
        }

        private ShapeLevelCatalog CreateCatalog(params (string id, ShapeLevelDefinition level)[] entries)
        {
            var catalog = Keep(ScriptableObject.CreateInstance<ShapeLevelCatalog>());
            var field = typeof(ShapeLevelCatalog).GetField("_levels", BindingFlags.Instance | BindingFlags.NonPublic);
            var levels = Activator.CreateInstance(field.FieldType);
            var add = field.FieldType.GetMethod("Add", new[] { typeof(string), typeof(ShapeLevelDefinition) });
            foreach (var (id, level) in entries) add.Invoke(levels, new object[] { id, level });
            field.SetValue(catalog, levels);
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
