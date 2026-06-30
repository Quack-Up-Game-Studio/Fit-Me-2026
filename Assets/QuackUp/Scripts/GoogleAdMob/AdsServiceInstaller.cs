using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using VContainer;
using VContainer.Unity;

[assembly: Sirenix.Serialization.BindTypeNameToType("QuackUp.Utils.AdsServiceInstaller, QuackUp.Utils", typeof(QuackUp.GoogleAdMob.AdsServiceInstaller))]

namespace QuackUp.GoogleAdMob
{
    [MovedFrom("QuackUp.Utils")]
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