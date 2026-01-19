using System.Collections.Generic;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using VContainer;
using VContainer.Unity;

namespace FitMe.Grid
{
    [ShowOdinSerializedPropertiesInInspector]
    public class GridLifetimeScope : SerializedLifetimeScope
    {
        [OdinSerialize] private List<IInstaller> installers;
        
        protected override void Configure(IContainerBuilder builder)
        {
            installers.ForEach(x => x.Install(builder));
        }
    }
}