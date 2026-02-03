using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Panel
{
    public class GameOverPanelLifeTimeScope : PanelLifetimeScope
    {
        [SerializeField] private GameOverPanelView _gameOverPanelView;
        
        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);
            builder.RegisterComponent(_gameOverPanelView).AsSelf().As<IPanelView>();
            builder.Register<GameOverPanelViewModel>(Lifetime.Singleton).AsSelf().As<IPanelViewModel>();
        }
        
        public override IPanelViewModel CreatPanel()
        {
            return Container.Resolve<GameOverPanelViewModel>();
        }
    }
}
