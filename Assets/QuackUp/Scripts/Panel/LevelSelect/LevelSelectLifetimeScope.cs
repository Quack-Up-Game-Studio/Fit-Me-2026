using System;
using FitMe.GameData;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Panel
{
    [Serializable]
    public class LevelSelectLifetimeScope : PanelLifetimeScope
    {
        [SerializeField] private LevelSelectView panelView;
        [SerializeField] private LevelDatabase levelDatabase;
        
        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);
            builder.RegisterComponent(panelView).AsSelf().As<IPanelView>();
            
            builder.RegisterInstance(levelDatabase);
            int currentPlayerLevel = 10; ;
            
            builder.Register<LevelSelectViewModel>(Lifetime.Singleton)
                .AsSelf().As<IPanelViewModel>()
                .WithParameter("playerMaxLevel", currentPlayerLevel);
        }
        
        public override IPanelViewModel CreatPanel()
        {
            return Container.Resolve<LevelSelectViewModel>();
        }
    }
}
