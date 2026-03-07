using System;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using VContainer;
using VContainer.Unity;

namespace QuackUp.GPGS
{
    [Serializable]
    public class GPGSLeaderboardInstaller : IInstaller
    {
        [ShowInInspector] private InspectorPlaceholder _title;
        
        public void Install(IContainerBuilder builder)
        {
            builder.Register<GPGSLeaderboard>(Lifetime.Singleton).AsSelf();
        }
    }
}