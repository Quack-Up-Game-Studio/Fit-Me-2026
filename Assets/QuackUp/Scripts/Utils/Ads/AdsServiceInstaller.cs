using System;
using VContainer;
using VContainer.Unity;

namespace QuackUp.Utils
{
    [Serializable]
    public class AdsServiceInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<AdsService>().AsSelf();
        }
    }
}