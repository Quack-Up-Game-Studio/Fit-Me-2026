using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Panel
{
    public class ChallengePanelLifetimeScope : PanelLifetimeScope
    {
        [SerializeField] private ChallengePanelView challengePanelView;

        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);
            builder.Register<ChallengePanelViewModel>(Lifetime.Singleton).As<IPanelViewModel>();
            builder.RegisterComponent(challengePanelView).As<IPanelView>();
        }

        public override IPanelViewModel CreatPanel()
        {
            return Container.Resolve<IPanelViewModel>();
        }
    }
}