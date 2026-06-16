using System;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using VContainer;
using VContainer.Unity;

namespace QuackUp.Notification
{
    [Serializable]
    public class NotificationOutsideInstaller : IInstaller
    {
        [ShowInInspector] private InspectorPlaceholder _title;

        public void Install(IContainerBuilder builder)
        {
            builder.Register<NotificationService>(Lifetime.Singleton);
            builder.RegisterEntryPoint<NotificationLifecycleManager>().AsSelf();
        }
    }
}
