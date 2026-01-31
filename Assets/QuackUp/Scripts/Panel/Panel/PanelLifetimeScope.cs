using QuackUp.Input;
using QuackUp.Utils;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Panel
{
    public abstract class PanelLifetimeScope : SerializedLifetimeScope
    {
        [SerializeReference] private PanelManagerInstaller panelManagerInstaller;
        protected PanelManager ParentPanelManager;
        public abstract IPanelViewModel CreatPanel();
        
        public void Initialize()
        {
            Awake();
        }
        
        protected override void Configure(IContainerBuilder builder)
        {
            panelManagerInstaller?.Install(builder);
            builder.RegisterBuildCallback(x =>
            {
                ParentPanelManager = x.Resolve<PanelManager>();
            });
        }
    }
}