using System;
using System.Collections.Generic;
using FitMe.GameData;
using QuackUp.Save;
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
        private List<ShapeLevelSelectItem> _items;

        public IReadOnlyList<ShapeLevelSelectItem> Items => _items;
        public ReactiveCommand<string> SelectLevelCommand { get; } = new();
        public ReactiveCommand<Unit> ReturnToModeSelectCommand { get; } = new();
        public Observable<IReadOnlyList<ShapeLevelSelectItem>> ItemInitialized => _itemInitialized;
        private readonly Subject<IReadOnlyList<ShapeLevelSelectItem>> _itemInitialized = new();
        
        private readonly ShapeLevelCatalog _catalog;

        public ShapeLevelSelectViewModel(
            PanelManager panelManager,
            ShapeLevelCatalog catalog) : base(panelManager)
        {
            _catalog = catalog;
        }

        public override void Dispose()
        {
            base.Dispose();
            SelectLevelCommand.Dispose();
            ReturnToModeSelectCommand.Dispose();
            _itemInitialized.Dispose();
        }

        public void InitializeItems(
            int highestUnlockedOrder)
        {
            _items = BuildItems(_catalog, Math.Max(1, highestUnlockedOrder));
            _itemInitialized.OnNext(_items);
        }

        private static List<ShapeLevelSelectItem> BuildItems(
            ShapeLevelCatalog catalog,
            int highestUnlockedOrder)
        {
            var items = new List<ShapeLevelSelectItem>();
            if (!catalog) return items;

            var catalogIsValid = catalog.Validate().IsValid;
            var entries = catalog.Entries;
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                var levelId = entry?.Key;
                var definition = entry?.LevelDefinition;
                var order = index + 1;
                var isValid = catalogIsValid && !string.IsNullOrWhiteSpace(levelId) && catalog.GetOrder(levelId) == order && definition &&
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
