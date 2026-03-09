using System;
using System.Collections.Generic;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace QuackUp.GPGS
{
    [ShowOdinSerializedPropertiesInInspector]
    [Serializable]
    public class GPGSInstaller : IInstaller
    {
        [ShowInInspector] private InspectorPlaceholder _title;
        [SerializeField] private GPGSAuthenticationManagerConfig config;
        [OdinSerialize] private List<IInstaller> services;
        
        public void Install(IContainerBuilder builder)
        {
#if UNITY_ANDROID
            builder.RegisterInstance(config);
            builder.RegisterEntryPoint<GPGSAuthenticationManager>().AsSelf();
            foreach (var installer in services)
            {
                installer.Install(builder);
            }
#endif
        }
    }
}