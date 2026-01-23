using System;
using System.Collections.Generic;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Entity
{
    [Serializable]
    public class EntityManagerInstaller : IInstaller
    {
        [ShowInInspector] private InspectorPlaceholder _title;
        [SerializeField] private EntityManagerConfig entityManagerConfig;
        [OdinSerialize] private List<IEntityLifetimeScope> sceneEntity = new();
        
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(entityManagerConfig);
            builder.RegisterInstance(sceneEntity);
            builder.Register<EntityManager>(Lifetime.Singleton);
        }
    }
}