using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Panel
{
    public class LeaderboardTabLifetimeScope : PanelLifetimeScope
    {
        [SerializeField] private LeaderboardTabView leaderboardTabView;

        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);
            builder.RegisterComponent(leaderboardTabView).AsSelf().As<IPanelView>();
            builder.Register<LeaderboardTabViewModel>(Lifetime.Singleton).As<IPanelViewModel>();
        }
        public override IPanelViewModel CreatPanel()
        {
            return Container.Resolve<IPanelViewModel>();
        }
    }
}