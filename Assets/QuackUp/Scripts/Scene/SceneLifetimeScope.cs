using System.Collections.Generic;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using VContainer;
using VContainer.Unity;

namespace FitMe.Scene
{
    [ShowOdinSerializedPropertiesInInspector]
    public class SceneLifetimeScope : SerializedLifetimeScope
    {
        [OdinSerialize] private List<IInstaller> installers = new();
        [OdinSerialize] private List<IInstaller> uiInstallers = new();
        
        protected override void Configure(IContainerBuilder builder)
        {
            foreach (var installer in installers)
            {
                installer.Install(builder);
            }
            foreach (var uiInstaller in uiInstallers)
            {
                uiInstaller.Install(builder);
            }
        }
    }
}