using System;
using System.Collections.Generic;
using System.Reflection;
using FitMe.GameData;
using FitMe.Grid;
using FitMe.Panel;
using FitMe.Scene;
using FitMe.Shared;
using NUnit.Framework;
using UnityEngine;
using VContainer;

namespace QuackUp.SceneManagement.Tests
{
    public sealed class ShapeLevelSelectionServiceTests
    {
        private readonly List<ScriptableObject> _createdAssets = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _createdAssets)
                if (asset) UnityEngine.Object.DestroyImmediate(asset);
            _createdAssets.Clear();
        }

        [Test]
        public void SceneType_ContainsDedicatedShapeLevelSelectScene()
        {
            Assert.That(Enum.TryParse<SceneType>("ShapeLevelSelect", out _), Is.True);
        }

        [TestCase(GameMode.Classic, SceneType.ModeSelect)]
        [TestCase(GameMode.LevelShape, SceneType.ShapeLevelSelect)]
        public void UniversalSettingsQuitDestinationMatchesActiveGameMode(GameMode gameMode, SceneType expectedScene)
        {
            var destination = UniversalSettingsPanelViewModel.ResolveQuitDestination(gameMode);
            Assert.That(destination, Is.EqualTo(expectedScene));
        }

        [Test]
        public void ShapeLevelSelectViewModel_ProvidesSelectedWorldDisplayName()
        {
            var worldCatalog = CreateCatalog(("level-a", CreateLevel()));
            worldCatalog.name = "Forest World";
            var catalogService = new ShapeLevelWorldCatalogService(CreateWorldCatalog(("forest", worldCatalog)));
            using var viewModel = new ShapeLevelSelectViewModel(null, catalogService);

            viewModel.InitializeItems("forest", CreateProgress(("forest", true)).ShapeWorldProgress);

            Assert.That(viewModel.WorldDisplayName.Value, Is.EqualTo("Forest World"));

            viewModel.InitializeItems("missing-world", CreateProgress(("forest", true)).ShapeWorldProgress);
            Assert.That(viewModel.WorldDisplayName.Value, Is.Empty, "A missing selection must clear a previous world's title.");
        }

        [Test]
        public void ProjectCatalogInstallerExposesCatalogToChildSceneScopes()
        {
            var worlds = CreateWorldCatalog(("world-a", CreateCatalog(("shape-intro", CreateLevel()))));
            var installer = new ShapeLevelWorldCatalogInstaller();
            SetPrivateField(installer, "worldCatalog", worlds);
            var projectBuilder = new ContainerBuilder();
            installer.Install(projectBuilder);

            using var projectScope = projectBuilder.Build();
            using var sceneScope = projectScope.CreateScope(builder =>
                builder.Register<ShapeLevelSelectionService>(Lifetime.Singleton).AsSelf());
            var selectionService = sceneScope.Resolve<ShapeLevelSelectionService>();
            var dispatchCount = 0;

            var dispatched = selectionService.TryDispatchSelection(
                "world-a", "shape-intro", CreateProgress(("world-a", true)), _ => dispatchCount++);

            Assert.That(dispatched, Is.True);
            Assert.That(dispatchCount, Is.EqualTo(1));
        }

        [Test]
        public void TryDispatchSelection_UsesWorldCatalogIdAndSelectedLevelPayloadAfterPreviousCompletion()
        {
            var first = CreateLevel();
            var second = CreateLevel();
            var worlds = CreateWorldCatalog(
                ("world-a", CreateCatalog(("shape-intro", first), ("shape-next", second))));
            var service = new ShapeLevelSelectionService(new ShapeLevelWorldCatalogService(worlds));
            var progress = CreateProgress(("world-a", true));
            progress.ShapeWorldProgress["world-a"].RecordLevelPass("shape-intro");
            LevelSessionRequest dispatchedRequest = default;
            var dispatchCount = 0;

            var dispatched = service.TryDispatchSelection("world-a", "shape-next", progress, request =>
            {
                dispatchedRequest = request;
                dispatchCount++;
            });

            Assert.That(dispatched, Is.True);
            Assert.That(dispatchCount, Is.EqualTo(1));
            Assert.That(dispatchedRequest.GameMode, Is.EqualTo(GameMode.LevelShape));
            Assert.That(dispatchedRequest.ShapeWorldKey, Is.EqualTo("world-a"));
            Assert.That(dispatchedRequest.ShapeLevelId, Is.EqualTo("shape-next"));
            Assert.That(dispatchedRequest.ShapeLevel, Is.SameAs(second));
            Assert.That(dispatchedRequest.GridPreset, Is.SameAs(second.Board));
        }

        [Test]
        public void TryDispatchSelection_RejectsNextLevelUntilPreviousLevelIsCompleted()
        {
            var worlds = CreateWorldCatalog(("world-a", CreateCatalog(
                ("shape-intro", CreateLevel()), ("shape-next", CreateLevel()))));
            var service = new ShapeLevelSelectionService(new ShapeLevelWorldCatalogService(worlds));
            var progress = CreateProgress(("world-a", true));
            var dispatchCount = 0;

            Assert.That(service.TryDispatchSelection("world-a", "shape-next", progress, _ => dispatchCount++), Is.False);
            Assert.That(dispatchCount, Is.Zero);
        }

        [Test]
        public void TryDispatchSelection_RequiresSelectedWorldToBeUnlocked()
        {
            var first = CreateLevel();
            var worlds = CreateWorldCatalog(("world-a", CreateCatalog(("shape-intro", first))));
            var service = new ShapeLevelSelectionService(new ShapeLevelWorldCatalogService(worlds));
            var progress = CreateProgress(("world-a", false));
            var dispatchCount = 0;

            Assert.That(service.TryDispatchSelection("world-a", "shape-intro", progress, _ => dispatchCount++), Is.False);
            Assert.That(dispatchCount, Is.Zero);

            progress.ShapeWorldProgress["world-a"].Unlock();
            Assert.That(service.TryDispatchSelection("world-a", "shape-intro", progress, _ => dispatchCount++), Is.True);
            Assert.That(dispatchCount, Is.EqualTo(1));
        }

        [Test]
        public void TryDispatchSelection_UsesPerWorldCatalogAndProgress()
        {
            var aLevel = CreateLevel();
            var bLevel = CreateLevel();
            var worlds = CreateWorldCatalog(
                ("world-a", CreateCatalog(("shared-id", aLevel))),
                ("world-b", CreateCatalog(("shared-id", bLevel))));
            var service = new ShapeLevelSelectionService(new ShapeLevelWorldCatalogService(worlds));
            var progress = CreateProgress(("world-a", true), ("world-b", false));
            LevelSessionRequest request = default;
            var count = 0;

            Assert.That(service.TryDispatchSelection("world-b", "shared-id", progress, _ => count++), Is.False);
            Assert.That(service.TryDispatchSelection("world-a", "shared-id", progress, value => { request = value; count++; }), Is.True);

            Assert.That(count, Is.EqualTo(1));
            Assert.That(request.ShapeWorldKey, Is.EqualTo("world-a"));
            Assert.That(request.ShapeLevel, Is.SameAs(aLevel));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("unknown-level")]
        public void TryDispatchSelection_RejectsInvalidIdWithoutDispatch(string levelId)
        {
            var worlds = CreateWorldCatalog(("world-a", CreateCatalog(("shape-intro", CreateLevel()))));
            var service = new ShapeLevelSelectionService(new ShapeLevelWorldCatalogService(worlds));
            var progress = CreateProgress(("world-a", true));
            var dispatchCount = 0;

            var dispatched = service.TryDispatchSelection("world-a", levelId, progress, _ => dispatchCount++);

            Assert.That(dispatched, Is.False);
            Assert.That(dispatchCount, Is.Zero);
        }

        [Test]
        public void TryDispatchSelection_RejectsUnknownWorldAndMissingWorldProgress()
        {
            var worlds = CreateWorldCatalog(("world-a", CreateCatalog(("shape-intro", CreateLevel()))));
            var service = new ShapeLevelSelectionService(new ShapeLevelWorldCatalogService(worlds));
            var dispatchCount = 0;
            var missingProgress = new PlayerRecordSaveData();

            Assert.That(service.TryDispatchSelection("unknown-world", "shape-intro", CreateProgress(("world-a", true)), _ => dispatchCount++), Is.False);
            Assert.That(service.TryDispatchSelection("world-a", "shape-intro", missingProgress, _ => dispatchCount++), Is.False);
            Assert.That(dispatchCount, Is.Zero);
        }

        [Test]
        public void TryDispatchSelection_RejectsInvalidDefinitionWithoutGameplayDispatch()
        {
            var invalidLevel = Keep(ScriptableObject.CreateInstance<ShapeLevelDefinition>());
            var service = new ShapeLevelSelectionService(new ShapeLevelWorldCatalogService(
                CreateWorldCatalog(("world-a", CreateCatalog(("shape-invalid", invalidLevel))))));
            var dispatchCount = 0;

            var dispatched = service.TryDispatchSelection("world-a", "shape-invalid", CreateProgress(("world-a", true)), _ => dispatchCount++);

            Assert.That(dispatched, Is.False);
            Assert.That(dispatchCount, Is.Zero);
        }

        [Test]
        public void TryDispatchSelection_RejectsWorldCatalogWithAnotherInvalidEntry()
        {
            var worlds = CreateWorldCatalog(
                ("world-a", CreateCatalog(("shape-intro", CreateLevel()), (" ", CreateLevel()))));
            var service = new ShapeLevelSelectionService(new ShapeLevelWorldCatalogService(worlds));
            var dispatchCount = 0;

            var dispatched = service.TryDispatchSelection("world-a", "shape-intro", CreateProgress(("world-a", true)), _ => dispatchCount++);

            Assert.That(dispatched, Is.False);
            Assert.That(dispatchCount, Is.Zero);
        }

        [Test]
        public void TryDispatchSelection_RejectsMissingCatalogAndDispatchCallback()
        {
            var missingCatalogService = new ShapeLevelSelectionService(new ShapeLevelWorldCatalogService(null));
            var validService = new ShapeLevelSelectionService(
                new ShapeLevelWorldCatalogService(CreateWorldCatalog(("world-a", CreateCatalog(("shape-intro", CreateLevel()))))));
            var progress = CreateProgress(("world-a", true));
            var dispatchCount = 0;

            Assert.That(missingCatalogService.TryDispatchSelection("world-a", "shape-intro", progress, _ => dispatchCount++), Is.False);
            Assert.That(validService.TryDispatchSelection("world-a", "shape-intro", progress, null), Is.False);
            Assert.That(dispatchCount, Is.Zero);
        }

        [Test]
        public void ShapeLevelSelectViewModel_BuildsOrderedItemsAndUnlocksOnlyForUnlockedWorld()
        {
            var first = CreateLevel();
            first.name = "Shape Intro";
            var second = CreateLevel();
            second.name = "Shape Bridge";
            var catalog = CreateCatalog(("shape-intro", first), ("shape-bridge", second));
            var catalogService = new ShapeLevelWorldCatalogService(CreateWorldCatalog(("world-a", catalog), ("world-b", catalog)));
            using var viewModel = new ShapeLevelSelectViewModel(null, catalogService);
            var progress = CreateProgress(("world-a", true), ("world-b", false));

            viewModel.InitializeItems("world-a", progress.ShapeWorldProgress);

            Assert.That(viewModel.Items, Has.Count.EqualTo(2));
            Assert.That(viewModel.Items[0].LevelId, Is.EqualTo("shape-intro"));
            Assert.That(viewModel.Items[0].DisplayName, Is.EqualTo("Shape Intro"));
            Assert.That(viewModel.Items[0].Definition, Is.SameAs(first));
            Assert.That(viewModel.Items[0].Order, Is.EqualTo(1));
            Assert.That(viewModel.Items[0].IsUnlocked, Is.True);
            Assert.That(viewModel.Items[0].IsSelectable, Is.True);
            Assert.That(viewModel.Items[1].LevelId, Is.EqualTo("shape-bridge"));
            Assert.That(viewModel.Items[1].DisplayName, Is.EqualTo("Shape Bridge"));
            Assert.That(viewModel.Items[1].Definition, Is.SameAs(second));
            Assert.That(viewModel.Items[1].Order, Is.EqualTo(2));
            Assert.That(viewModel.Items[1].IsUnlocked, Is.False);
            Assert.That(viewModel.Items[1].IsSelectable, Is.False);

            progress.ShapeWorldProgress["world-a"].RecordLevelPass("shape-intro");
            viewModel.InitializeItems("world-a", progress.ShapeWorldProgress);
            Assert.That(viewModel.Items[1].IsUnlocked, Is.True);
            Assert.That(viewModel.Items[1].IsSelectable, Is.True);

            viewModel.InitializeItems("world-b", progress.ShapeWorldProgress);
            Assert.That(viewModel.Items[0].IsUnlocked, Is.False);
            Assert.That(viewModel.Items[0].IsSelectable, Is.False);
            Assert.That(viewModel.Items[1].IsUnlocked, Is.False);
        }

        [Test]
        public void ShapeLevelSelectViewModel_MarksDuplicateKeysInvalid()
        {
            var level = CreateLevel();
            var catalog = CreateCatalog(("duplicate", level), ("duplicate", level));
            var catalogService = new ShapeLevelWorldCatalogService(CreateWorldCatalog(("world-a", catalog)));
            using var viewModel = new ShapeLevelSelectViewModel(null, catalogService);
            viewModel.InitializeItems("world-a", CreateProgress(("world-a", true)).ShapeWorldProgress);

            Assert.That(viewModel.Items, Has.Count.EqualTo(2));
            Assert.That(viewModel.Items[0].IsValid, Is.False);
            Assert.That(viewModel.Items[1].IsValid, Is.False);
            Assert.That(viewModel.Items[0].IsSelectable, Is.False);
            Assert.That(viewModel.Items[1].IsSelectable, Is.False);
        }

        [Test]
        public void ShapeLevelSelectViewModel_MarksWholeCatalogInvalidWhenAnyKeyIsMissing()
        {
            var catalog = CreateCatalog(("shape-intro", CreateLevel()), (" ", CreateLevel()));
            var catalogService = new ShapeLevelWorldCatalogService(CreateWorldCatalog(("world-a", catalog)));
            using var viewModel = new ShapeLevelSelectViewModel(null, catalogService);
            viewModel.InitializeItems("world-a", CreateProgress(("world-a", true)).ShapeWorldProgress);

            Assert.That(viewModel.Items, Has.Count.EqualTo(2));
            Assert.That(viewModel.Items[0].IsValid, Is.False);
            Assert.That(viewModel.Items[0].IsSelectable, Is.False);
        }

        [Test]
        public void ShapeLevelSelectViewModel_WithoutCatalogReturnsNoItems()
        {
            using var viewModel = new ShapeLevelSelectViewModel(null, new ShapeLevelWorldCatalogService(null));
            viewModel.InitializeItems("world-a", CreateProgress(("world-a", true)).ShapeWorldProgress);

            Assert.That(viewModel.Items, Is.Empty);
        }

        private ShapeLevelWorldCatalog CreateWorldCatalog(params (string key, ShapeLevelCatalog catalog)[] worlds)
        {
            var asset = Keep(ScriptableObject.CreateInstance<ShapeLevelWorldCatalog>());
            var field = typeof(ShapeLevelWorldCatalog).GetField("_entries", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Expected serialized world catalog entry list.");
            var entries = new List<ShapeLevelWorldEntry>();
            foreach (var (key, catalog) in worlds)
                entries.Add(new ShapeLevelWorldEntry(key, catalog));
            field.SetValue(asset, entries);
            return asset;
        }

        private ShapeLevelCatalog CreateCatalog(params (string id, ShapeLevelDefinition level)[] entries)
        {
            var catalog = Keep(ScriptableObject.CreateInstance<ShapeLevelCatalog>());
            var entriesField = typeof(ShapeLevelCatalog).GetField("_entries", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(entriesField, Is.Not.Null, "Expected serialized catalog entry list.");
            var catalogEntries = new List<ShapeLevelCatalogEntry>();
            foreach (var (id, level) in entries)
                catalogEntries.Add(new ShapeLevelCatalogEntry(id, level));
            entriesField.SetValue(catalog, catalogEntries);
            return catalog;
        }

        private static PlayerRecordSaveData CreateProgress(params (string worldKey, bool unlocked)[] worlds)
        {
            var progress = new PlayerRecordSaveData();
            foreach (var (worldKey, unlocked) in worlds)
                progress.ShapeWorldProgress.Add(worldKey, new ShapeWorldProgressData(worldKey, unlocked));
            return progress;
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