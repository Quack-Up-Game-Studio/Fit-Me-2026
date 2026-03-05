using System;
using System.Collections.Generic;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Achievement
{
    [Serializable]
    public class AchievementManagerInstaller : IInstaller
    {
        [ShowInInspector] private InspectorPlaceholder _title;
        [SerializeField] private AchievementManagerConfig config;
        
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(config);
            builder.RegisterEntryPoint<AchievementManager>().AsSelf();
            builder.RegisterBuildCallback(x =>
            {
                foreach (var preset in config.AchievementPresets)
                {
                    x.Inject(preset);
                }
                x.Resolve<AchievementManager>();
            });
        }
    }
}