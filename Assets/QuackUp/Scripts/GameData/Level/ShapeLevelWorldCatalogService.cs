using System;
using System.Collections.Generic;
using UnityEngine;

namespace FitMe.GameData
{
    public readonly struct ShapeLevelWorldInfo
    {
        public string WorldKey { get; }
        public string DisplayName { get; }
        public int Order { get; }

        public ShapeLevelWorldInfo(string worldKey, string displayName, int order)
            : this(worldKey, displayName, order, null, null) { }

        public Sprite UnlockedSprite { get; }
        public Sprite LockedSprite { get; }
        public string Subtitle { get; }

        public ShapeLevelWorldInfo(string worldKey, string displayName, int order, Sprite unlockedSprite, Sprite lockedSprite)
            : this(worldKey, displayName, order, unlockedSprite, lockedSprite, string.Empty) { }

        public ShapeLevelWorldInfo(string worldKey, string displayName, int order, Sprite unlockedSprite, Sprite lockedSprite, string subtitle)
        {
            WorldKey = worldKey;
            DisplayName = displayName;
            Order = order;
            UnlockedSprite = unlockedSprite;
            LockedSprite = lockedSprite;
            Subtitle = subtitle ?? string.Empty;
        }
    }

    public readonly struct ShapeLevelInfo
    {
        public string LevelId { get; }
        public string DisplayName { get; }
        public int Order { get; }
        public bool IsValid { get; }
        public ShapeLevelDefinition Definition { get; }

        public ShapeLevelInfo(string levelId, string displayName, int order, bool isValid, ShapeLevelDefinition definition)
        {
            LevelId = levelId;
            DisplayName = displayName;
            Order = order;
            IsValid = isValid;
            Definition = definition;
        }
    }

    public sealed class ShapeLevelWorldCatalogService
    {
        private readonly ShapeLevelWorldCatalog _catalog;

        public ShapeLevelWorldCatalogService(ShapeLevelWorldCatalog catalog)
        {
            _catalog = catalog;
        }

        public bool IsValid => _catalog && _catalog.IsValid;
        public int WorldCount => IsValid ? _catalog.Count : 0;

        public IReadOnlyList<ShapeLevelWorldInfo> Worlds
        {
            get
            {
                if (!IsValid) return Array.Empty<ShapeLevelWorldInfo>();

                var worlds = new List<ShapeLevelWorldInfo>(_catalog.Count);
                for (var index = 0; index < _catalog.Count; index++)
                {
                    var entry = _catalog.Entries[index];
                    worlds.Add(new ShapeLevelWorldInfo(entry.WorldKey, entry.LevelCatalog.name, index + 1,
                        entry.UnlockedSprite, entry.LockedSprite, entry.Subtitle));
                }
                return worlds;
            }
        }

        public bool TryGetWorld(string worldKey, out ShapeLevelWorldInfo worldInfo)
        {
            worldInfo = default;
            if (!IsValid || !_catalog.TryGetByKey(worldKey, out var entry)) return false;

            var order = _catalog.GetOrder(worldKey);
            if (order == 0) return false;
            worldInfo = new ShapeLevelWorldInfo(entry.WorldKey, entry.LevelCatalog.name, order,
                entry.UnlockedSprite, entry.LockedSprite, entry.Subtitle);
            return true;
        }

        public int GetWorldOrder(string worldKey)
        {
            if (!IsValid || string.IsNullOrWhiteSpace(worldKey)) return 0;
            return _catalog.GetOrder(worldKey);
        }

        public bool TryGetWorldAtOrder(int order, out ShapeLevelWorldInfo worldInfo)
        {
            worldInfo = default;
            if (!IsValid || order < 1 || order > _catalog.Count) return false;

            var entry = _catalog.Entries[order - 1];
            worldInfo = new ShapeLevelWorldInfo(entry.WorldKey, entry.LevelCatalog.name, order,
                entry.UnlockedSprite, entry.LockedSprite, entry.Subtitle);
            return true;
        }

        public bool IsWorldComplete(string worldKey, IDictionary<string, ShapeWorldProgressData> progress)
        {
            if (!TryGetWorld(worldKey, out _) || progress == null ||
                !progress.TryGetValue(worldKey, out var worldProgress) || !worldProgress.IsUnlocked ||
                !_catalog.TryGetByKey(worldKey, out var world) || !world.LevelCatalog.Validate().IsValid ||
                world.LevelCatalog.Count == 0)
                return false;

            foreach (var entry in world.LevelCatalog.Entries)
                if (entry == null || string.IsNullOrWhiteSpace(entry.Key) ||
                    worldProgress.CompletedLevelIds?.Contains(entry.Key) != true)
                    return false;

            return true;
        }

        public IReadOnlyList<ShapeLevelInfo> GetLevels(string worldKey)
        {
            if (!_catalog || !_catalog.TryGetByKey(worldKey, out var world))
                return Array.Empty<ShapeLevelInfo>();

            var levelCatalog = world.LevelCatalog;
            var catalogIsValid = levelCatalog && levelCatalog.Validate().IsValid;
            var entries = levelCatalog ? levelCatalog.Entries : Array.Empty<ShapeLevelCatalogEntry>();
            var levels = new List<ShapeLevelInfo>(entries.Count);
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                var levelId = entry?.Key;
                var definition = entry?.LevelDefinition;
                var isValid = catalogIsValid && !string.IsNullOrWhiteSpace(levelId) &&
                    levelCatalog.GetOrder(levelId) == index + 1 && definition &&
                    ShapeLevelDataValidator.Validate(definition).IsValid;
                var displayName = definition && !string.IsNullOrWhiteSpace(definition.name)
                    ? definition.name
                    : levelId;
                levels.Add(new ShapeLevelInfo(levelId, displayName, index + 1, isValid, definition));
            }
            return levels;
        }

        public bool TryGetLevel(string worldKey, string levelId, out ShapeLevelDefinition level)
        {
            level = null;
            if (!TryGetWorld(worldKey, out _) || string.IsNullOrWhiteSpace(levelId) ||
                !_catalog.TryGetByKey(worldKey, out var world) || !world.LevelCatalog.TryGetById(levelId, out var candidate) ||
                !ShapeLevelDataValidator.Validate(candidate).IsValid)
                return false;

            level = candidate;
            return true;
        }

        public int GetLevelOrder(string worldKey, string levelId)
        {
            if (!TryGetWorld(worldKey, out _) || !_catalog.TryGetByKey(worldKey, out var world)) return 0;
            return world.LevelCatalog.GetOrder(levelId);
        }

        public bool TryGetLevelAtOrder(string worldKey, int order, out string levelId, out ShapeLevelDefinition level)
        {
            levelId = null;
            level = null;
            if (!TryGetWorld(worldKey, out _) || !_catalog.TryGetByKey(worldKey, out var world) ||
                !world.LevelCatalog.Validate().IsValid || order < 1 || order > world.LevelCatalog.Count)
                return false;

            var candidateId = world.LevelCatalog.GetIdAtOrder(order);
            if (!TryGetLevel(worldKey, candidateId, out var candidate)) return false;
            levelId = candidateId;
            level = candidate;
            return true;
        }

        public bool TryGetFirstLevel(string worldKey, out string levelId, out ShapeLevelDefinition level) =>
            TryGetLevelAtOrder(worldKey, 1, out levelId, out level);

        public bool TryGetNextLevel(
            string worldKey,
            string currentLevelId,
            ShapeLevelDefinition currentLevel,
            bool wrapAtEnd,
            out string nextLevelId,
            out ShapeLevelDefinition nextLevel)
        {
            nextLevelId = null;
            nextLevel = null;
            if (!TryGetLevel(worldKey, currentLevelId, out var catalogLevel) || catalogLevel != currentLevel)
                return false;

            var levelCount = GetLevelCount(worldKey);
            var currentOrder = GetLevelOrder(worldKey, currentLevelId);
            if (levelCount == 0 || currentOrder == 0) return false;

            var nextOrder = currentOrder + 1;
            if (nextOrder > levelCount)
            {
                if (!wrapAtEnd) return false;
                nextOrder = 1;
            }

            return TryGetLevelAtOrder(worldKey, nextOrder, out nextLevelId, out nextLevel);
        }

        public int GetLevelCount(string worldKey)
        {
            if (!TryGetWorld(worldKey, out _) || !_catalog.TryGetByKey(worldKey, out var world) ||
                !world.LevelCatalog.Validate().IsValid)
                return 0;
            return world.LevelCatalog.Count;
        }
    }
}
