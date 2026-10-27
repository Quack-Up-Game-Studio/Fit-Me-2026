using System;
using System.Collections.Generic;
using FitMe.GameData;
using R3;
using VContainer;

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
        private readonly ShapeLevelWorldCatalogService _catalogService;
        private List<ShapeLevelSelectItem> _items;

        public IReadOnlyList<ShapeLevelSelectItem> Items => _items;
        public ReactiveProperty<string> WorldDisplayName { get; } = new(string.Empty);
        public ReactiveProperty<string> WorldSubtitle { get; } = new(string.Empty);
        public ReactiveCommand<string> SelectLevelCommand { get; } = new();
        public ReactiveCommand<Unit> BackToWorldsCommand { get; } = new();
        public Observable<IReadOnlyList<ShapeLevelSelectItem>> ItemInitialized => _itemInitialized;
        private readonly Subject<IReadOnlyList<ShapeLevelSelectItem>> _itemInitialized = new();

        [Inject]
        public ShapeLevelSelectViewModel(PanelManager panelManager, ShapeLevelWorldCatalogService catalogService) : base(panelManager)
        {
            _catalogService = catalogService;
        }

        public override void Dispose()
        {
            base.Dispose();
            WorldDisplayName.Dispose();
            WorldSubtitle.Dispose();
            SelectLevelCommand.Dispose();
            BackToWorldsCommand.Dispose();
            _itemInitialized.Dispose();
        }

        public void InitializeItems(
            string worldKey,
            System.Collections.Generic.IDictionary<string, ShapeWorldProgressData> progress)
        {
            if (_catalogService != null && _catalogService.TryGetWorld(worldKey, out var worldInfo))
            {
                WorldDisplayName.Value = worldInfo.DisplayName;
                WorldSubtitle.Value = worldInfo.Subtitle;
            }
            else
            {
                WorldDisplayName.Value = string.Empty;
                WorldSubtitle.Value = string.Empty;
            }

            ShapeWorldProgressData worldProgress = null;
            if (progress != null) progress.TryGetValue(worldKey, out worldProgress);
            var levels = _catalogService?.GetLevels(worldKey) ?? Array.Empty<ShapeLevelInfo>();
            _items = BuildItems(levels, worldProgress);
            _itemInitialized.OnNext(_items);
        }

        private static List<ShapeLevelSelectItem> BuildItems(
            IReadOnlyList<ShapeLevelInfo> levels,
            ShapeWorldProgressData worldProgress)
        {
            var items = new List<ShapeLevelSelectItem>(levels.Count);
            for (var index = 0; index < levels.Count; index++)
            {
                var level = levels[index];
                var unlocked = worldProgress?.IsUnlocked == true;
                for (var precedingIndex = 0; unlocked && precedingIndex < index; precedingIndex++)
                {
                    var precedingId = levels[precedingIndex].LevelId;
                    if (string.IsNullOrWhiteSpace(precedingId) || worldProgress.CompletedLevelIds?.Contains(precedingId) != true)
                        unlocked = false;
                }

                items.Add(new ShapeLevelSelectItem(
                    level.LevelId,
                    level.DisplayName,
                    level.Order,
                    unlocked,
                    level.IsValid,
                    level.Definition));
            }
            return items;
        }
    }
}
