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
            IPublisher<SpawnWithBlockPresetEvent> startSpawnBlockPublisher,
            IPublisher<SpawnWithGridPresetEvent> startSpawnPublisher,
            IPublisher<StartCreateGridEvent> startCreateGridPublisher,
            IPublisher<DifficultyChangeEvent> difficultyChangePublisher,
            ISubscriber<LoadSceneStageEvent> loadSceneStageSubscriber,
            ISubscriber<GameOverEvent> gameOverSubscriber,
            ISubscriber<ContinueEvent> continueSubscriber)
        {
            MessageWrappers[typeof(SpawnWithBlockPresetEvent)] = new MessageWrapper<SpawnWithBlockPresetEvent>(
                startSpawnBlockPublisher,
                null);
            MessageWrappers[typeof(SpawnWithGridPresetEvent)] = new MessageWrapper<SpawnWithGridPresetEvent>(
                startSpawnPublisher,
                null);
            MessageWrappers[typeof(StartCreateGridEvent)] = new MessageWrapper<StartCreateGridEvent>(
                startCreateGridPublisher,
                null);
            MessageWrappers[typeof(DifficultyChangeEvent)] = new MessageWrapper<DifficultyChangeEvent>(
                difficultyChangePublisher,
                null);
            MessageWrappers[typeof(LoadSceneStageEvent)] = new MessageWrapper<LoadSceneStageEvent>(
                null,
                loadSceneStageSubscriber);
            MessageWrappers[typeof(GameOverEvent)] = new MessageWrapper<GameOverEvent>(
                null,
                gameOverSubscriber);
            MessageWrappers[typeof(ContinueEvent)] = new MessageWrapper<ContinueEvent>(
                null,
                continueSubscriber);
        }
    }
}