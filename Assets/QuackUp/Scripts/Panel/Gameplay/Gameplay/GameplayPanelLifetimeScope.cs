using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Panel
{
    public class GameplayPanelLifetimeScope : PanelLifetimeScope
    {
        [SerializeField] private GameplayPanelView panelView;

        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);
            builder.RegisterComponent(panelView).AsSelf().As<IPanelView>();
            builder.Register<GameplayPanelViewModel>(Lifetime.Singleton).AsSelf().As<IPanelViewModel>();
        }

        public override IPanelViewModel CreatPanel()
        {
            return Container.Resolve<GameplayPanelViewModel>();
        }
    }
}