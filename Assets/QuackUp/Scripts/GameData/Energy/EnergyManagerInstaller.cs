using System;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.GameData
{
    [Serializable]
    public class EnergyManagerDebugData : DebugDataBase
    {
        [SerializeField] private EnergyManager manager;
        
        public EnergyManagerDebugData(EnergyManager manager)
        {
            this.manager = manager;
        }
    }
    
    [Serializable]
    public class EnergyManagerInstaller : DebugableInstaller<EnergyManagerDebugData>
    {
        [ShowInInspector] private InspectorPlaceholder _title;
        [SerializeField] private EnergyManagerConfig config;
        
        public override void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(config);
            builder.RegisterEntryPoint<EnergyManager>().AsSelf().As<QuackUp.Notification.IEnergyProvider>();
            builder.RegisterBuildCallback(x =>
            {
                var manager = x.Resolve<EnergyManager>();
                DebugData = new(manager);
            });
        }
    }
}