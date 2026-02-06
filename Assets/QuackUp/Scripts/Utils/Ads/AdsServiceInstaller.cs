using System;
using Sirenix.OdinInspector;
using VContainer;
using VContainer.Unity;

namespace QuackUp.Utils
{
    [Serializable]
    public class AdsServiceInstaller : IInstaller
    {
        [ShowInInspector] private InspectorPlaceholder _title;
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<AdsService>().AsSelf();
        }
    }
}