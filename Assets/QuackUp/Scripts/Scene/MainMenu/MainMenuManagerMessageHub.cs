using FitMe.Grid;
using MessagePipe;
using QuackUp.Utils;
using VContainer;

namespace FitMe.Scene.MainMenu
{
    public class MainMenuManagerMessageHub : MessageHub
    {
        public const string MainMenuManagerMessageHubKey = "MainMenuManagerMessageHub";
        
        [Inject]
        public MainMenuManagerMessageHub(
            IPublisher<StartSpawnEvent> startSpawnPublisher)
        {
            MessageWrappers[typeof(StartSpawnEvent)] = new MessageWrapper<StartSpawnEvent>(
                startSpawnPublisher,
                null);
        }
    }
}