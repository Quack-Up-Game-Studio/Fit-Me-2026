using FitMe.Grid;
using FitMe.Shared;
using MessagePipe;
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
            ISubscriber<GameOverEvent> gameOverSubscriber)
        {
            MessageWrappers[typeof(StartSpawnEvent)] = new MessageWrapper<StartSpawnEvent>(
                startSpawnPublisher,
                null);
            MessageWrappers[typeof(GameOverEvent)] = new MessageWrapper<GameOverEvent>(
                null,
                gameOverSubscriber);
        }
    }
}