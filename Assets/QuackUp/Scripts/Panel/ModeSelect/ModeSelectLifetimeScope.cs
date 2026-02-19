using System;
using FitMe.GameData;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Panel
{
    [Serializable]
    public class ModeSelectLifetimeScope : PanelLifetimeScope
    {
        [SerializeField] private ModeSelectView panelView;
        
        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);
            builder.RegisterComponent(panelView).AsSelf().As<IPanelView>();

            builder.Register<ModeSelectViewModel>(Lifetime.Singleton)
                .AsSelf().As<IPanelViewModel>();
        }
        
        public override IPanelViewModel CreatPanel()
        {
            return Container.Resolve<ModeSelectViewModel>();
        }
    }
}
