using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Panel
{
    public class LeaderboardPanelLifetimeScope : PanelLifetimeScope
    {
        [SerializeField] private LeaderboardPanelView leaderboardPanelView;
        
        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);
            builder.RegisterComponent(leaderboardPanelView).AsSelf().As<IPanelView>();
            builder.Register<LeaderboardPanelViewModel>(Lifetime.Singleton).As<IPanelViewModel>();
        }
        
        public override IPanelViewModel CreatPanel()
        {
            return Container.Resolve<IPanelViewModel>();
        }
    }
}