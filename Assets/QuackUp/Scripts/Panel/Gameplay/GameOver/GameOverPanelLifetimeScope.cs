using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Panel
{
    public class GameOverPanelLifeTimeScope : PanelLifetimeScope
    {
        [SerializeField] private GameOverPanelView gameOverPanelView;
        [SerializeField] private int maxContinueCount;
        [SerializeField] private float countdownTime;
        
        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);
            builder.RegisterComponent(gameOverPanelView).AsSelf().As<IPanelView>();
            builder.Register<GameOverPanelViewModel>(Lifetime.Singleton).AsSelf().As<IPanelViewModel>();
            builder.RegisterInstance(maxContinueCount).As<int>().Keyed(GameOverPanelViewModel.MaxContinueCountId);
            builder.RegisterInstance(countdownTime).As<float>().Keyed(GameOverPanelViewModel.CountdownTimeId);
        }
        
        public override IPanelViewModel CreatPanel()
        {
            return Container.Resolve<GameOverPanelViewModel>();
        }
    }
}
