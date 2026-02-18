using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Scene
{
    public class ModeSelectManagerInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<ModeSelectManager>().AsSelf();
            builder.RegisterBuildCallback(x =>
            {
                x.Resolve<ModeSelectManager>();
            });
        }
    }
}
