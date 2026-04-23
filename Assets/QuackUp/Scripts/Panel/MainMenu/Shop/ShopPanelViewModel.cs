using System;
using QuackUp.Utils;
using R3;
using VContainer;

namespace FitMe.Panel
{
    public class ShopPanelViewModel : PanelViewModel
    {
        private readonly InAppPurchaseManager _inAppPurchaseManager;
        private IDisposable _bindings;

        [Inject]
        public ShopPanelViewModel(
            PanelManager panelManager,
            InAppPurchaseManager inAppPurchaseManager) : base(panelManager)
        {
            _inAppPurchaseManager = inAppPurchaseManager;
        }

        public void OnBuyButtonClicked(string productId)
        {
            _inAppPurchaseManager.BuyProductID(productId);
        }

        public override void Dispose()
        {
            base.Dispose();
            _bindings?.Dispose();
        }
    }
}