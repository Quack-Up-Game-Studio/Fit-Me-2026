using System;
using FitMe.GameData;

namespace FitMe.Scene
{
    public sealed class ShapeLevelSelectionService
    {
        private readonly ShapeLevelCatalog _catalog;

        public ShapeLevelSelectionService(ShapeLevelCatalog catalog)
        {
            _catalog = catalog;
        }

        public bool TryDispatchSelection(
            string levelId,
            PlayerRecordSaveData progress,
            Action<LevelSessionRequest> dispatch)
        {
            return progress != null && TryDispatchSelection(
                levelId, progress.HighestUnlockedShapeOrder, dispatch);
        }

        public bool TryDispatchSelection(
            string levelId,
            int highestUnlockedOrder,
            Action<LevelSessionRequest> dispatch)
        {
            if (dispatch == null || !_catalog || highestUnlockedOrder < 1 || !_catalog.Validate().IsValid ||
                string.IsNullOrWhiteSpace(levelId) || !_catalog.TryGetById(levelId, out var level))
                return false;

            var levelOrder = _catalog.GetOrder(levelId);

            if (levelOrder == 0 || levelOrder > highestUnlockedOrder ||
                !ShapeLevelDataValidator.Validate(level).IsValid)
                return false;

            dispatch(LevelSessionRequest.ForShapeLevel(levelId, level));
            return true;
        }
    }
}
