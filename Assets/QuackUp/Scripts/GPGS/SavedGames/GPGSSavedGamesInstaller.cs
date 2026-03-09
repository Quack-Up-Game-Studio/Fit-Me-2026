using System;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace QuackUp.GPGS
{
    [Serializable]
    public class GPGSSavedGamesInstaller : IInstaller
    {
        [ShowInInspector] private InspectorPlaceholder _title;
        [SerializeField] private GPGSSavedGamesConfig config;
        
        public void Install(IContainerBuilder builder)
        {
#if UNITY_ANDROID
            builder.RegisterInstance(config);
            builder.Register<GPGSSavedGames>(Lifetime.Singleton);
#endif
        }
    }
}