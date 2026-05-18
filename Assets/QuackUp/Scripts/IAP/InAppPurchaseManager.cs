using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using FitMe.GameData;
using MessagePipe;
using QuackUp.Utils;
using R3;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Purchasing;
using VContainer;
using VContainer.Unity;
using DisposableBag = R3.DisposableBag;
using Result = UnityEngine.Purchasing.Result;
#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_TVOS)
using UnityEngine.Purchasing.Security;
#endif

namespace QuackUp.IAP
{
    public struct EndSubscriptionEvent { }
    
    public static class ProductIds
    {
        public const string MonthlyPass   = "monthlypass";
        public const string QuarterlyPass = "quarterltypass"; // intentionally kept as-is
        public const string AnnuallyPass  = "annuallypass";
        public const string Energy2       = "2_1energy";
        public const string Energy3       = "3_2energy";
        public const string MaxEnergy     = "max_energy";

#if UNITY_EDITOR
        public const string GoldTest = "gold.100";
#endif
    }

    [Serializable]
    public class InAppPurchaseManager : IStartable, IDisposable
    {
        private CatalogProvider _catalogProvider;
        private EnergyManager _energyManager;
        private AdsService _adsService;

        /// <summary>
        /// Event that called when the store is connected, products and purchases are fetched, and the IAP system is ready to use.
        /// </summary>
        public Observable<Unit> OnIAPReady => _onIAPReady;
        public Observable<Unit> OnPurchaseSuccess => _onPurchaseSuccess;
        private Subject<Unit> _onPurchaseSuccess = new Subject<Unit>();
        private Subject<Unit> _onIAPReady = new Subject<Unit>();
        private DisposableBag _fetchProductsSubscriptions;
        private DisposableBag _fetchPurchasesSubscriptions;
        private DisposableBag _connectSubscriptions;
        private DisposableBag _purchaseSubscriptions;
        private IDisposable _subscriptions;
        private IDisposable _expirationTimer;
        private IDisposable _periodicCheckTimer;
        private ISubscriber<EndSubscriptionEvent> _endSubscriptionEvent;
        private bool _initializing;

        [ShowInInspector, ReadOnly] public bool IsIAPReady => IsConnected && IsProductReady && IsPurchaseReady;
        [ShowInInspector, ReadOnly] public bool IsConnected { get; private set; }
        [ShowInInspector, ReadOnly] public bool IsProductReady { get; private set; }
        [ShowInInspector, ReadOnly] public bool IsPurchaseReady { get; private set; }
        private readonly List<SubscriptionInfo> _confirmedSubscriptions = new();
        [ShowInInspector, ReadOnly] private IReadOnlyList<string> DebugConfirmedSubscriptions =>
            _confirmedSubscriptions.Select(x => x.GetProductId()).ToList();

        [Button("Debug Initialize")]
        private void DebugInitialize() => Initialize().Forget();

        [Button("Debug Clear Subscriptions")]
        private void DebugClearSubscriptions()
        {
            _confirmedSubscriptions.Clear();
            EndOfSubscription();
        }

        [Inject]
        public void Construct(
            ISubscriber<EndSubscriptionEvent> endSubscriptionEvent,
            EnergyManager energyManager,
            AdsService adsService)
        {
            _energyManager = energyManager;
            _endSubscriptionEvent = endSubscriptionEvent;
            _adsService = adsService;
            Subscribe();
        }

        private void Subscribe()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            _endSubscriptionEvent
                .Subscribe(_ => EndOfSubscription())
                .AddTo(ref disposableBuilder);
            _subscriptions = disposableBuilder.Build();
        }

        public void Dispose()
        {
            _fetchProductsSubscriptions.Dispose();
            _fetchPurchasesSubscriptions.Dispose();
            _connectSubscriptions.Dispose();
            _purchaseSubscriptions.Dispose();
            _subscriptions?.Dispose();
            _expirationTimer?.Dispose();
            _periodicCheckTimer?.Dispose();
            _onPurchaseSuccess?.Dispose();
            _onIAPReady?.Dispose();
        }

        public void Start()
        {
            CreateCatalog();
            Initialize().Forget();
        }

        private void CreateCatalog()
        {
            var catalogProvider = new CatalogProvider();

#if UNITY_EDITOR
            catalogProvider.AddProduct(ProductIds.GoldTest, ProductType.Consumable,
                new StoreSpecificIds() { { ProductIds.GoldTest, GooglePlay.Name } });
#endif

            catalogProvider.AddProduct(ProductIds.Energy2, ProductType.Consumable,
                new StoreSpecificIds() { { ProductIds.Energy2, GooglePlay.Name } });
            catalogProvider.AddProduct(ProductIds.Energy3, ProductType.Consumable,
                new StoreSpecificIds() { { ProductIds.Energy3, GooglePlay.Name } });
            catalogProvider.AddProduct(ProductIds.MaxEnergy, ProductType.Consumable,
                new StoreSpecificIds() { { ProductIds.MaxEnergy, GooglePlay.Name } });

            catalogProvider.AddProduct(ProductIds.MonthlyPass, ProductType.Subscription,
                new StoreSpecificIds() { { ProductIds.MonthlyPass, GooglePlay.Name } });
            catalogProvider.AddProduct(ProductIds.QuarterlyPass, ProductType.Subscription,
                new StoreSpecificIds() { { ProductIds.QuarterlyPass, GooglePlay.Name } });
            catalogProvider.AddProduct(ProductIds.AnnuallyPass, ProductType.Subscription,
                new StoreSpecificIds() { { ProductIds.AnnuallyPass, GooglePlay.Name } });

            _catalogProvider = catalogProvider;
        }

        private async UniTask Initialize()
        {
            _initializing = true;
            if (!IsConnected)
                await InitializeConnection();
            if (!IsProductReady)
                await InitializeProducts();
            if (!IsPurchaseReady)
                await InitializePurchases();
            if (IsIAPReady)
                _onIAPReady?.OnNext(Unit.Default);
            _initializing = false;
        }

        public async UniTask Reinitialize()
        {
            if (_initializing) return;
            IsConnected = false;
            IsProductReady = false;
            IsPurchaseReady = false;
            await Initialize();
        }

        #region Connection

        private async UniTask InitializeConnection()
        {
            _connectSubscriptions.Dispose();
            var storeController = UnityIAPServices.StoreController();
            _connectSubscriptions = new DisposableBag();
            Observable.FromEvent<StoreConnectionFailureDescription>(
                    handler => storeController.OnStoreDisconnected += handler,
                    handler => storeController.OnStoreDisconnected -= handler)
                .Subscribe(OnStoreDisconnected)
                .AddTo(ref _connectSubscriptions);
            await storeController.Connect();
            IsConnected = true;
        }

        private void OnStoreDisconnected(StoreConnectionFailureDescription desc)
        {
            IsConnected = false;
            IsProductReady = false;
            IsPurchaseReady = false;
            DebugUtils.LogError($"IAP: Store connection failed: {desc.Message}");
            if (!desc.IsRetryable) return;
            Debug.Log("IAP: Retrying store connection...");
            Initialize().Forget();
        }

        #endregion

        #region Products

        private async UniTask InitializeProducts()
        {
            if (_catalogProvider == null || _catalogProvider.GetProducts().Count == 0)
            {
                DebugUtils.LogError("IAP: No products found in catalog.");
                return;
            }
            var storeController = UnityIAPServices.StoreController();
            _fetchProductsSubscriptions.Dispose();
            _fetchProductsSubscriptions = new DisposableBag();
            var productFetchTcs = new UniTaskCompletionSource<bool>();
            Observable.FromEvent<List<Product>>(
                    handler => storeController.OnProductsFetched += handler,
                    handler => storeController.OnProductsFetched -= handler)
                .Subscribe(products => OnProductsFetched(products, productFetchTcs))
                .AddTo(ref _fetchProductsSubscriptions);
            Observable.FromEvent<ProductFetchFailed>(
                    handler => storeController.OnProductsFetchFailed += handler,
                    handler => storeController.OnProductsFetchFailed -= handler)
                .Subscribe(failure => OnProductsFetchedFail(failure, productFetchTcs))
                .AddTo(ref _fetchProductsSubscriptions);
            storeController.FetchProductsWithNoRetries(_catalogProvider.GetProducts());
            await productFetchTcs.Task;
            IsProductReady = productFetchTcs.GetResult(0);
        }

        private void OnProductsFetched(List<Product> products, UniTaskCompletionSource<bool> tcs)
        {
            DebugUtils.Log("IAP: Products fetched successfully");
            tcs.TrySetResult(true);
        }

        private void OnProductsFetchedFail(ProductFetchFailed productFetchFailed, UniTaskCompletionSource<bool> tcs)
        {
            DebugUtils.LogError($"IAP: Failed to fetch products: {productFetchFailed.FailureReason}");
            tcs.TrySetResult(false);
        }

        #endregion

        #region Purchases

        private async UniTask InitializePurchases()
        {
            var storeController = UnityIAPServices.StoreController();
            _fetchPurchasesSubscriptions.Dispose();
            _fetchPurchasesSubscriptions = new DisposableBag();
            var purchaseFetchTcs = new UniTaskCompletionSource<bool>();
            Observable.FromEvent<Orders>(
                    handler => storeController.OnPurchasesFetched += handler,
                    handler => storeController.OnPurchasesFetched -= handler)
                .Subscribe(orders => OnPurchasesFetched(orders, purchaseFetchTcs))
                .AddTo(ref _fetchPurchasesSubscriptions);
            Observable.FromEvent<PurchasesFetchFailureDescription>(
                    handler => storeController.OnPurchasesFetchFailed += handler,
                    handler => storeController.OnPurchasesFetchFailed -= handler)
                .Subscribe(failure => OnPurchasesFetchedFail(failure, purchaseFetchTcs))
                .AddTo(ref _fetchPurchasesSubscriptions);

            _purchaseSubscriptions.Dispose();
            _purchaseSubscriptions = new DisposableBag();
            Observable.FromEvent<PendingOrder>(
                    handler => storeController.OnPurchasePending += handler,
                    handler => storeController.OnPurchasePending -= handler)
                .Subscribe(OnPurchasePending)
                .AddTo(ref _purchaseSubscriptions);
            Observable.FromEvent<FailedOrder>(
                    handler => storeController.OnPurchaseFailed += handler,
                    handler => storeController.OnPurchaseFailed -= handler)
                .Subscribe(OnPurchaseFailed)
                .AddTo(ref _purchaseSubscriptions);

            storeController.FetchPurchases();
            await purchaseFetchTcs.Task;
            IsPurchaseReady = purchaseFetchTcs.GetResult(0);
        }

        private void OnPurchasesFetched(Orders orders, UniTaskCompletionSource<bool> tcs)
        {
            var confirmedSubscription =
                orders.ConfirmedOrders
                    .Where(x => ValidateReceipt(x.Info.Receipt))
                    .Select(x => (x.CartOrdered.Items().FirstOrDefault()?.Product, x.Info.Receipt))
                    .Where(p => p.Product != null && p.Product.definition.type == ProductType.Subscription)
                    .Select(p => GetSubscriptionInfo(p.Product, p.Receipt))
                    .ToList();
            _confirmedSubscriptions.Clear();
            _confirmedSubscriptions.AddRange(confirmedSubscription);

            if (HasActiveSubscription())
            {
                DebugUtils.Log("IAP: Active subscription found, restoring benefits.");
                _energyManager.SetInfiniteEnergy(true);
                _adsService.SetEnableStateAll(false);
            }
            else
            {
                DebugUtils.Log("IAP: No active subscription.");
                _energyManager.SetInfiniteEnergy(false);
                _adsService.SetEnableStateAll(true);
            }

            StartSubscriptionCheckTimer();
            StartExpirationTimer();
            tcs.TrySetResult(true);
        }

        private void OnPurchasesFetchedFail(PurchasesFetchFailureDescription failureDescription, UniTaskCompletionSource<bool> tcs)
        {
            DebugUtils.LogError($"IAP: Failed to fetch purchases: {failureDescription.FailureReason}");
            tcs.TrySetResult(false);
        }

        private void OnPurchasePending(PendingOrder order)
        {
            var receipt = order.Info.Receipt;
            if (!ValidateReceipt(receipt)) return;
            var product = order.CartOrdered.Items().FirstOrDefault()?.Product;
            var id = product?.definition.id;
            switch (id)
            {
#if UNITY_EDITOR
                case ProductIds.GoldTest:
                    DebugUtils.Log("IAP: Test product - 100 gold granted.");
                    // _currencyService.AddGold(100);
                    break;
#endif
                case ProductIds.Energy2:
                    DebugUtils.Log("IAP: 3 energy granted.");
                    _energyManager.ChangeEnergy(3);
                    break;
                case ProductIds.Energy3:
                    DebugUtils.Log("IAP: 5 energy granted.");
                    _energyManager.ChangeEnergy(5);
                    break;
                case ProductIds.MaxEnergy:
                    DebugUtils.Log("IAP: Max energy granted.");
                    _energyManager.ChangeEnergy(_energyManager.Config.MaxEnergy);
                    break;
                case ProductIds.MonthlyPass:
                case ProductIds.QuarterlyPass:
                case ProductIds.AnnuallyPass:
                    DebugUtils.Log($"IAP: {id} pass activated.");
                    _energyManager.SetInfiniteEnergy(true);
                    _adsService.SetEnableStateAll(false);
                    _confirmedSubscriptions.Add(GetSubscriptionInfo(product, receipt));
                    StartExpirationTimer();
#if UNITY_EDITOR
                    PlayerPrefs.SetInt("Mock_HasVIP", 1);
                    PlayerPrefs.Save();
#endif
                    break;
                default:
                    DebugUtils.LogWarning($"IAP: Unknown product ID: {id}");
                    break;
            }
            var storeController = UnityIAPServices.StoreController();
            storeController.ConfirmPurchase(order);
            _onPurchaseSuccess?.OnNext(Unit.Default);
        }

        private void OnPurchaseFailed(FailedOrder order)
        {
            var id = order.CartOrdered.Items().FirstOrDefault()?.Product.definition.id;
            var failureReason = order.FailureReason;
            DebugUtils.LogWarning($"IAP: Purchase failed: {id}, Reason: {failureReason}");
        }

        private bool ValidateReceipt(string receipt)
        {
           
#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_TVOS)
            if (string.IsNullOrEmpty(receipt))
            {
                DebugUtils.LogWarning("IAP: Cannot validate null or empty receipt");
                return false;
            }
            var validator = new CrossPlatformValidator(GooglePlayTangle.Data(), Application.identifier);
            try
            {
                var results = validator.Validate(receipt);
                foreach (var result in results)
                {
                    DebugUtils.Log($"IAP: Receipt valid. Product ID: {result.productID}, Purchase Date: {result.purchaseDate}, Transaction ID: {result.transactionID}");
                }
                return true;
            }
            catch (IAPSecurityException e) 
            {
                DebugUtils.LogError($"IAP: Failed to validate receipt: {receipt}, exception: {e.Message}");
                return false;
            }
#endif
            return true; // Auto pass for other platforms.
        }

        public void BuyProductID(string productId)
        {
            if (IsIAPReady)
            {
                var storeController = UnityIAPServices.StoreController();
                storeController.PurchaseProduct(productId);
            }
            else
            {
                DebugUtils.LogWarning("IAP: Store not ready. Please try again later.");
            }
        }
        #endregion

        #region Subscriptions

        private void StartSubscriptionCheckTimer()
        {
            _periodicCheckTimer?.Dispose();
            _periodicCheckTimer = Observable.Interval(TimeSpan.FromMinutes(5))
                .Subscribe(_ => CheckAndUpdateSubscription().Forget());
        }

        private void StartExpirationTimer()
        {
            _expirationTimer?.Dispose();
            var expiration = _confirmedSubscriptions
                .Select(x => x.GetExpireDate())
                .Where(date => date > DateTime.UtcNow) // Only future dates
                .OrderByDescending(x => x)
                .FirstOrDefault();
            // Check if we have a valid expiration date (not default and in the future)
            if (expiration > DateTime.UtcNow && expiration < new DateTime(9999, 12, 31))
            {
                var expirationOffset = new DateTimeOffset(expiration, TimeSpan.Zero);
                _expirationTimer = Observable.Timer(expirationOffset)
                    .Subscribe(_ => CheckAndUpdateSubscription().Forget());
                DebugUtils.Log($"IAP: Timer set for {expiration:yyyy-MM-dd HH:mm:ss} UTC");
            }
            else
            {
                DebugUtils.Log("IAP: No valid subscription expiration date found. Skipping expiration timer.");
            }
        }

        private async UniTaskVoid CheckAndUpdateSubscription()
        {
            IsPurchaseReady = false;
            await InitializePurchases();
            if (HasActiveSubscription()) return;
            DebugUtils.Log("IAP: Subscription expired.");
            EndOfSubscription();
        }

        private void EndOfSubscription()
        {
            _energyManager.SetInfiniteEnergy(false);
            _adsService.SetEnableStateAll(true);
            DebugUtils.Log("IAP: Subscription ended. Infinite energy revoked.");
        }

        private SubscriptionInfo GetSubscriptionInfo(Product product, string receipt = null)
        {
            DebugUtils.Log($"Getting subscription info for: {product.definition.id} | receipt: {receipt}...");
            var infoHelper = string.IsNullOrEmpty(receipt) ?
                new SubscriptionInfoHelper(product, null) : 
                new SubscriptionInfoHelper(receipt, product.definition.storeSpecificId, null);
            try
            {
                return infoHelper.GetSubscriptionInfo();
            }
            catch (StoreSubscriptionInfoNotSupportedException)
            {
                return null;
            }
        }
        public bool HasActiveSubscription()
        {
            return _confirmedSubscriptions.Any(x => x.IsSubscribed() is Result.True);
        }

        public bool IsInFreeTrial()
        {
            return _confirmedSubscriptions.Any(x => x.IsFreeTrial() is Result.True);
        }

        #endregion

        #region Helpers

        public string GetLocalizedPrice(string productId)
        {
            if (!IsIAPReady) return "";
            var storeController = UnityIAPServices.StoreController();
            var product = storeController.GetProductById(productId);
            return product?.metadata.localizedPriceString ?? "";
        }
        #endregion
    }
}