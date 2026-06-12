using UnityEngine;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace FitMe.GameAnalytics
{
    public class GameAnalyticsInitializer : IPostInitializable
    {
        private readonly GameObject _gameAnalyticsObject;
        
        [Inject]
        public GameAnalyticsInitializer(GameObject gameAnalyticsObject)
        {
            _gameAnalyticsObject = gameAnalyticsObject;
        }
        
        public void PostInitialize()
        {
            Object.Instantiate(_gameAnalyticsObject);
            GameAnalyticsSDK.GameAnalytics.Initialize();
        }
    }
}
