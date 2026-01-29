using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Panel
{
    public class PausePanelLifetimeScope : PanelLifetimeScope
    {
        [SerializeField] private PausePanelView panelView;
        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);
            builder.RegisterComponent(panelView).AsSelf().As<IPanelView>();
            builder.Register<PausePanelViewModel>(Lifetime.Singleton).AsSelf().As<IPanelViewModel>();
        }

        public override IPanelViewModel CreatPanel()
        {
            var viewModel = Container.Resolve<PausePanelViewModel>();
            panelView.Construct(viewModel);
            return viewModel;
        }
    }
}