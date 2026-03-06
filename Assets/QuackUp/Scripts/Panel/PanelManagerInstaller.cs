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
        
        public bool IsNested { get; set; }
        public const string NestedPanelId = "Nested";
        
        public void Install(IContainerBuilder builder)
        {
            if (!IsNested)
            {
                builder.Register(_ => new PanelManager(panelLifetimeScopes, startupPanelId), Lifetime.Singleton).AsSelf();
            }
            else
            {
                builder.Register(_ => new PanelManager(panelLifetimeScopes, startupPanelId), Lifetime.Singleton).AsSelf()
                    .Keyed(NestedPanelId);
            }
            builder.RegisterBuildCallback(c =>
            {
                panelLifetimeScopes.Values.ForEach(x =>
                {
                    x.gameObject.SetActive(true);
                    x.parentReference.Object = parentLifetimeScope;
                    x.Initialize();
                    x.Build();
                });
                var panelManager = !IsNested ? c.Resolve<PanelManager>() : c.Resolve<PanelManager>(NestedPanelId);
                panelManager.Initialize();
            });
        }
    }
}