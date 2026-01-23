using System;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Scene
{
    [Serializable]
    public class LevelManagerInstaller : IInstaller
    {
        [ShowInInspector] private InspectorPlaceholder _title;
        [SerializeField] private LevelManagerConfig levelManagerConfig;
        
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(levelManagerConfig);
            builder.RegisterEntryPoint<LevelManager>(Lifetime.Singleton).As<LevelManager>();
            builder.Register<IMessageHub, LevelManagerMessageHub>(Lifetime.Singleton)
                .Keyed(LevelManagerMessageHub.MessageHubKey);
        }
    }
}