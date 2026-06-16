using MessagePipe;
using QuackUp.Utils;
using VContainer;

namespace FitMe.Panel
{
    public struct SplashScreenFinishedEvent { }

    public class SplashScreenMessageHub : MessageHub
    {
        [Inject]
        public SplashScreenMessageHub(
            IPublisher<SplashScreenFinishedEvent> finishedPublisher,
            ISubscriber<SplashScreenFinishedEvent> finishedSubscriber)
        {
            MessageWrappers[typeof(SplashScreenFinishedEvent)] = new MessageWrapper<SplashScreenFinishedEvent>(
                finishedPublisher,
                finishedSubscriber);
        }
    }
}
