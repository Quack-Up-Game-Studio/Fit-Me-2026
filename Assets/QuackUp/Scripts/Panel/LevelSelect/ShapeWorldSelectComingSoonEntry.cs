using System;
using System.Collections.Generic;
using UnityEngine;

namespace FitMe.Panel
{
    [Serializable]
    public sealed class ShapeWorldSelectComingSoonEntry
    {
        [SerializeField] private string worldKey;
        [SerializeField] private Sprite lockedSprite;

        public string WorldKey => worldKey;
        public Sprite LockedSprite => lockedSprite;

        public ShapeWorldSelectComingSoonEntry() { }

        public ShapeWorldSelectComingSoonEntry(string worldKey, Sprite lockedSprite)
        {
            this.worldKey = worldKey;
            this.lockedSprite = lockedSprite;
        }
    }

    // Presentation-only entries never enter the gameplay catalog or save data.
    public static class ShapeWorldSelectDisplayItems
    {
        public static List<ShapeWorldSelectItem> Build(
            IReadOnlyList<ShapeWorldSelectItem> worlds,
            IReadOnlyList<ShapeWorldSelectComingSoonEntry> comingSoon)
        {
            var items = new List<ShapeWorldSelectItem>();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            if (worlds != null)
                foreach (var world in worlds)
                {
                    if (world == null) continue;
                    items.Add(world);
                    keys.Add(world.WorldKey);
                }

            if (comingSoon == null) return items;
            foreach (var entry in comingSoon)
            {
                // Publishing a real world automatically replaces its placeholder.
                if (entry == null || string.IsNullOrWhiteSpace(entry.WorldKey) || !keys.Add(entry.WorldKey))
                    continue;
                items.Add(new ShapeWorldSelectItem(entry.WorldKey, entry.WorldKey, false, false,
                    null, entry.LockedSprite));
            }
            return items;
        }
    }
}
