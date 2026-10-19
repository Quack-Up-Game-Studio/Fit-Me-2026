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
        private const string FirstWorldKey = "world-one";
        private const string SecondWorldKey = "world-two";
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
        public void Retry_ReturnsSameWorldLevelRequestWithoutChangingProgress()
        {
            var first = CreateLevel();
            var second = CreateLevel();
            var worlds = CreateWorldCatalog(
                (FirstWorldKey, CreateCatalog(("shape-one", first), ("shape-two", second))));
            var active = LevelSessionRequest.ForShapeLevel(FirstWorldKey, "shape-one", first);
            var progress = CreateProgress((FirstWorldKey, true));
            var service = new ShapeLevelResultService(new ShapeLevelWorldCatalogService(worlds));

            var resolved = service.TryResolve(
                ShapeLevelResultAction.Retry, active, wasPassed: false, progress, out var outcome);

            Assert.That(resolved, Is.True);
            Assert.That(outcome.Action, Is.EqualTo(ShapeLevelResultAction.Retry));
            Assert.That(outcome.SessionRequest.ShapeWorldKey, Is.EqualTo(FirstWorldKey));
            Assert.That(outcome.SessionRequest.ShapeLevelId, Is.EqualTo("shape-one"));
            Assert.That(outcome.SessionRequest.ShapeLevel, Is.SameAs(first));
            Assert.That(progress.ShapeWorldProgress[FirstWorldKey].IsUnlocked, Is.True);
            Assert.That(progress.ShapeWorldProgress[FirstWorldKey].CompletedLevelIds, Is.Empty);
        }

        [Test]
        public void TryResolve_RejectsInvalidWorldCatalogEvenWhenActiveLevelIsValid()
        {
            var first = CreateLevel();
            var invalidCatalog = CreateCatalog(("shape-one", first));
            var worlds = CreateWorldCatalog((" ", invalidCatalog));
            var active = LevelSessionRequest.ForShapeLevel(" ", "shape-one", first);
            var service = new ShapeLevelResultService(new ShapeLevelWorldCatalogService(worlds));

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
            var worlds = CreateWorldCatalog((FirstWorldKey, CreateCatalog(("shape-one", first), ("shape-two", second))));
            var active = LevelSessionRequest.ForShapeLevel(FirstWorldKey, "shape-one", first);
            var progress = CreateProgress((FirstWorldKey, true));
            var service = new ShapeLevelResultService(new ShapeLevelWorldCatalogService(worlds));

            var resolved = service.TryResolve(
                ShapeLevelResultAction.NextLevel, active, wasPassed: false, progress, out var outcome);

            Assert.That(resolved, Is.False);
            Assert.That(outcome.Action, Is.EqualTo(ShapeLevelResultAction.None));
            Assert.That(progress.ShapeWorldProgress[FirstWorldKey].CompletedLevelIds, Is.Empty);
        }

        [Test]
        public void NextLevel_RejectsFailedRunEvenWhenFollowingLevelWasAlreadyUnlocked()
        {
            var first = CreateLevel();
            var second = CreateLevel();
            var worlds = CreateWorldCatalog((FirstWorldKey, CreateCatalog(("shape-one", first), ("shape-two", second))));
            var active = LevelSessionRequest.ForShapeLevel(FirstWorldKey, "shape-one", first);
            var progress = CreateProgress((FirstWorldKey, true));
            progress.ShapeWorldProgress[FirstWorldKey].RecordLevelPass("shape-one");
            var service = new ShapeLevelResultService(new ShapeLevelWorldCatalogService(worlds));

            var resolved = service.TryResolve(
                ShapeLevelResultAction.NextLevel, active, wasPassed: false, progress, out var outcome);

            Assert.That(resolved, Is.False);
            Assert.That(outcome.Action, Is.EqualTo(ShapeLevelResultAction.None));
            Assert.That(progress.ShapeWorldProgress[FirstWorldKey].CompletedLevelIds, Does.Contain("shape-one"));
        }

        [Test]
        public void RecordPass_UsesLevelIdOrderWhenCatalogEntriesShareDefinitionAndRetainsWorldProgress()
        {
            var first = CreateLevel();
            var shared = CreateLevel();
            var firstCatalog = CreateCatalog(
                ("level-1", first), ("level-2", shared), ("level-3", shared), ("level-4", shared));
            var secondCatalog = CreateCatalog(("other-level", CreateLevel()));
            var worlds = CreateWorldCatalog((FirstWorldKey, firstCatalog), (SecondWorldKey, secondCatalog));
            var progress = CreateProgress((FirstWorldKey, true), (SecondWorldKey, true));
            progress.ShapeWorldProgress[FirstWorldKey].RecordLevelPass("level-1");
            progress.ShapeWorldProgress[FirstWorldKey].RecordLevelPass("level-2");
            progress.ShapeWorldProgress[SecondWorldKey].RecordLevelPass("other-level");
            var service = new ShapeLevelProgressService(new ShapeLevelWorldCatalogService(worlds));

            var recorded = service.TryRecordPass("level-3", FirstWorldKey, shared, progress, out var progressChanged);

            Assert.That(recorded, Is.True);
            Assert.That(progressChanged, Is.True);
            Assert.That(progress.ShapeWorldProgress[FirstWorldKey].CompletedLevelIds, Does.Contain("level-3"));
            Assert.That(progress.ShapeWorldProgress[SecondWorldKey].CompletedLevelIds, Does.Contain("other-level"));

            var active = LevelSessionRequest.ForShapeLevel(FirstWorldKey, "level-3", shared);
            var resultService = new ShapeLevelResultService(new ShapeLevelWorldCatalogService(worlds));
            var canAdvance = resultService.TryResolve(
                ShapeLevelResultAction.NextLevel, active, wasPassed: true, progress, out var outcome);

            Assert.That(canAdvance, Is.True);
            Assert.That(outcome.SessionRequest.ShapeWorldKey, Is.EqualTo(FirstWorldKey));
            Assert.That(outcome.SessionRequest.ShapeLevelId, Is.EqualTo("level-4"));
        }

        [Test]
        public void NextLevel_DispatchesImmediatelyFollowingUnlockedCatalogEntryAfterPass()
        {
            var first = CreateLevel();
            var second = CreateLevel();
            var worlds = CreateWorldCatalog((FirstWorldKey, CreateCatalog(("shape-one", first), ("shape-two", second))));
            var active = LevelSessionRequest.ForShapeLevel(FirstWorldKey, "shape-one", first);
            var progress = CreateProgress((FirstWorldKey, true));
            progress.ShapeWorldProgress[FirstWorldKey].RecordLevelPass("shape-one");
            var service = new ShapeLevelResultService(new ShapeLevelWorldCatalogService(worlds));

            var resolved = service.TryResolve(
                ShapeLevelResultAction.NextLevel, active, wasPassed: true, progress, out var outcome);

            Assert.That(resolved, Is.True);
            Assert.That(outcome.SessionRequest.ShapeWorldKey, Is.EqualTo(FirstWorldKey));
            Assert.That(outcome.SessionRequest.ShapeLevelId, Is.EqualTo("shape-two"));
            Assert.That(outcome.SessionRequest.ShapeLevel, Is.SameAs(second));
        }

        [Test]
        public void NextProgressionAction_AfterFinalLevelPassResolvesNextWorldFirstLevel()
        {
            var finalLevel = CreateLevel();
            var nextWorldFirstLevel = CreateLevel();
            var worlds = CreateWorldCatalog(
                (FirstWorldKey, CreateCatalog(("shape-final", finalLevel))),
                (SecondWorldKey, CreateCatalog(("next-world-first", nextWorldFirstLevel))));
            var active = LevelSessionRequest.ForShapeLevel(FirstWorldKey, "shape-final", finalLevel);
            var progress = CreateProgress((FirstWorldKey, true), (SecondWorldKey, true));
            progress.ShapeWorldProgress[FirstWorldKey].RecordLevelPass("shape-final");
            var service = new ShapeLevelResultService(new ShapeLevelWorldCatalogService(worlds));

            var resolved = service.TryResolve(
                ShapeLevelResultAction.NextWorld, active, wasPassed: true, progress, out var outcome);
            var nextLevelResolved = service.TryResolve(
                ShapeLevelResultAction.NextLevel, active, wasPassed: true, progress, out _);
            var availability = service.GetNextWorldAvailability(active, wasPassed: true, progress);

            Assert.That(availability.IsVisible, Is.True);
            Assert.That(availability.IsEnabled, Is.True);
            Assert.That(nextLevelResolved, Is.False,
                "The Next Level result action must not cross a world boundary; that route is exclusively Next World.");
            Assert.That(resolved, Is.True,
                "A passed final level should route the result progression action to the next world's first level.");
            Assert.That(outcome.SessionRequest.ShapeWorldKey, Is.EqualTo(SecondWorldKey));
            Assert.That(outcome.SessionRequest.ShapeLevelId, Is.EqualTo("next-world-first"));
            Assert.That(outcome.SessionRequest.ShapeLevel, Is.SameAs(nextWorldFirstLevel));
        }

        [Test]
        public void NextWorldAvailability_IsHiddenForFailedRun()
        {
            var finalLevel = CreateLevel();
            var nextWorldLevel = CreateLevel();
            var worlds = CreateWorldCatalog(
                (FirstWorldKey, CreateCatalog(("shape-final", finalLevel))),
                (SecondWorldKey, CreateCatalog(("next-world-first", nextWorldLevel))));
            var active = LevelSessionRequest.ForShapeLevel(FirstWorldKey, "shape-final", finalLevel);
            var progress = CreateProgress((FirstWorldKey, true), (SecondWorldKey, true));
            progress.ShapeWorldProgress[FirstWorldKey].RecordLevelPass("shape-final");
            var service = new ShapeLevelResultService(new ShapeLevelWorldCatalogService(worlds));

            var availability = service.GetNextWorldAvailability(active, wasPassed: false, progress);

            Assert.That(availability.IsVisible, Is.False);
            Assert.That(availability.IsEnabled, Is.False);
        }

        [Test]
        public void NextWorldAvailability_IsHiddenWhenNoFollowingWorldExists()
        {
            var finalLevel = CreateLevel();
            var worlds = CreateWorldCatalog(
                (FirstWorldKey, CreateCatalog(("shape-final", finalLevel))));
            var active = LevelSessionRequest.ForShapeLevel(FirstWorldKey, "shape-final", finalLevel);
            var progress = CreateProgress((FirstWorldKey, true));
            progress.ShapeWorldProgress[FirstWorldKey].RecordLevelPass("shape-final");
            var service = new ShapeLevelResultService(new ShapeLevelWorldCatalogService(worlds));

            var availability = service.GetNextWorldAvailability(active, wasPassed: true, progress);

            Assert.That(availability.IsVisible, Is.False);
            Assert.That(availability.IsEnabled, Is.False);
        }

        [Test]
        public void NextWorldAvailability_IsVisibleButDisabledWhenFollowingWorldIsLocked()
        {
            var finalLevel = CreateLevel();
            var nextWorldLevel = CreateLevel();
            var worlds = CreateWorldCatalog(
                (FirstWorldKey, CreateCatalog(("shape-final", finalLevel))),
                (SecondWorldKey, CreateCatalog(("next-world-first", nextWorldLevel))));
            var active = LevelSessionRequest.ForShapeLevel(FirstWorldKey, "shape-final", finalLevel);
            var progress = CreateProgress((FirstWorldKey, true), (SecondWorldKey, false));
            progress.ShapeWorldProgress[FirstWorldKey].RecordLevelPass("shape-final");
            var service = new ShapeLevelResultService(new ShapeLevelWorldCatalogService(worlds));

            var availability = service.GetNextWorldAvailability(active, wasPassed: true, progress);
            var resolved = service.TryResolve(
                ShapeLevelResultAction.NextWorld, active, wasPassed: true, progress, out var outcome);

            Assert.That(availability.IsVisible, Is.True);
            Assert.That(availability.IsEnabled, Is.False);
            Assert.That(resolved, Is.False);
            Assert.That(outcome.Action, Is.EqualTo(ShapeLevelResultAction.None));
        }

        [Test]
        public void ReturnToLevelSelect_ProducesSelectionNavigationOutcomeWithoutLosingWorldKey()
        {
            var first = CreateLevel();
            var worlds = CreateWorldCatalog((FirstWorldKey, CreateCatalog(("shape-one", first))));
            var active = LevelSessionRequest.ForShapeLevel(FirstWorldKey, "shape-one", first);
            var service = new ShapeLevelResultService(new ShapeLevelWorldCatalogService(worlds));

            var resolved = service.TryResolve(
                ShapeLevelResultAction.ReturnToLevelSelect, active, wasPassed: false,
                CreateProgress((FirstWorldKey, true)), out var outcome);

            Assert.That(resolved, Is.True);
            Assert.That(outcome.Action, Is.EqualTo(ShapeLevelResultAction.ReturnToLevelSelect));
            Assert.That(outcome.SessionRequest.ShapeLevel, Is.Null);
            Assert.That(outcome.SessionRequest.ShapeWorldKey, Is.Null);
        }

        [Test]
        public void NextLevel_RejectsWhenFollowingLevelIsStillLocked()
        {
            var first = CreateLevel();
            var second = CreateLevel();
            var worlds = CreateWorldCatalog((FirstWorldKey, CreateCatalog(("shape-one", first), ("shape-two", second))));
            var active = LevelSessionRequest.ForShapeLevel(FirstWorldKey, "shape-one", first);
            var service = new ShapeLevelResultService(new ShapeLevelWorldCatalogService(worlds));

            var resolved = service.TryResolve(
                ShapeLevelResultAction.NextLevel, active, wasPassed: true,
                CreateProgress((FirstWorldKey, true)), out var outcome);

            Assert.That(resolved, Is.False);
            Assert.That(outcome.Action, Is.EqualTo(ShapeLevelResultAction.None));
        }

        [Test]
        public void CatalogService_ResolvesInitialAndWrappedNextLevelsByStableId()
        {
            var sharedDefinition = CreateLevel();
            var worlds = CreateWorldCatalog((FirstWorldKey, CreateCatalog(
                ("level-first", sharedDefinition), ("level-second", sharedDefinition))));
            var catalogService = new ShapeLevelWorldCatalogService(worlds);

            Assert.That(catalogService.TryGetFirstLevel(FirstWorldKey, out var firstId, out var firstLevel), Is.True);
            Assert.That(firstId, Is.EqualTo("level-first"));
            Assert.That(firstLevel, Is.SameAs(sharedDefinition));

            var resolved = catalogService.TryGetNextLevel(
                FirstWorldKey, "level-second", sharedDefinition, wrapAtEnd: true,
                out var nextId, out var nextLevel);

            Assert.That(resolved, Is.True);
            Assert.That(nextId, Is.EqualTo("level-first"));
            Assert.That(nextLevel, Is.SameAs(sharedDefinition));
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

        private ShapeLevelWorldCatalog CreateWorldCatalog(params (string key, ShapeLevelCatalog catalog)[] entries)
        {
            var worldCatalog = Keep(ScriptableObject.CreateInstance<ShapeLevelWorldCatalog>());
            var field = typeof(ShapeLevelWorldCatalog).GetField("_entries", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Expected serialized world catalog entry list.");
            var worldEntries = new List<ShapeLevelWorldEntry>();
            foreach (var (key, catalog) in entries)
                worldEntries.Add(new ShapeLevelWorldEntry(key, catalog));
            field.SetValue(worldCatalog, worldEntries);
            return worldCatalog;
        }

        private static PlayerRecordSaveData CreateProgress(params (string key, bool unlocked)[] worlds)
        {
            var progress = new PlayerRecordSaveData();
            foreach (var (key, unlocked) in worlds)
                progress.ShapeWorldProgress[key] = new ShapeWorldProgressData(key, unlocked);
            return progress;
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