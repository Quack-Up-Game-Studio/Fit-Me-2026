using FitMe.Grid;
using MessagePipe;
using QuackUp.SceneManagement;
using QuackUp.Utils;
using VContainer;

namespace FitMe.Scene.MainMenu
{
    public class MainMenuManagerMessageHub : MessageHub
    {
        public const string MainMenuManagerMessageHubKey = "MainMenuManagerMessageHub";
        
        [Inject]
        public MainMenuManagerMessageHub(
            IPublisher<StartSpawnEvent> startSpawnPublisher,
            ISubscriber<LoadSceneStageEvent> loadSceneStageSubscriber)
        {
            MessageWrappers[typeof(StartSpawnEvent)] = new MessageWrapper<StartSpawnEvent>(
                startSpawnPublisher,
                null);
            MessageWrappers[typeof(LoadSceneStageEvent)] = new MessageWrapper<LoadSceneStageEvent>(
                null,
                loadSceneStageSubscriber);
        }
    }
}