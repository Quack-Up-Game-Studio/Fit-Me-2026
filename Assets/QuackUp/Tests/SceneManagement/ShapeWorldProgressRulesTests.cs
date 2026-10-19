using System.Collections.Generic;
using FitMe.GameData;
using FitMe.Scene;
using NUnit.Framework;
using UnityEngine;

namespace QuackUp.SceneManagement.Tests
{
    public sealed class ShapeWorldProgressRulesTests
    {
        private readonly List<ScriptableObject> _assets = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _assets) if (asset) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        [Test]
        public void InitialProgress_UnlocksOnlyFirstWorld()
        {
            var catalogService = new ShapeLevelWorldCatalogService(CreateWorlds());
            var progress = new Dictionary<string, ShapeWorldProgressData>();

            ShapeWorldProgressRules.InitializeUnlocks(catalogService, progress);

            Assert.That(progress["world-a"].IsUnlocked, Is.True);
            Assert.That(progress["world-b"].IsUnlocked, Is.False);
        }

        [Test]
        public void CompletingAllLevelsUnlocksNextWorld_AndNewLevelMakesWorldIncomplete()
        {
            var catalogService = new ShapeLevelWorldCatalogService(CreateWorlds());
            var progress = new Dictionary<string, ShapeWorldProgressData>();
            ShapeWorldProgressRules.InitializeUnlocks(catalogService, progress);
            ShapeWorldProgressRules.RecordPassAndUnlockNext(catalogService, progress, "world-a", "a1");

            Assert.That(catalogService.IsWorldComplete("world-a", progress), Is.True);
            Assert.That(progress["world-b"].IsUnlocked, Is.True);

            var expanded = CreateLevelCatalog(("a1", CreateLevel()), ("a2", CreateLevel()));
            var expandedCatalogService = new ShapeLevelWorldCatalogService(CreateWorlds(expanded));
            Assert.That(expandedCatalogService.IsWorldComplete("world-a", progress), Is.False);
            Assert.That(progress["world-b"].IsUnlocked, Is.True);
        }

        [Test]
        public void InvalidAndEmptyWorldCatalogsAreIncomplete()
        {
            var emptyLevelCatalog = Keep(ScriptableObject.CreateInstance<ShapeLevelCatalog>());
            var catalogService = new ShapeLevelWorldCatalogService(CreateWorlds(emptyLevelCatalog));
            var progress = new Dictionary<string, ShapeWorldProgressData>
            {
                ["world-a"] = new("world-a", true)
            };

            Assert.That(catalogService.IsWorldComplete("world-a", progress), Is.False);
        }

        private ShapeLevelWorldCatalog CreateWorlds(ShapeLevelCatalog firstCatalog = null)
        {
            var first = firstCatalog ? firstCatalog : CreateLevelCatalog(("a1", CreateLevel()));
            var second = CreateLevelCatalog(("b1", CreateLevel()));
            var worlds = Keep(ScriptableObject.CreateInstance<ShapeLevelWorldCatalog>());
            typeof(ShapeLevelWorldCatalog).GetField("_entries", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(worlds, new List<ShapeLevelWorldEntry> { new("world-a", first), new("world-b", second) });
            return worlds;
        }

        private ShapeLevelCatalog CreateLevelCatalog(params (string id, ShapeLevelDefinition level)[] entries)
        {
            var catalog = Keep(ScriptableObject.CreateInstance<ShapeLevelCatalog>());
            var list = new List<ShapeLevelCatalogEntry>();
            foreach (var (id, level) in entries) list.Add(new ShapeLevelCatalogEntry(id, level));
            typeof(ShapeLevelCatalog).GetField("_entries", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(catalog, list);
            return catalog;
        }

        private ShapeLevelDefinition CreateLevel()
        {
            var board = Keep(ScriptableObject.CreateInstance<FitMe.Grid.GridPreset>());
            board.PresetGridType = FitMe.Grid.GridType.Rectangle;
            board.GridSize = Vector2Int.one;
            var preset = Keep(ScriptableObject.CreateInstance<FitMe.Grid.BlockPreset>());
            preset.BlockSchema.schema = new[,] { { 1 } };
            var definition = Keep(ScriptableObject.CreateInstance<ShapeLevelDefinition>());
            typeof(ShapeLevelDefinition).GetField("_board", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(definition, board);
            typeof(ShapeLevelDefinition).GetField("_playerBlocks", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(definition, new List<ShapePlayerBlockEntry> { new(preset, 1) });
            return definition;
        }

        private T Keep<T>(T asset) where T : ScriptableObject { _assets.Add(asset); return asset; }
    }
}
