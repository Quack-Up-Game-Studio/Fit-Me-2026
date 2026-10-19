using System.Collections.Generic;
using FitMe.GameData;

namespace FitMe.Scene
{
    public static class ShapeWorldProgressRules
    {

        public static void InitializeUnlocks(
            ShapeLevelWorldCatalogService catalogService,
            IDictionary<string, ShapeWorldProgressData> progress)
        {
            if (catalogService == null || !catalogService.IsValid || progress == null) return;

            var worlds = catalogService.Worlds;
            for (var index = 0; index < worlds.Count; index++)
            {
                var world = worlds[index];
                if (!progress.TryGetValue(world.WorldKey, out var worldProgress))
                {
                    worldProgress = new ShapeWorldProgressData(world.WorldKey, index == 0);
                    progress[world.WorldKey] = worldProgress;
                }
                if (index == 0) worldProgress.Unlock();
                if (index > 0 && catalogService.IsWorldComplete(worlds[index - 1].WorldKey, progress))
                    worldProgress.Unlock();
            }
        }

        public static void RecordPassAndUnlockNext(
            ShapeLevelWorldCatalogService catalogService,
            IDictionary<string, ShapeWorldProgressData> progress,
            string worldKey,
            string levelId)
        {
            if (catalogService == null || !catalogService.IsValid || progress == null ||
                !catalogService.TryGetLevel(worldKey, levelId, out _) ||
                !progress.TryGetValue(worldKey, out var current) || !current.IsUnlocked)
                return;

            current.RecordLevelPass(levelId);
            if (!catalogService.IsWorldComplete(worldKey, progress)) return;
            var nextOrder = catalogService.GetWorldOrder(worldKey) + 1;
            if (catalogService.TryGetWorldAtOrder(nextOrder, out var next) && progress.TryGetValue(next.WorldKey, out var nextProgress))
                nextProgress.Unlock();
        }
    }
}
