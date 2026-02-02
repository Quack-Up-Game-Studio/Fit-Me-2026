using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Panel
{
    public class SettingsPanelLifetimeScope : PanelLifetimeScope
    {
        [SerializeField] private SettingsPanelView settingsPanelView;

        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);
            builder.RegisterComponent(settingsPanelView).AsSelf().As<IPanelView>();
            builder.Register<SettingsPanelViewModel>(Lifetime.Singleton).As<IPanelViewModel>();
        }

        public override IPanelViewModel CreatPanel()
        {
            return Container.Resolve<IPanelViewModel>();
        }
    }
}