using FitMe.GameData;

namespace FitMe.Scene
{
    public enum ShapeLevelResultAction
    {
        None,
        Retry,
        NextLevel,
        NextWorld,
        ReturnToLevelSelect
    }

    public readonly struct ShapeLevelNextWorldAvailability
    {
        public bool IsVisible { get; }
        public bool IsEnabled { get; }

        public ShapeLevelNextWorldAvailability(bool isVisible, bool isEnabled)
        {
            IsVisible = isVisible;
            IsEnabled = isEnabled;
        }
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
        private readonly ShapeLevelWorldCatalogService _catalogService;

        public ShapeLevelResultService(ShapeLevelWorldCatalogService catalogService)
        {
            _catalogService = catalogService;
        }

        public bool TryResolve(
            ShapeLevelResultAction action,
            LevelSessionRequest activeRequest,
            bool wasPassed,
            PlayerRecordSaveData progress,
            out ShapeLevelResultOutcome outcome)
        {
            outcome = new ShapeLevelResultOutcome(ShapeLevelResultAction.None, default);
            if (_catalogService == null || progress == null || !_catalogService.IsValid ||
                !TryGetActiveOrder(_catalogService, activeRequest, out var activeOrder))
                return false;

            switch (action)
            {
                case ShapeLevelResultAction.Retry:
                    outcome = new ShapeLevelResultOutcome(action, activeRequest);
                    return true;
                case ShapeLevelResultAction.NextLevel:
                    if (!wasPassed || IsCompletedFinalLevel(activeRequest, progress)) return false;

                    var nextOrder = activeOrder + 1;
                    if (nextOrder > _catalogService.GetLevelCount(activeRequest.ShapeWorldKey) ||
                        !progress.ShapeWorldProgress.TryGetValue(activeRequest.ShapeWorldKey, out var worldProgress) ||
                        nextOrder > (worldProgress.CompletedLevelIds?.Count ?? 0) + 1)
                        return false;

                    if (!_catalogService.TryGetLevelAtOrder(activeRequest.ShapeWorldKey, nextOrder,
                            out var nextLevelId, out var nextLevel))
                        return false;

                    outcome = new ShapeLevelResultOutcome(
                        action, LevelSessionRequest.ForShapeLevel(activeRequest.ShapeWorldKey, nextLevelId, nextLevel));
                    return true;
                case ShapeLevelResultAction.NextWorld:
                    return wasPassed && TryResolveNextWorld(activeRequest, progress, out outcome);
                case ShapeLevelResultAction.ReturnToLevelSelect:
                    outcome = new ShapeLevelResultOutcome(action, default);
                    return true;
                default:
                    return false;
            }
        }

        public bool IsCompletedFinalLevel(LevelSessionRequest activeRequest, PlayerRecordSaveData progress)
        {
            return _catalogService != null && progress != null && _catalogService.IsValid &&
                   TryGetActiveOrder(_catalogService, activeRequest, out var activeOrder) &&
                   activeOrder == _catalogService.GetLevelCount(activeRequest.ShapeWorldKey) &&
                   _catalogService.IsWorldComplete(activeRequest.ShapeWorldKey, progress.ShapeWorldProgress);
        }

        public ShapeLevelNextWorldAvailability GetNextWorldAvailability(
            LevelSessionRequest activeRequest,
            bool wasPassed,
            PlayerRecordSaveData progress)
        {
            if (!wasPassed || !IsCompletedFinalLevel(activeRequest, progress)) return default;

            var nextWorldOrder = _catalogService.GetWorldOrder(activeRequest.ShapeWorldKey) + 1;
            if (!_catalogService.TryGetWorldAtOrder(nextWorldOrder, out var nextWorld)) return default;

            var isUnlocked = progress.ShapeWorldProgress != null &&
                             progress.ShapeWorldProgress.TryGetValue(nextWorld.WorldKey, out var nextProgress) &&
                             nextProgress.IsUnlocked;
            var hasFirstLevel = _catalogService.TryGetFirstLevel(nextWorld.WorldKey, out _, out _);
            return new ShapeLevelNextWorldAvailability(true, isUnlocked && hasFirstLevel);
        }

        private bool TryResolveNextWorld(
            LevelSessionRequest activeRequest,
            PlayerRecordSaveData progress,
            out ShapeLevelResultOutcome outcome)
        {
            outcome = new ShapeLevelResultOutcome(ShapeLevelResultAction.None, default);
            var availability = GetNextWorldAvailability(activeRequest, wasPassed: true, progress);
            if (!availability.IsVisible || !availability.IsEnabled) return false;

            var nextWorldOrder = _catalogService.GetWorldOrder(activeRequest.ShapeWorldKey) + 1;
            if (!_catalogService.TryGetWorldAtOrder(nextWorldOrder, out var nextWorld) ||
                !_catalogService.TryGetFirstLevel(nextWorld.WorldKey, out var firstLevelId, out var firstLevel))
                return false;

            outcome = new ShapeLevelResultOutcome(
                ShapeLevelResultAction.NextWorld,
                LevelSessionRequest.ForShapeLevel(nextWorld.WorldKey, firstLevelId, firstLevel));
            return true;
        }

        private static bool TryGetActiveOrder(ShapeLevelWorldCatalogService catalogService, LevelSessionRequest request, out int order)
        {
            order = 0;
            if (request.GameMode != FitMe.Shared.GameMode.LevelShape ||
                string.IsNullOrWhiteSpace(request.ShapeLevelId) || !request.ShapeLevel ||
                !catalogService.TryGetLevel(request.ShapeWorldKey, request.ShapeLevelId, out var catalogLevel) ||
                catalogLevel != request.ShapeLevel)
                return false;

            order = catalogService.GetLevelOrder(request.ShapeWorldKey, request.ShapeLevelId);
            return order > 0;
        }
    }
}
