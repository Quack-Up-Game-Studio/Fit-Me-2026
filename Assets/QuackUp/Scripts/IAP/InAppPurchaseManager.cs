using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using FitMe.GameData;
using MessagePipe;
using R3;
using UnityEngine;
using UnityEngine.Purchasing;
using VContainer;
using VContainer.Unity;

namespace QuackUp.IAP
{
    public struct EndSubscriptionEvent { }
    
    public static class ProductIds
    {
        public const string MonthlyPass   = "monthlypass";
        public const string QuarterlyPass = "quarterltypass";
        public const string AnnuallyPass  = "annuallypass";
        public const string Energy2       = "2_1energy";
        public const string Energy3       = "3_2energy";
        public const string MaxEnergy     = "max_energy";

#if UNITY_EDITOR
        public const string GoldTest = "gold.100";
#endif
    }

    public class InAppPurchaseManager : IStartable, IStoreListener
    {
        private IStoreController m_StoreController;
        private IExtensionProvider m_StoreExtensionProvider;
        private EnergyManager _energyManager;
        public event Action OnInitializedSuccess;
        public event Action OnPurchaseSuccess;
        private IDisposable _subscriptions;
        private CancellationTokenSource _subscriptionCheckCts;

        [Inject]
        public void Construct(ISubscriber<EndSubscriptionEvent> endSubscriptionEvent,
            EnergyManager energyManager)
        {
            var disposableBuilder = Disposable.CreateBuilder();
            endSubscriptionEvent.Subscribe(evt => EndOfSubscription())
                .AddTo(ref disposableBuilder);
            _subscriptions = disposableBuilder.Build();
            _energyManager = energyManager;
        }

        private void Dispose()
        {
            _subscriptions?.Dispose();
            _subscriptionCheckCts?.Cancel();
            _subscriptionCheckCts?.Dispose();
        }
        
        public void Start()
        {
            InitializePurchasing();
        }

        private void StartSubscriptionCheckLoop()
        {
            _subscriptionCheckCts = new CancellationTokenSource();
            SubscriptionCheckLoop(_subscriptionCheckCts.Token).Forget();
        }

        private async UniTaskVoid SubscriptionCheckLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromMinutes(5), cancellationToken: token);
        
                if (!HasActiveSubscription())
                {
                    Debug.Log("IAP: Subscription expired.");
                    EndOfSubscription();
                }
            }
        }
        
        public void InitializePurchasing()
        {
            if (IsInitialized()) return;

            var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());

#if UNITY_EDITOR
            builder.AddProduct(ProductIds.GoldTest, ProductType.Consumable,
                new StoreSpecificIds() { { ProductIds.GoldTest, GooglePlay.Name } });
#endif

            // Consumable products
            builder.AddProduct(ProductIds.Energy2, ProductType.Consumable,
                new StoreSpecificIds() { { ProductIds.Energy2, GooglePlay.Name } });
            builder.AddProduct(ProductIds.Energy3, ProductType.Consumable,
                new StoreSpecificIds() { { ProductIds.Energy3, GooglePlay.Name } });
            builder.AddProduct(ProductIds.MaxEnergy, ProductType.Consumable,
                new StoreSpecificIds() { { ProductIds.MaxEnergy, GooglePlay.Name } });

            // Subscription products
            builder.AddProduct(ProductIds.MonthlyPass, ProductType.Subscription,
                new StoreSpecificIds() { { ProductIds.MonthlyPass, GooglePlay.Name } });
            builder.AddProduct(ProductIds.QuarterlyPass, ProductType.Subscription,
                new StoreSpecificIds() { { ProductIds.QuarterlyPass, GooglePlay.Name } });
            builder.AddProduct(ProductIds.AnnuallyPass, ProductType.Subscription,
                new StoreSpecificIds() { { ProductIds.AnnuallyPass, GooglePlay.Name } });
            
            UnityPurchasing.Initialize(this, builder);
        }

        public bool IsInitialized() => m_StoreController != null && m_StoreExtensionProvider != null;

        public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            Debug.Log("IAP: Store initialized successfully.");
            m_StoreController = controller;
            m_StoreExtensionProvider = extensions;
            
            if (!HasActiveSubscription())
                EndOfSubscription();
            
            StartSubscriptionCheckLoop();
            OnInitializedSuccess?.Invoke();
        }

        public void OnInitializeFailed(InitializationFailureReason error)
            => OnInitializeFailed(error, null);

        public void OnInitializeFailed(InitializationFailureReason error, string message = null)
        {
            Debug.LogWarning($"IAP: Store init failed: {error}" + (message != null ? $" - {message}" : ""));
        }

        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
        {
            if (!ValidateReceipt(args))
            {
                Debug.LogWarning($"IAP: Receipt validation failed for {args.purchasedProduct.definition.id}");
                return PurchaseProcessingResult.Complete;
            }

            switch (args.purchasedProduct.definition.id)
            {
#if UNITY_EDITOR
                case ProductIds.GoldTest:
                    Debug.Log("IAP: Test product - 100 gold granted.");
                    // _currencyService.AddGold(100);
                    break;
#endif
                // Consumable products
                case ProductIds.Energy2:
                    Debug.Log("IAP: 2 energy granted.");
                    _energyManager.ChangeEnergy(3);
                    break;
                case ProductIds.Energy3:
                    Debug.Log("IAP: 3 energy granted.");
                    _energyManager.ChangeEnergy(5);
                    break;
                case ProductIds.MaxEnergy:
                    Debug.Log("IAP: Max energy granted.");
                    _energyManager.ChangeEnergy(7);
                    break;

                // Subscription products
                case ProductIds.MonthlyPass:
                    Debug.Log("IAP: Monthly pass activated.");
                    _energyManager.SetInfiniteEnergy(true);
                    break;
                case ProductIds.QuarterlyPass:
                    Debug.Log("IAP: Quarterly pass activated.");
                    _energyManager.SetInfiniteEnergy(true);
                    break;
                case ProductIds.AnnuallyPass:
                    Debug.Log("IAP: Annual pass activated.");
                    _energyManager.SetInfiniteEnergy(true);
                    break;

                default:
                    Debug.LogWarning($"IAP: Unknown product ID: {args.purchasedProduct.definition.id}");
                    break;
            }

            OnPurchaseSuccess?.Invoke();
            return PurchaseProcessingResult.Complete;
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
        {
            Debug.LogWarning($"IAP: Purchase failed: {product.definition.id}, Reason: {failureReason}");
        }

        public void BuyProductID(string productId)
        {
            if (IsInitialized())
            {
                m_StoreController.InitiatePurchase(productId);
            }
            else
            {
                Debug.LogWarning("IAP: Store not ready. Please try again later.");
            }
        }

        private bool ValidateReceipt(PurchaseEventArgs args)
        {
#if UNITY_EDITOR
            return true;
#else
            Debug.LogWarning("IAP: Receipt validation not yet implemented.");
            return true;
            /*try
            {
                var validator = new CrossPlatformValidator(
                    GooglePlayTangle.Data(),
                    AppleTangle.Data(),
                    Application.identifier);

                var result = validator.Validate(args.purchasedProduct.receipt);
                foreach (var receipt in result)
                {
                    Debug.Log($"IAP: Receipt validated - ProductID: {receipt.productID}");
                }
                return true;
            }
            catch (IAPSecurityException ex)
            {
                Debug.LogWarning($"IAP: Invalid receipt: {ex.Message}");
                return false;
            }*/
#endif
        }
        
        public string GetLocalizedPrice(string productId)
        {
            if (!IsInitialized()) return "";
            var product = m_StoreController.products.WithID(productId);
            return product?.metadata.localizedPriceString ?? "";
        }
        
        public void EndOfSubscription()
        {
            _energyManager.SetInfiniteEnergy(false);
            Debug.Log( "IAP: Subscription ended. Infinite energy revoked.");
        }
        
        public bool HasActiveSubscription()
        {
            if (!IsInitialized()) return false;

            var subscriptionIds = new[]
            {
                ProductIds.MonthlyPass,
                ProductIds.QuarterlyPass,
                ProductIds.AnnuallyPass
            };

            foreach (var id in subscriptionIds)
            {
                var product = m_StoreController.products.WithID(id);
                if (product?.hasReceipt == true)
                    return true;
            }
            return false;
        }
    }
}
