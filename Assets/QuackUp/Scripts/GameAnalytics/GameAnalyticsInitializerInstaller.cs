using System;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using QuackUp.Utils;
using QuackUp.Analytics;

namespace FitMe.GameAnalytics
{
    [Serializable]
    public class GameAnalyticsInitializerInstaller : IInstaller
    {
        [ShowInInspector] private InspectorPlaceholder _title;
        [SerializeField] private GameObject gameAnalyticsObject;
        
        public void Install(IContainerBuilder builder)
        {
            builder.Register<IAnalyticsService, GameAnalyticsAnalyticsService>(Lifetime.Singleton);
            builder.RegisterEntryPoint(_ => new GameAnalyticsInitializer(gameAnalyticsObject),
                    Lifetime.Singleton)
                .AsSelf();
        }
    }
}
