using System;
using FitMe.GameData;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UniLabs.Time;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Panel
{
    [Serializable]
    [ShowOdinSerializedPropertiesInInspector]
    public class OutOfEnergyDebugData : DebugDataBase
    {
        [OdinSerialize] private OutOfEnergyManager _manager;
        
        public OutOfEnergyDebugData(OutOfEnergyManager manager)
        {
            _manager = manager;
        }
    }
    
    [Serializable]
    public class OutOfEnergyInstaller : DebugableInstaller<OutOfEnergyDebugData>
    {
        [ShowInInspector] private InspectorPlaceholder _title;
        [SerializeField] private OutOfEnergyView outOfEnergyView;
        [SerializeField, TimeSpanDrawerSettings(TimeUnit.Minutes)] private UTimeSpan adCooldown;
        [SerializeField] private int adCount;
        
        public override void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance<TimeSpan>(adCooldown).Keyed(OutOfEnergyManager.AdCoolDownTimeKey);
            builder.RegisterInstance(adCount).Keyed(OutOfEnergyManager.AdCountKey);
            builder.RegisterEntryPoint<OutOfEnergyManager>().AsSelf();
            builder.Register<OutOfEnergyViewModel>(Lifetime.Singleton);
            builder.RegisterComponent(outOfEnergyView).AsSelf();
            builder.RegisterBuildCallback(x =>
            {
                x.Resolve<OutOfEnergyViewModel>();
                var manager = x.Resolve<OutOfEnergyManager>();
                DebugData = new OutOfEnergyDebugData(manager);
            });
        }
    }
}