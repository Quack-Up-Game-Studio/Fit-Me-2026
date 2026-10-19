using System.Collections.Generic;
using FitMe.GameData;

namespace FitMe.Scene
{
    public sealed class ShapeLevelProgressService
    {
        private readonly ShapeLevelWorldCatalogService _catalogService;

        public ShapeLevelProgressService(ShapeLevelWorldCatalogService catalogService)
        {
            _catalogService = catalogService;
        }

        public bool TryRecordPass(
            string levelId,
            string worldKey,
            ShapeLevelDefinition completedLevel,
            PlayerRecordSaveData progress,
            out bool progressChanged)
        {
            progressChanged = false;
            if (_catalogService == null || !_catalogService.IsValid || progress == null ||
                !_catalogService.TryGetLevel(worldKey, levelId, out var catalogLevel) || catalogLevel != completedLevel)
                return false;

            progress.ShapeWorldProgress ??= new Dictionary<string, ShapeWorldProgressData>();
            var initializationChanged = false;
            foreach (var world in _catalogService.Worlds)
                if (!progress.ShapeWorldProgress.ContainsKey(world.WorldKey)) initializationChanged = true;
            ShapeWorldProgressRules.InitializeUnlocks(_catalogService, progress.ShapeWorldProgress);
            if (!progress.ShapeWorldProgress.TryGetValue(worldKey, out var worldProgress) || !worldProgress.IsUnlocked ||
                _catalogService.GetLevelOrder(worldKey, levelId) > (worldProgress.CompletedLevelIds?.Count ?? 0) + 1)
                return false;

            var wasCompleted = worldProgress.CompletedLevelIds?.Contains(levelId) == true;
            var completionChanged = worldProgress.RecordLevelPass(levelId);
            var nextOrder = _catalogService.GetWorldOrder(worldKey) + 1;
            var unlocksNext = false;
            if (_catalogService.IsWorldComplete(worldKey, progress.ShapeWorldProgress) &&
                _catalogService.TryGetWorldAtOrder(nextOrder, out var nextWorld) &&
                progress.ShapeWorldProgress.TryGetValue(nextWorld.WorldKey, out var nextProgress) && !nextProgress.IsUnlocked)
            {
                nextProgress.Unlock();
                unlocksNext = true;
            }
            progressChanged = initializationChanged || completionChanged || unlocksNext || !wasCompleted;
            return true;
        }
    }
}
