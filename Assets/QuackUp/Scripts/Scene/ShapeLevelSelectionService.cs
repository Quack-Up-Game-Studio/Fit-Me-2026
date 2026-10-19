using System;
using FitMe.GameData;

namespace FitMe.Scene
{
    public sealed class ShapeLevelSelectionService
    {
        private readonly ShapeLevelWorldCatalogService _catalogService;

        public ShapeLevelSelectionService(ShapeLevelWorldCatalogService catalogService)
        {
            _catalogService = catalogService;
        }

        public bool TryDispatchSelection(string worldKey, string levelId, PlayerRecordSaveData progress,
            Action<LevelSessionRequest> dispatch)
        {
            return progress != null && TryDispatchSelection(worldKey, levelId, progress.ShapeWorldProgress, dispatch);
        }

        public bool TryDispatchSelection(string worldKey, string levelId,
            System.Collections.Generic.IDictionary<string, ShapeWorldProgressData> progress, Action<LevelSessionRequest> dispatch)
        {
            if (dispatch == null || _catalogService == null || !_catalogService.IsValid || progress == null ||
                !_catalogService.TryGetLevel(worldKey, levelId, out var level) ||
                !progress.TryGetValue(worldKey, out var worldProgress) || !worldProgress.IsUnlocked)
                return false;

            var levelOrder = _catalogService.GetLevelOrder(worldKey, levelId);
            if (levelOrder == 0) return false;

            for (var order = 1; order < levelOrder; order++)
            {
                if (!_catalogService.TryGetLevelAtOrder(worldKey, order, out var precedingLevelId, out _) ||
                    worldProgress.CompletedLevelIds?.Contains(precedingLevelId) != true)
                    return false;
            }

            dispatch(LevelSessionRequest.ForShapeLevel(worldKey, levelId, level));
            return true;
        }
    }
}
