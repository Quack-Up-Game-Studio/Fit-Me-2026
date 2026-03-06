using System;
using System.Collections.Generic;
using QuackUp.Input;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Panel
{
    [ShowOdinSerializedPropertiesInInspector]
    public abstract class PanelLifetimeScope : SerializedLifetimeScope
    {
        [SerializeField] private bool hasNestedPanelManager;
        [OdinSerialize, ShowIf(nameof(hasNestedPanelManager))] private PanelManagerInstaller panelManagerInstaller;
        [OdinSerialize] private List<IInstaller> additionalInstallers = new();
    
        public abstract IPanelViewModel CreatPanel();
        
        public void Initialize()
        {
            Awake();
        }
        
        protected override void Configure(IContainerBuilder builder)
        {
            if (hasNestedPanelManager)
            {
                panelManagerInstaller.IsNested = true;
                panelManagerInstaller.Install(builder);
            }
            foreach (var installer in additionalInstallers)
            {
                installer.Install(builder);
            }
            builder.RegisterBuildCallback(_ =>
            {
            });
            builder.RegisterDisposeCallback(_ =>
            {
            });
        }
    }
}