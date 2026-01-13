using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace QuackUp.Samples
{
    [Serializable]
    public class HealthUIInstaller : IInstaller
    {
        [SerializeField] private HealthUIView view;
        [SerializeField] private HealthUIConfig config;
        
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterComponent(view);
            builder.RegisterInstance(config);
            builder.Register<HealthUIViewModel>(Lifetime.Scoped);
            builder.Register<HealthUIModel>(Lifetime.Singleton);
        }
    }
}