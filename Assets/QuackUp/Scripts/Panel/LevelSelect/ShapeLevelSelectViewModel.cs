using System;
using System.Collections.Generic;
using FitMe.GameData;
using R3;

namespace FitMe.Panel
{
    public readonly struct ShapeLevelSelectItem
    {
        public string LevelId { get; }
        public string DisplayName { get; }
        public int Order { get; }
        public bool IsUnlocked { get; }
        public bool IsValid { get; }
        public bool IsSelectable => IsUnlocked && IsValid;
        public ShapeLevelDefinition Definition { get; }

        public ShapeLevelSelectItem(
            string levelId,
            string displayName,
            int order,
            bool isUnlocked,
            bool isValid,
            ShapeLevelDefinition definition)
        {
            LevelId = levelId;
            DisplayName = displayName;
            Order = order;
            IsUnlocked = isUnlocked;
            IsValid = isValid;
            Definition = definition;
        }
    }

    public sealed class ShapeLevelSelectViewModel : PanelViewModel
    {
        private readonly List<ShapeLevelSelectItem> _items;

        public IReadOnlyList<ShapeLevelSelectItem> Items => _items;
        public ReactiveCommand<string> SelectLevelCommand { get; } = new();
        public ReactiveCommand<Unit> ReturnToModeSelectCommand { get; } = new();

        public ShapeLevelSelectViewModel(
            PanelManager panelManager,
            ShapeLevelCatalog catalog,
            int highestUnlockedOrder) : base(panelManager)
        {
            _items = BuildItems(catalog, Math.Max(1, highestUnlockedOrder));
        }

        public override void Dispose()
        {
            base.Dispose();
            SelectLevelCommand.Dispose();
            ReturnToModeSelectCommand.Dispose();
        }

        private static List<ShapeLevelSelectItem> BuildItems(
            ShapeLevelCatalog catalog,
            int highestUnlockedOrder)
        {
            var items = new List<ShapeLevelSelectItem>();
            if (!catalog) return items;

            var levels = catalog.Levels;
            var keys = levels.Keys.GetEnumerator();
            for (var index = 0; index < levels.Count; index++)
            {
                if (!keys.MoveNext()) break;

                // The catalog preserves insertion order for both keys and indexed values.
                var levelId = keys.Current;
                var definition = levels[index];
                var order = index + 1;
                var isValid = !string.IsNullOrWhiteSpace(levelId) && definition &&
                    ShapeLevelDataValidator.Validate(definition).IsValid;
                var displayName = definition && !string.IsNullOrWhiteSpace(definition.name)
                    ? definition.name
                    : levelId;

                items.Add(new ShapeLevelSelectItem(
                    levelId,
                    displayName,
                    order,
                    order <= highestUnlockedOrder,
                    isValid,
                    definition));
            }

            return items;
        }
    }
}
