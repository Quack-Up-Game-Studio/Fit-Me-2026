using VContainer;
using VContainer.Unity;
using UnityEngine;

namespace FitMe.Panel
{
    public class UniversalSettingsPanelLifetimeScope : PanelLifetimeScope
    {
        [SerializeField] private UniversalSettingsPanelView universalSettingsPanelView;
        
        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);
            builder.RegisterComponent(universalSettingsPanelView).AsSelf().As<IPanelView>();
            builder.Register<UniversalSettingsPanelViewModel>(Lifetime.Singleton)
                .As<IPanelViewModel>()
                .AsSelf();
        }
        
        public override IPanelViewModel CreatPanel()
        {
            return Container.Resolve<IPanelViewModel>();
        }
    }
}
