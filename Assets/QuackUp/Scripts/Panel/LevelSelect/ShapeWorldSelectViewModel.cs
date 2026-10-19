using System.Collections.Generic;
using FitMe.GameData;
using R3;
using VContainer;

namespace FitMe.Panel
{
    public sealed class ShapeWorldSelectItem
    {
        public string WorldKey { get; }
        public string DisplayName { get; }
        public bool IsUnlocked { get; }
        public bool IsComplete { get; }

        public ShapeWorldSelectItem(string worldKey, string displayName, bool isUnlocked, bool isComplete)
        {
            WorldKey = worldKey;
            DisplayName = displayName;
            IsUnlocked = isUnlocked;
            IsComplete = isComplete;
        }
    }

    public sealed class ShapeWorldSelectViewModel : PanelViewModel
    {
        private readonly ShapeLevelWorldCatalogService _catalogService;
        private List<ShapeWorldSelectItem> _items = new();

        public IReadOnlyList<ShapeWorldSelectItem> Items => _items;
        public ReactiveCommand<string> SelectWorldCommand { get; } = new();
        public ReactiveCommand<Unit> ReturnToModeSelectCommand { get; } = new();
        public Observable<IReadOnlyList<ShapeWorldSelectItem>> ItemInitialized => _itemInitialized;
        private readonly Subject<IReadOnlyList<ShapeWorldSelectItem>> _itemInitialized = new();

        [Inject]
        public ShapeWorldSelectViewModel(PanelManager panelManager, ShapeLevelWorldCatalogService catalogService) : base(panelManager)
        {
            _catalogService = catalogService;
        }

        public void InitializeItems(PlayerRecordSaveData saveData)
        {
            var progress = saveData?.ShapeWorldProgress ?? new Dictionary<string, ShapeWorldProgressData>();
            _items = new List<ShapeWorldSelectItem>();
            if (_catalogService != null)
                foreach (var world in _catalogService.Worlds)
                {
                    progress.TryGetValue(world.WorldKey, out var state);
                    _items.Add(new ShapeWorldSelectItem(world.WorldKey, world.DisplayName,
                        state?.IsUnlocked == true,
                        _catalogService.IsWorldComplete(world.WorldKey, progress)));
                }
            _itemInitialized.OnNext(_items);
        }

        public override void Dispose()
        {
            base.Dispose();
            SelectWorldCommand.Dispose();
            ReturnToModeSelectCommand.Dispose();
            _itemInitialized.Dispose();
        }
    }
}
