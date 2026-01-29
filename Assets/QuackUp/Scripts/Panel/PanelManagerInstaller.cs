using System;
using System.Collections.Generic;
using QuackUp.Utils;
using Redcode.Extensions;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Panel
{
    [Serializable]
    public class PanelManagerInstaller : IInstaller
    {
        [ShowInInspector] private InspectorPlaceholder _title;
        [SerializeField] private LifetimeScope parentLifetimeScope;
        [OdinSerialize] private Dictionary<string, PanelLifetimeScope> panelLifetimeScopes = new();
        [SerializeField] private string startupPanelId;
        
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(panelLifetimeScopes).As<IReadOnlyDictionary<string, PanelLifetimeScope>>();
            builder.RegisterInstance(startupPanelId);
            builder.Register<PanelManager>(Lifetime.Singleton);
            builder.RegisterBuildCallback(c =>
            {
                panelLifetimeScopes.Values.ForEach(x =>
                {
                    x.gameObject.SetActive(true);
                    x.parentReference.Object = parentLifetimeScope;
                    x.Build();
                });
                c.Resolve<PanelManager>().Initialize();
            });
        }
    }
}