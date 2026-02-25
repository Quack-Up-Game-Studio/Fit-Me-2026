using System;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using VContainer;
using VContainer.Unity;

namespace FitMe.GameData
{
    [Serializable]
    public class PlaytimeTrackerInstaller : IInstaller
    {
        [ShowInInspector] private InspectorPlaceholder _title;

        public void Install(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<PlaytimeTracker>().AsSelf();
            builder.RegisterBuildCallback(x =>
            {
                x.Resolve<PlaytimeTracker>();
            });
        }
    }
}