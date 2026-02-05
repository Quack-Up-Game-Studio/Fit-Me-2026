using FitMe.Grid;
using FitMe.Shared;
using MessagePipe;
using QuackUp.SceneManagement;
using QuackUp.Utils;
using VContainer;

namespace FitMe.Scene
{
    public class LevelManagerMessageHub : MessageHub
    {
        public const string MessageHubKey = "LevelManagerMessageHub";
        
        [Inject]
        public LevelManagerMessageHub(
            IPublisher<StartSpawnEvent> startSpawnPublisher,
            ISubscriber<LoadSceneStageEvent> loadSceneStageSubscriber)
            ISubscriber<GameOverEvent> gameOverSubscriber)
        {
            MessageWrappers[typeof(StartSpawnEvent)] = new MessageWrapper<StartSpawnEvent>(
                startSpawnPublisher,
                null);
            MessageWrappers[typeof(LoadSceneStageEvent)] = new MessageWrapper<LoadSceneStageEvent>(
                null,
                loadSceneStageSubscriber);
            MessageWrappers[typeof(GameOverEvent)] = new MessageWrapper<GameOverEvent>(
                null,
                gameOverSubscriber);
        }
    }
}