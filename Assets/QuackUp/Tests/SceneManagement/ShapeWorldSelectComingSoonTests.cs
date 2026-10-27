using System.Collections.Generic;
using System.Linq;
using FitMe.Panel;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace QuackUp.SceneManagement.Tests
{
    public sealed class ShapeWorldSelectComingSoonTests
    {
        [Test]
        public void ThreeComingSoonWorldsAreAppendedLockedWithTheirOwnArtwork()
        {
            var real = RealWorlds();
            var placeholders = ComingSoon();

            var display = ShapeWorldSelectDisplayItems.Build(real, placeholders);

            Assert.That(display.Select(item => item.WorldKey),
                Is.EqualTo(new[] { "world-1", "world-2", "world-3", "world-4", "world-5" }));
            for (var i = 0; i < placeholders.Count; i++)
            {
                var item = display[real.Count + i];
                Assert.That(item.IsUnlocked, Is.False);
                Assert.That(item.IsComplete, Is.False);
                Assert.That(item.UnlockedSprite, Is.Null);
                Assert.That(item.LockedSprite, Is.SameAs(placeholders[i].LockedSprite));
                Assert.That(item.LockedSprite, Is.Not.Null);
            }
        }

        [Test]
        public void PublishingARealWorldReplacesItsPlaceholder()
        {
            var published = new ShapeWorldSelectItem("world-3", "Published World", true, true);
            var real = RealWorlds();
            real.Add(published);

            var display = ShapeWorldSelectDisplayItems.Build(real, ComingSoon());

            Assert.That(display.Count(item => item.WorldKey == "world-3"), Is.EqualTo(1));
            Assert.That(display[2], Is.SameAs(published));
            Assert.That(display[2].IsUnlocked, Is.True);
            Assert.That(display.Select(item => item.WorldKey),
                Is.EqualTo(new[] { "world-1", "world-2", "world-3", "world-4", "world-5" }));
        }

        [Test]
        public void EmptyAndDuplicatePlaceholderKeysAreIgnored()
        {
            var placeholders = new[] {
                null,
                new ShapeWorldSelectComingSoonEntry("", null),
                new ShapeWorldSelectComingSoonEntry("world-3", null),
                new ShapeWorldSelectComingSoonEntry("world-3", null),
                new ShapeWorldSelectComingSoonEntry("   ", null)
            };

            var display = ShapeWorldSelectDisplayItems.Build(RealWorlds(), placeholders);

            Assert.That(display.Select(item => item.WorldKey), Is.EqualTo(new[] { "world-1", "world-2", "world-3" }));
        }

        [Test]
        public void DisplayPlaceholdersDoNotMutateTheRealWorldList()
        {
            var real = RealWorlds();
            var first = real[0];
            var second = real[1];

            var display = ShapeWorldSelectDisplayItems.Build(real, ComingSoon());

            Assert.That(real.Count, Is.EqualTo(2));
            Assert.That(display, Is.Not.SameAs(real));
            Assert.That(display[0], Is.SameAs(first));
            Assert.That(display[1], Is.SameAs(second));
            Assert.That(real[0].IsUnlocked, Is.True);
            Assert.That(real[1].IsUnlocked, Is.False);
        }

        [Test]
        public void MissingOptionalListsProduceAnEmptyDisplayWithoutProgressionEntries()
        {
            Assert.That(ShapeWorldSelectDisplayItems.Build(null, null), Is.Empty);
        }

        private static List<ShapeWorldSelectItem> RealWorlds() => new()
        {
            new ShapeWorldSelectItem("world-1", "WORLD 1", true, false),
            new ShapeWorldSelectItem("world-2", "WORLD 2", false, false)
        };

        private static List<ShapeWorldSelectComingSoonEntry> ComingSoon()
        {
            var entries = new List<ShapeWorldSelectComingSoonEntry>();
            for (var world = 3; world <= 5; world++)
            {
                var path = "Assets/QuackUp/Arts/ArtAssets/02_Screens/World_Select/World_" + world + "_Locked.png";
                var art = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Single();
                entries.Add(new ShapeWorldSelectComingSoonEntry("world-" + world, art));
            }
            return entries;
        }
    }
}
