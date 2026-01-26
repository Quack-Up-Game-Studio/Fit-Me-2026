using FitMe.Grid;
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
            IPublisher<StartSpawnEvent> startSpawnPublisher)
        {
            MessageWrappers[typeof(StartSpawnEvent)] = new MessageWrapper<StartSpawnEvent>(
                startSpawnPublisher,
                null);
        }
    }
}