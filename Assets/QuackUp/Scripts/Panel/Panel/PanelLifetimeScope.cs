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
        [SerializeReference] private PanelManagerInstaller panelManagerInstaller;
        [OdinSerialize] private List<IInstaller> additionalInstallers = new();
        protected PanelManager ParentPanelManager;
        public abstract IPanelViewModel CreatPanel();
        
        public void Initialize()
        {
            Awake();
        }
        
        protected override void Configure(IContainerBuilder builder)
        {
            panelManagerInstaller?.Install(builder);
            foreach (var installer in additionalInstallers)
            {
                installer.Install(builder);
            }
            builder.RegisterBuildCallback(x =>
            {
                ParentPanelManager = x.Resolve<PanelManager>();
            });
        }
    }
}