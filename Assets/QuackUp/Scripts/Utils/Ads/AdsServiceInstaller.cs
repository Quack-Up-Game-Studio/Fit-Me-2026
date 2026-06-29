using System;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace QuackUp.Utils
{
    [Serializable]
    public class AdsServiceInstaller : IInstaller
    {
        [ShowInInspector] private InspectorPlaceholder _title;

        [SerializeField] private AdsSettings _adsSettings;
        
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<AdsService>().AsSelf();
            builder.RegisterComponent(_adsSettings);
        }
    }
}