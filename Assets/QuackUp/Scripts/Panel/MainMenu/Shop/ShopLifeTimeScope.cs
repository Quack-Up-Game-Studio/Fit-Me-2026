using QuackUp.IAP;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Panel
{
    public class ShopLifeTimeScope : PanelLifetimeScope
    {
        [SerializeField] private ShopPanelView shopPanelView;

        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);
            builder.RegisterComponent(shopPanelView).AsSelf().As<IPanelView>();
            builder.Register<ShopPanelViewModel>(Lifetime.Singleton).AsSelf().As<IPanelViewModel>();
            
            builder.Register<InAppPurchaseManager>(Lifetime.Singleton);
        }
        
        public override IPanelViewModel CreatPanel()
        {
            return Container.Resolve<ShopPanelViewModel>();
        }
    }
}