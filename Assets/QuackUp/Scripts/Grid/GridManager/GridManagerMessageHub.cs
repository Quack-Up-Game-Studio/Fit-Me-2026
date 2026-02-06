using FitMe.Shared;
using MessagePipe;
using QuackUp.SceneManagement;
using QuackUp.Utils;
using VContainer;

namespace FitMe.Grid
{
    public class GridManagerMessageHub : MessageHub
    {
        public const string GridManagerMessageHubKey = "GridManagerMessageHub";
        
        [Inject]
        public GridManagerMessageHub(
            ISubscriber<LoadSceneStageEvent> loadSceneStageSubscriber,
            ISubscriber<StartSpawnEvent> startSpawnSubscriber,
            ISubscriber<ClearGridEvent> clearGridSubscriber)
        {
            MessageWrappers[typeof(LoadSceneStageEvent)] = new MessageWrapper<LoadSceneStageEvent>(
                null,
                loadSceneStageSubscriber);
            MessageWrappers[typeof(StartSpawnEvent)] = new MessageWrapper<StartSpawnEvent>(
                null,
                startSpawnSubscriber);
            MessageWrappers[typeof(ClearGridEvent)] = new MessageWrapper<ClearGridEvent>(
                null,
                clearGridSubscriber);
        }
    }
}