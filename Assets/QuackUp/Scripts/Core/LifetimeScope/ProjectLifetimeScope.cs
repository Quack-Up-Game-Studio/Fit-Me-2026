using System.Collections.Generic;
using MessagePipe;
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
        
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterMessagePipe(options =>
            {
                options.InstanceLifetime = InstanceLifetime.Singleton;
            });
            installers.ForEach(installer => installer.Install(builder));
            builder.RegisterBuildCallback(x => GlobalMessagePipe.SetProvider(x.AsServiceProvider()));
        }
    }
}