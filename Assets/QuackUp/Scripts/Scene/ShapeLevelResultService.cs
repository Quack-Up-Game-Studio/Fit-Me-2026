using FitMe.GameData;

namespace FitMe.Scene
{
    public enum ShapeLevelResultAction
    {
        None,
        Retry,
        NextLevel,
        ReturnToLevelSelect
    }

    public readonly struct ShapeLevelResultOutcome
    {
        public ShapeLevelResultAction Action { get; }
        public LevelSessionRequest SessionRequest { get; }

        public ShapeLevelResultOutcome(ShapeLevelResultAction action, LevelSessionRequest sessionRequest)
        {
            Action = action;
            SessionRequest = sessionRequest;
        }
    }

    public sealed class ShapeLevelResultService
    {
        private readonly ShapeLevelCatalog _catalog;

        public ShapeLevelResultService(ShapeLevelCatalog catalog)
        {
            _catalog = catalog;
        }

        public bool TryResolve(
            ShapeLevelResultAction action,
            LevelSessionRequest activeRequest,
            bool wasPassed,
            PlayerRecordSaveData progress,
            out ShapeLevelResultOutcome outcome)
        {
            outcome = new ShapeLevelResultOutcome(ShapeLevelResultAction.None, default);
            if (!_catalog || progress == null || !TryGetActiveOrder(activeRequest, out var activeOrder))
                return false;

            switch (action)
            {
                case ShapeLevelResultAction.Retry:
                    outcome = new ShapeLevelResultOutcome(action, activeRequest);
                    return true;
                case ShapeLevelResultAction.NextLevel:
                    if (!wasPassed) return false;
                    var nextOrder = activeOrder + 1;
                    if (nextOrder > progress.HighestUnlockedShapeOrder || nextOrder > _catalog.Levels.Count)
                        return false;

                    var nextLevelId = GetIdAtOrder(nextOrder);
                    if (nextLevelId == null || !_catalog.TryGetById(nextLevelId, out var nextLevel) ||
                        !ShapeLevelDataValidator.Validate(nextLevel).IsValid)
                        return false;

                    outcome = new ShapeLevelResultOutcome(
                        action, LevelSessionRequest.ForShapeLevel(nextLevelId, nextLevel));
                    return true;
                case ShapeLevelResultAction.ReturnToLevelSelect:
                    outcome = new ShapeLevelResultOutcome(action, default);
                    return true;
                default:
                    return false;
            }
        }

        private bool TryGetActiveOrder(LevelSessionRequest request, out int order)
        {
            order = 0;
            if (request.GameMode != FitMe.Shared.GameMode.LevelShape ||
                string.IsNullOrWhiteSpace(request.ShapeLevelId) || !request.ShapeLevel ||
                !_catalog.TryGetById(request.ShapeLevelId, out var catalogLevel) ||
                catalogLevel != request.ShapeLevel)
                return false;

            order = GetOrder(request.ShapeLevelId);
            return order > 0;
        }

        private int GetOrder(string levelId)
        {
            var keys = _catalog.Levels.Keys.GetEnumerator();
            for (var index = 0; index < _catalog.Levels.Count; index++)
            {
                if (!keys.MoveNext()) return 0;
                if (string.Equals(keys.Current, levelId, System.StringComparison.Ordinal)) return index + 1;
            }

            return 0;
        }

        private string GetIdAtOrder(int order)
        {
            if (order < 1 || order > _catalog.Levels.Count) return null;
            var keys = _catalog.Levels.Keys.GetEnumerator();
            for (var index = 0; index < order; index++)
            {
                if (!keys.MoveNext()) return null;
            }

            return keys.Current;
        }
    }
}
