using FitMe.Shared;
using MessagePipe;
using QuackUp.SceneManagement;
using QuackUp.Utils;
using VContainer;

namespace FitMe.Grid
{
    public struct SpawnWithBlockPresetEvent
    {
        public readonly BlockPreset BlockPreset;
        public readonly bool AllowRotation;
        
        public SpawnWithBlockPresetEvent(BlockPreset blockPreset, bool allowRotation = true)
        {
            AllowRotation = allowRotation;
            BlockPreset = blockPreset;
        }
    }
    
    public struct SpawnWithGridPresetEvent
    {
        public readonly GridPreset GridPreset;
        
        public SpawnWithGridPresetEvent(GridPreset gridPreset)
        {
            GridPreset = gridPreset;
        }
    }

    public struct StartCreateGridEvent
    {
    }

    public class GridManagerMessageHub : MessageHub
    {
        public const string GridManagerMessageHubKey = "GridManagerMessageHub";
        
        [Inject]
        public GridManagerMessageHub(
            ISubscriber<LoadSceneStageEvent> loadSceneStageSubscriber,
            ISubscriber<SpawnWithBlockPresetEvent> startSpawnSubscriber,
            ISubscriber<SpawnWithGridPresetEvent> startGridSpawnSubscriber,
            ISubscriber<StartCreateGridEvent> startCreateGridSubscriber,
            ISubscriber<ClearGridEvent> clearGridSubscriber)
        {
            MessageWrappers[typeof(LoadSceneStageEvent)] = new MessageWrapper<LoadSceneStageEvent>(
                null,
                loadSceneStageSubscriber);
            MessageWrappers[typeof(SpawnWithBlockPresetEvent)] = new MessageWrapper<SpawnWithBlockPresetEvent>(
                null,
                startSpawnSubscriber);
            MessageWrappers[typeof(SpawnWithGridPresetEvent)] = new MessageWrapper<SpawnWithGridPresetEvent>(
                null,
                startGridSpawnSubscriber);
            MessageWrappers[typeof(StartCreateGridEvent)] = new MessageWrapper<StartCreateGridEvent>(
                null,
                startCreateGridSubscriber);
            MessageWrappers[typeof(ClearGridEvent)] = new MessageWrapper<ClearGridEvent>(
                null,
                clearGridSubscriber);
        }
    }
}