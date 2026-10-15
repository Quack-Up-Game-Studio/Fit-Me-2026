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
        public void TryResolve_RejectsInvalidCatalogEvenWhenActiveLevelIsValid()
        {
            var first = CreateLevel();
            var second = CreateLevel();
            var catalog = CreateCatalog(("shape-one", first), (" ", second));
            var active = LevelSessionRequest.ForShapeLevel("shape-one", first);
            var service = new ShapeLevelResultService(catalog);

            var resolved = service.TryResolve(
                ShapeLevelResultAction.Retry, active, wasPassed: false,
                new PlayerRecordSaveData(), out var outcome);

            Assert.That(resolved, Is.False);
            Assert.That(outcome.Action, Is.EqualTo(ShapeLevelResultAction.None));
        }

        [Test]
        public void NextLevel_RejectsFailedRunWhenFollowingLevelIsStillLocked()
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
        public void NextLevel_RejectsFailedRunEvenWhenFollowingLevelWasAlreadyUnlocked()
        {
            var first = CreateLevel();
            var second = CreateLevel();
            var service = new ShapeLevelResultService(CreateCatalog(("shape-one", first), ("shape-two", second)));
            var active = LevelSessionRequest.ForShapeLevel("shape-one", first);
            var progress = new PlayerRecordSaveData();
            progress.RecordShapeLevelPass(1, "shape-one", 100);

            var resolved = service.TryResolve(
                ShapeLevelResultAction.NextLevel, active, wasPassed: false, progress, out var outcome);

            Assert.That(resolved, Is.False);
            Assert.That(outcome.Action, Is.EqualTo(ShapeLevelResultAction.None));
        }

        [Test]
        public void RecordPass_UsesLevelIdOrderWhenCatalogEntriesShareDefinition()
        {
            var first = CreateLevel();
            var shared = CreateLevel();
            var catalog = CreateCatalog(
                ("level-1", first),
                ("level-2", shared),
                ("level-3", shared),
                ("level-4", shared));
            var progress = new PlayerRecordSaveData();
            progress.RecordShapeLevelPass(1, "level-1", 10);
            progress.RecordShapeLevelPass(2, "level-2", 20);
            var service = new ShapeLevelProgressService(catalog);

            var recorded = service.TryRecordPass(
                "level-3", shared, progress, 30, out var progressChanged);

            Assert.That(recorded, Is.True);
            Assert.That(progressChanged, Is.True);
            Assert.That(progress.HighestUnlockedShapeOrder, Is.EqualTo(4));
            Assert.That(progress.ShapeLevelHighScores["level-3"], Is.EqualTo(30));

            var active = LevelSessionRequest.ForShapeLevel("level-3", shared);
            var resultService = new ShapeLevelResultService(catalog);
            var canAdvance = resultService.TryResolve(
                ShapeLevelResultAction.NextLevel, active, wasPassed: true, progress, out var outcome);

            Assert.That(canAdvance, Is.True);
            Assert.That(outcome.SessionRequest.ShapeLevelId, Is.EqualTo("level-4"));
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
            var field = typeof(ShapeLevelCatalog).GetField("_entries", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Expected serialized catalog entry list.");
            var catalogEntries = new List<ShapeLevelCatalogEntry>();
            foreach (var (id, level) in entries)
                catalogEntries.Add(new ShapeLevelCatalogEntry(id, level));
            field.SetValue(catalog, catalogEntries);
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
