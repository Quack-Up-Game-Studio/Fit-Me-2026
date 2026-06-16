using System;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Notification
{
    [Serializable]
    public class NotificationOutsideInstaller : IInstaller
    {
        [ShowInInspector] private InspectorPlaceholder _title;
        [SerializeField] private NotificationConfig config;

        public void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(config);
            builder.Register<NotificationService>(Lifetime.Singleton);
            builder.RegisterEntryPoint<NotificationLifecycleManager>().AsSelf();
        }
    }
}
