using System;
using System.Collections.Generic;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace QuackUp.iOSSocial
{
    [ShowOdinSerializedPropertiesInInspector]
    [Serializable]
    public class iOSSocialInstaller : IInstaller
    {
        [ShowInInspector] private InspectorPlaceholder _title;
        [SerializeField] private iOSSocialConfig config;
        [OdinSerialize] private List<IInstaller> services = new();

        public void Install(IContainerBuilder builder)
        {
#if UNITY_IOS
            builder.RegisterInstance(config);
            builder.Register<iOSAuthenticationService>(Lifetime.Scoped).AsImplementedInterfaces();
            builder.Register<iOSLeaderboardService>(Lifetime.Scoped).AsImplementedInterfaces();
            foreach (var installer in services)
            {
                installer.Install(builder);
            }
#endif
        }
    }

    [CreateAssetMenu(fileName = "iOSSocialConfig", menuName = "QuackUp/iOS/iOSSocialConfig")]
    public class iOSSocialConfig : ScriptableObject
    {
        public string leaderboardId = "com.yourcompany.fitme.leaderboard";
    }
}
