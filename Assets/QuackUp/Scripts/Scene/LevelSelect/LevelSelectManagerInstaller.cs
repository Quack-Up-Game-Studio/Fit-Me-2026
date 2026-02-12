using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Scene
{
    public class LevelSelectManagerInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<LevelSelectManager>().AsSelf();
            builder.RegisterBuildCallback(x =>
            {
                x.Resolve<LevelSelectManager>();
            });
        }
    }
}
