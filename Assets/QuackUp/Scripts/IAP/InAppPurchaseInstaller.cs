using System;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using VContainer;
using VContainer.Unity;

namespace QuackUp.IAP
{
    [Serializable]
    [ShowOdinSerializedPropertiesInInspector]
    public class InAppPurchaseManagerDebugData : DebugDataBase
    {
        [OdinSerialize] private InAppPurchaseManager _inAppPurchaseManager;

        public InAppPurchaseManagerDebugData(InAppPurchaseManager inAppPurchaseManager)
        { 
            _inAppPurchaseManager = inAppPurchaseManager;
        }
    }
    
    [Serializable]
    public class InAppPurchaseInstaller : DebugableInstaller<InAppPurchaseManagerDebugData>
    {
        [ShowInInspector] private InspectorPlaceholder _title;
        public override void Install(IContainerBuilder builder)
        {
            builder.Register<InAppPurchaseManager>(Lifetime.Singleton)
                .AsSelf()
                .As<IStartable>();
            builder.RegisterBuildCallback(x =>
            {
                var inAppPurchaseManager = x.Resolve<InAppPurchaseManager>();
                DebugData = new InAppPurchaseManagerDebugData(inAppPurchaseManager);
            });
        }
    }
}
