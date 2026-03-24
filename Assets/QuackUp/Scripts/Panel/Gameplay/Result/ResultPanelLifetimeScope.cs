using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Panel
{
    public class ResultPanelLifeTimeScope : PanelLifetimeScope
    {
        [SerializeField] private ResultPanelView _resultPanelView;
        [SerializeField] private int forcedAdsFitMeThreshold = 13;
        
        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);
            builder.RegisterInstance(forcedAdsFitMeThreshold).AsSelf().Keyed(ResultPanelViewModel.ForceAdsThresholdKey);
            builder.RegisterComponent(_resultPanelView).AsSelf().As<IPanelView>();
            builder.Register<ResultPanelViewModel>(Lifetime.Singleton).AsSelf().As<IPanelViewModel>();
        }
        
        public override IPanelViewModel CreatPanel()
        {
            return Container.Resolve<ResultPanelViewModel>();
        }
    }
}
