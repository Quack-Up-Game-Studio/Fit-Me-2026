using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Panel
{
    public class MainMenuPanelLifetimeScope : PanelLifetimeScope
    {
        [SerializeField] private MainMenuPanelView mainMenuPanelView;
        
        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);
            builder.RegisterComponent(mainMenuPanelView).AsSelf().As<IPanelView>();
            builder.Register<MainMenuPanelViewModel>(Lifetime.Singleton).As<IPanelViewModel>();
        }

        public override IPanelViewModel CreatPanel()
        {
            return Container.Resolve<IPanelViewModel>();
        }
    }
}