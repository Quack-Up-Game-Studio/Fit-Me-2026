using System;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Panel
{
    [Serializable]
    public class RunTimerInstaller : IInstaller
    {
        [ShowInInspector] private InspectorPlaceholder _placeholder;
        [SerializeField] private RunTimerView view;
        
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterComponent(view).AsSelf();
            builder.Register<RunTimerViewModel>(Lifetime.Singleton).AsSelf();
            builder.RegisterBuildCallback(x =>
            {
                x.Resolve<RunTimerViewModel>();
            });
        }
    }
}