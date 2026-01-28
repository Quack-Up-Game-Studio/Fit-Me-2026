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
            builder.Register<PanelManager>(Lifetime.Scoped);
            builder.RegisterBuildCallback(_ =>
            {
                panelLifetimeScopes.Values.ForEach(x =>
                {
                    x.parentReference.Object = parentLifetimeScope;
                    x.Build();
                });
            });
        }
    }
}