using System.Collections.Generic;
using MessagePipe;
using QuackUp.Notification;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace QuackUp.Core
{
    [ShowOdinSerializedPropertiesInInspector]
    public class ProjectLifetimeScope : SerializedLifetimeScope
    {
        [Title("Installers")]   
        [HideReferenceObjectPicker]
        [OdinSerialize] private List<IInstaller> installers;
        [OdinSerialize] private Dictionary<RuntimePlatform, PlatformSpecificInstallerPreset> platformSpecificInstallers = new();
        
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterMessagePipe(options =>
            {
                options.InstanceLifetime = InstanceLifetime.Singleton;
            });
            foreach (var installer in installers)
            {
                installer.Install(builder);
            }
            new NotificationOutsideInstaller().Install(builder);
            if (platformSpecificInstallers.TryGetValue(Application.platform, out var platformInstallers))
            {
                foreach (var installer in platformInstallers.PlatformSpecificInstallers)
                {
                    installer.Install(builder);
                }
            }
            builder.RegisterBuildCallback(x => GlobalMessagePipe.SetProvider(x.AsServiceProvider()));
        }
    }
}