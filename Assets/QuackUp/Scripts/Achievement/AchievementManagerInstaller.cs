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
        [SerializeField] private List<AchievementPreset> achievementPresets;
        
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(achievementPresets).As<IReadOnlyList<AchievementPreset>>();
            builder.Register<AchievementManager>(Lifetime.Singleton);
            builder.RegisterBuildCallback(x =>
            {
                foreach (var preset in achievementPresets)
                {
                    x.Inject(preset);
                }
                x.Resolve<AchievementManager>();
            });
        }
    }
}