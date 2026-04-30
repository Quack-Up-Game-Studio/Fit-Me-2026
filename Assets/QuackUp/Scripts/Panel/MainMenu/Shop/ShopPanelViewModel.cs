using System;
using System.Collections.Generic;
using MessagePipe;
using QuackUp.IAP;
using R3;
using VContainer;

namespace FitMe.Panel
{
    public enum ProductId
    {
        Energy2,
        Energy3,
        MaxEnergy,
        MonthlyPass,
        QuarterlyPass,
        AnnuallyPass
    }
    
    public static class ProductIdExtensions
    {
        public static string ToProductString(this ProductId id) => id switch
        {
            ProductId.Energy2       => ProductIds.Energy2,
            ProductId.Energy3       => ProductIds.Energy3,
            ProductId.MaxEnergy     => ProductIds.MaxEnergy,
            ProductId.MonthlyPass   => ProductIds.MonthlyPass,
            ProductId.QuarterlyPass => ProductIds.QuarterlyPass,
            ProductId.AnnuallyPass  => ProductIds.AnnuallyPass,
            _ => throw new ArgumentOutOfRangeException()
        };
    }
    
    public class ShopPanelViewModel : PanelViewModel
    {
        public bool HasActiveSubscription() => _inAppPurchaseManager.HasActiveSubscription();
        public string GetPrice(string productId) => _inAppPurchaseManager.GetLocalizedPrice(productId);
        private IPublisher<EndSubscriptionEvent> _endSubscriptionPublisher;
        private InAppPurchaseManager _inAppPurchaseManager;
        private IDisposable _bindings;

        public ShopPanelViewModel(
            PanelManager panelManager,
            InAppPurchaseManager inAppPurchaseManager
            ) : base(panelManager)
        {
            _inAppPurchaseManager = inAppPurchaseManager;
            _inAppPurchaseManager.Start();
        }
        
        public void RegisterOnPurchaseSuccess(Action onSuccess)
        {
            _inAppPurchaseManager.OnPurchaseSuccess += onSuccess;
        }
        
        public void RegisterOnIAPReady(Action onReady)
        {
            if (_inAppPurchaseManager.IsInitialized())
                onReady?.Invoke();
            else
                _inAppPurchaseManager.OnInitializedSuccess += onReady;
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