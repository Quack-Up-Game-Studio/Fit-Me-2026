using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Panel
{
    public class ResultPanelLifeTimeScope : PanelLifetimeScope
    {
        [SerializeField] private ResultPanelView _resultPanelView;
        
        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);
            builder.RegisterComponent(_resultPanelView).AsSelf().As<IPanelView>();
            builder.Register<ResultPanelViewModel>(Lifetime.Singleton).AsSelf().As<IPanelViewModel>();
        }
        
        public override IPanelViewModel CreatPanel()
        {
            return Container.Resolve<ResultPanelViewModel>();
        }
    }
}
