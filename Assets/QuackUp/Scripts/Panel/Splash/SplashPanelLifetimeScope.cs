using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Panel
{
    public class SplashPanelLifetimeScope : PanelLifetimeScope
    {
        [SerializeField] private SplashPanelView splashPanelView;
        
        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);
            builder.RegisterComponent(splashPanelView).AsSelf().As<IPanelView>();
            builder.Register<SplashPanelViewModel>(Lifetime.Singleton).As<IPanelViewModel>();
        }

        public override IPanelViewModel CreatPanel()
        {
            return Container.Resolve<IPanelViewModel>();
        }
    }
}
