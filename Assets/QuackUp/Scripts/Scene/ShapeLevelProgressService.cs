using FitMe.GameData;

namespace FitMe.Scene
{
    public sealed class ShapeLevelProgressService
    {
        private readonly ShapeLevelCatalog _catalog;

        public ShapeLevelProgressService(ShapeLevelCatalog catalog)
        {
            _catalog = catalog;
        }

        public bool TryRecordPass(
            string levelId,
            ShapeLevelDefinition completedLevel,
            PlayerRecordSaveData progress,
            int score,
            out bool progressChanged)
        {
            progressChanged = false;
            if (!_catalog || progress == null || !_catalog.Validate().IsValid ||
                string.IsNullOrWhiteSpace(levelId) ||
                !_catalog.TryGetById(levelId, out var catalogLevel) || catalogLevel != completedLevel)
                return false;

            var completedOrder = _catalog.GetOrder(levelId);
            if (completedOrder < 1 || !progress.IsShapeLevelUnlocked(completedOrder))
                return false;

            progressChanged = progress.RecordShapeLevelPass(completedOrder, levelId, score);
            return true;
        }
    }
}
