using System;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using VContainer;
using VContainer.Unity;

namespace QuackUp.IAP
{
    [Serializable]
    public class InAppPurchaseInstaller : IInstaller
    {
        [ShowInInspector] private InspectorPlaceholder _title;
        public void Install(IContainerBuilder builder)
        {
            builder.Register<InAppPurchaseManager>(Lifetime.Singleton)
                .AsSelf()
                .As<IStartable>();
        }
    }
}
