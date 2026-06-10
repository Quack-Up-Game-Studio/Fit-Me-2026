using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using FitMe.GameData;
using FitMe.Shared;
using GameAnalyticsSDK;
using MessagePipe;
using QuackUp.Save;
using QuackUp.SocialService;
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
        public const string QuarterlyPass = "quarterltypass";
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
        private readonly EnergyManager _energyManager;
        private readonly AdsService _adsService;
        private readonly MessagePackSaveManager _saveManager;
        private readonly ICloudSaveService _cloudSaveService;
        private readonly ISubscriber<EndSubscriptionEvent> _endSubscriptionEvent;

        /// <summary>
        /// Event that called when the store is connected, products and purchases are fetched, and the IAP system is ready to use.
        /// </summary>
        public Observable<Unit> OnIAPReady => _onIAPReady;
        public Observable<Unit> OnPurchaseSuccess => _onPurchaseSuccess;
        private Subject<Unit> _onPurchaseSuccess = new();
        private Subject<Unit> _onIAPReady = new();
        private DisposableBag _fetchProductsSubscriptions;
        private DisposableBag _fetchPurchasesSubscriptions;
        private DisposableBag _connectSubscriptions;
        private DisposableBag _purchaseSubscriptions;
        private IDisposable _entitlementSubscription;
        private IDisposable _subscriptions;
        private IDisposable _expirationTimer;
        private IDisposable _periodicCheckTimer;
        private CatalogProvider _catalogProvider;
        private bool _initializing;
        private StoreController StoreController => UnityIAPServices.StoreController();

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
            EndSubscription();
        }

        [Inject]
        public InAppPurchaseManager(
            EnergyManager energyManager,
            AdsService adsService,
            MessagePackSaveManager saveManager,
            ICloudSaveService cloudSaveService,
            ISubscriber<EndSubscriptionEvent> endSubscriptionEvent)
        {
            _energyManager = energyManager;
            _adsService = adsService;
            _saveManager = saveManager;
            _cloudSaveService = cloudSaveService;
            _endSubscriptionEvent = endSubscriptionEvent;
            Subscribe();
        }

        private void Subscribe()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            _endSubscriptionEvent
                .Subscribe(_ => EndSubscription())
                .AddTo(ref disposableBuilder);
            _subscriptions = disposableBuilder.Build();
        }

        public void Dispose()
        {
            _fetchProductsSubscriptions.Dispose();
            _fetchPurchasesSubscriptions.Dispose();
            _connectSubscriptions.Dispose();
            _purchaseSubscriptions.Dispose();
            _entitlementSubscription?.Dispose();
            _subscriptions?.Dispose();
            _expirationTimer?.Dispose();
            _periodicCheckTimer?.Dispose();
            _onPurchaseSuccess?.Dispose();
            _onIAPReady?.Dispose();
        }

        public void Start()
        {
            CreateCatalog();
            StartTask().Forget();
        }
        
        private async UniTaskVoid StartTask()
        {
            await Initialize();
            await _saveManager.WaitForSaveDataReady;
            UpdateAnalyticPlayerTier();
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
            if (_initializing) return;
            _initializing = true;
            SubscribeBeforeConnect();
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

        private void SubscribeBeforeConnect()
        {
            _purchaseSubscriptions.Dispose();
            _purchaseSubscriptions = new DisposableBag();
            Observable.FromEvent<Order>(
                    handler => StoreController.OnPurchaseConfirmed += handler,
                    handler => StoreController.OnPurchaseConfirmed -= handler)
                .Subscribe(OnPurchaseConfirmed)
                .AddTo(ref _purchaseSubscriptions);
            Observable.FromEvent<PendingOrder>(
                    handler => StoreController.OnPurchasePending += handler,
                    handler => StoreController.OnPurchasePending -= handler)
                .Subscribe(OnPurchasePending)
                .AddTo(ref _purchaseSubscriptions);
            Observable.FromEvent<FailedOrder>(
                    handler => StoreController.OnPurchaseFailed += handler,
                    handler => StoreController.OnPurchaseFailed -= handler)
                .Subscribe(OnPurchaseFailed)
                .AddTo(ref _purchaseSubscriptions);
            Observable.FromEvent<DeferredOrder>(
                    handler => StoreController.OnPurchaseDeferred += handler,
                    handler => StoreController.OnPurchaseDeferred -= handler)
                .Subscribe(OnPurchaseDeferred)
                .AddTo(ref _purchaseSubscriptions);
        }

        #region Connection

        private async UniTask InitializeConnection()
        {
            _connectSubscriptions.Dispose();
            _connectSubscriptions = new DisposableBag();
            var connectionTcs = new UniTaskCompletionSource<bool>();
            Observable.FromEvent<StoreConnectionFailureDescription>(
                    handler => StoreController.OnStoreDisconnected += handler,
                    handler => StoreController.OnStoreDisconnected -= handler)
                .Subscribe(x => OnStoreDisconnected(x, connectionTcs))
                .AddTo(ref _connectSubscriptions);
            Observable.FromEvent(
                    handler => StoreController.OnStoreConnected += handler,
                    handler => StoreController.OnStoreConnected -= handler)
                .Subscribe(_ => OnStoreConnected(connectionTcs))
                .AddTo(ref _connectSubscriptions);
            await StoreController.Connect();
            var result = await connectionTcs.Task;
            IsConnected = result;
            if (!result)
            {
                IsProductReady = false;
                IsPurchaseReady = false;
            }
        }

        private void OnStoreConnected(UniTaskCompletionSource<bool> tcs)
        {
            tcs.TrySetResult(true);
            DebugUtils.Log($"IAP: Store connected");
        }

        private void OnStoreDisconnected(StoreConnectionFailureDescription desc, UniTaskCompletionSource<bool> tcs)
        {
            tcs.TrySetResult(false);
            DebugUtils.LogError($"IAP: Store connection failed: {desc.Message}");
            if (!desc.IsRetryable) return;
            DebugUtils.Log("IAP: Retrying store connection...");
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
            _fetchProductsSubscriptions.Dispose();
            _fetchProductsSubscriptions = new DisposableBag();
            var productFetchTcs = new UniTaskCompletionSource<bool>();
            Observable.FromEvent<List<Product>>(
                    handler => StoreController.OnProductsFetched += handler,
                    handler => StoreController.OnProductsFetched -= handler)
                .Subscribe(products => OnProductsFetched(products, productFetchTcs))
                .AddTo(ref _fetchProductsSubscriptions);
            Observable.FromEvent<ProductFetchFailed>(
                    handler => StoreController.OnProductsFetchFailed += handler,
                    handler => StoreController.OnProductsFetchFailed -= handler)
                .Subscribe(failure => OnProductsFetchedFail(failure, productFetchTcs))
                .AddTo(ref _fetchProductsSubscriptions);
            StoreController.FetchProductsWithNoRetries(_catalogProvider.GetProducts());
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
            _fetchPurchasesSubscriptions.Dispose();
            _fetchPurchasesSubscriptions = new DisposableBag();
            var purchaseFetchTcs = new UniTaskCompletionSource<bool>();
            Observable.FromEvent<Orders>(
                    handler => StoreController.OnPurchasesFetched += handler,
                    handler => StoreController.OnPurchasesFetched -= handler)
                .Subscribe(orders => OnPurchasesFetched(orders, purchaseFetchTcs))
                .AddTo(ref _fetchPurchasesSubscriptions);
            Observable.FromEvent<PurchasesFetchFailureDescription>(
                    handler => StoreController.OnPurchasesFetchFailed += handler,
                    handler => StoreController.OnPurchasesFetchFailed -= handler)
                .Subscribe(failure => OnPurchasesFetchedFail(failure, purchaseFetchTcs))
                .AddTo(ref _fetchPurchasesSubscriptions);
            StoreController.FetchPurchases();
            await purchaseFetchTcs.Task;
            IsPurchaseReady = purchaseFetchTcs.GetResult(0);
        }

        private void OnPurchasesFetched(Orders orders, UniTaskCompletionSource<bool> tcs)
        {
            // var confirmedSubscription =
            //     orders.ConfirmedOrders
            //         .Where(x => ValidateReceipt(x.Info.Receipt))
            //         .Select(x => (x.CartOrdered.Items().FirstOrDefault()?.Product, x.Info.Receipt))
            //         .Where(p => p.Product != null && p.Product.definition.type == ProductType.Subscription)
            //         .Select(p => GetSubscriptionInfo(p.Product, p.Receipt))
            //         .ToList();
            
            var confirmedSubscription =
                orders.ConfirmedOrders
                    .Where(x => ValidateReceipt(x.Info.Receipt))
                    .Select(x => (x.CartOrdered.Items().FirstOrDefault()?.Product, x.Info?.PurchasedProductInfo.FirstOrDefault()?.subscriptionInfo))
                    .Where(p => p.Product != null && p.Product.definition.type == ProductType.Subscription)
                    .Select(p => p.subscriptionInfo)
                    .ToList();
            _confirmedSubscriptions.Clear();
            _confirmedSubscriptions.AddRange(confirmedSubscription);

            if (HasActiveSubscription())
            {
                StartSubscription();
            }
            else
            {
                EndSubscription();
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
            StoreController.ConfirmPurchase(order);
        }

        private void OnPurchaseConfirmed(Order order)
        {
            if (order is PendingOrder) return; //The order is still pending; it will be confirmed in OnPurchasePending, so we can skip processing here.
            var product = order.CartOrdered.Items().FirstOrDefault()?.Product;
            var id = product?.definition.id;
            var itemType = "Unknown";
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
                    itemType = "Energy";
                    _energyManager.ChangeEnergy(3, true, GAItemType.IAP, id);
                    break;
                case ProductIds.Energy3:
                    DebugUtils.Log("IAP: 5 energy granted.");
                    itemType = "Energy";
                    _energyManager.ChangeEnergy(5, true, GAItemType.IAP, id);
                    break;
                case ProductIds.MaxEnergy:
                    itemType = "Energy";
                    DebugUtils.Log("IAP: Max energy granted.");
                    _energyManager.ChangeEnergy(_energyManager.Config.MaxEnergy, true, GAItemType.IAP, id);
                    break;
                case ProductIds.MonthlyPass:
                case ProductIds.QuarterlyPass:
                case ProductIds.AnnuallyPass:
                    DebugUtils.Log($"IAP: {id} pass activated.");
                    itemType = "Subscription";
                    StartSubscription();
                    _confirmedSubscriptions.Add(order.Info.PurchasedProductInfo.FirstOrDefault()?.subscriptionInfo);
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
            var currency = product?.metadata.isoCurrencyCode ?? "USD";
            var priceDecimal = product?.metadata.localizedPrice ?? 0m;
            var amount = IapCurrencyHelper.GetAmountInMinorUnits(priceDecimal, currency);
            GameAnalytics.NewBusinessEvent(currency, amount, itemType, id, "Store");
            _onPurchaseSuccess?.OnNext(Unit.Default);
            
            //Save
            var saveObject = _saveManager.GetFirstSaveObjectOfType<PlayerRecordSaveObject>();
            if (!saveObject) return;
            var saveData = saveObject.GetSaveData<PlayerRecordSaveData>();
            if (saveData == null) return;
            if (saveData.HasPurchasedAtLeastOnce) return;
            saveData.HasPurchasedAtLeastOnce = true;
            _saveManager.Save(saveObject);
            _cloudSaveService.SaveToService(SaveToServiceParameters.Default).Forget();
        }

        private void OnPurchaseFailed(FailedOrder order)
        {
            var id = order.CartOrdered.Items().FirstOrDefault()?.Product.definition.id;
            var failureReason = order.FailureReason;
            DebugUtils.LogWarning($"IAP: Purchase failed: {id}, Reason: {failureReason}");
        }

        private void OnPurchaseDeferred(DeferredOrder order)
        {
            var id = order.CartOrdered.Items().FirstOrDefault()?.Product.definition.id;
            DebugUtils.Log($"IAP: Purchase deferred: {id}. Awaiting approval.");
        }

        private async UniTask<Entitlement> CheckEntitlement(Product product)
        {
            if (!IsIAPReady) return null;
            var tcs = new UniTaskCompletionSource<Entitlement>();
            _entitlementSubscription?.Dispose();
            _entitlementSubscription = Observable.FromEvent<Entitlement>(
                    handler => StoreController.OnCheckEntitlement += handler,
                    handler => StoreController.OnCheckEntitlement -= handler)
                .Subscribe(entitlement => OnCheckEntitlement(entitlement, tcs));
            StoreController.CheckEntitlement(product);
            var result = await tcs.Task;
            return result;
        }

        private void OnCheckEntitlement(Entitlement entitlement, UniTaskCompletionSource<Entitlement> tcs)
        { 
            var id = entitlement.Product?.definition.id;
            DebugUtils.Log($"IAP: Check entitlement: {id}");
            tcs.TrySetResult(entitlement);
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
                StoreController.PurchaseProduct(productId);
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
                .Where(x => x != null)
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
            EndSubscription();
        }

        private void StartSubscription()
        {
            UpdateAnalyticPlayerTier();
            _energyManager.SetInfiniteEnergy(true);
            _adsService.SetEnableStateAll(false);
            DebugUtils.Log("IAP: Subscription started. Infinite energy granted.");
        }

        private void EndSubscription()
        {
            UpdateAnalyticPlayerTier();
            _energyManager.SetInfiniteEnergy(false);
            _adsService.SetEnableStateAll(true);
            DebugUtils.Log("IAP: Subscription ended. Infinite energy revoked.");
        }

//         private SubscriptionInfo GetSubscriptionInfo(Product product, string receipt = null)
//         {
//             DebugUtils.Log($"Getting subscription info for: {product.definition.id} | receipt: {receipt}...");
//             var infoHelper = string.IsNullOrEmpty(receipt) ?
//                 new SubscriptionInfoHelper(product, null) : 
//                 new SubscriptionInfoHelper(receipt, product.definition.storeSpecificId, null);
//             try
//             {
//                 return infoHelper.GetSubscriptionInfo();
//             }
//             catch (StoreSubscriptionInfoNotSupportedException)
//             {
// #if UNITY_EDITOR
//                 // This works in editor but not in other platforms. Using this as fallback for testing.
//                 return new SubscriptionInfoHelper(product, null).GetSubscriptionInfo();
// #endif
//                 return null;
//             }
//         }
        
        public bool HasActiveSubscription()
        {
#if UNITY_EDITOR
            return _confirmedSubscriptions.Count > 0; // Since in editor, the subscription info is always null, we just assume that any confirmed subscription is active for testing purposes.
#endif
            return _confirmedSubscriptions.Any(x => x.IsSubscribed() is Result.True);
        }

        public bool IsInFreeTrial()
        {
#if UNITY_EDITOR
            return false; // Since in editor, the subscription info is always null, we cannot determine if it's in free trial or not. Returning false for testing purposes.
#endif
            return _confirmedSubscriptions.Any(x => x.IsFreeTrial() is Result.True);
        }

        #endregion

        #region Helpers

        public string GetLocalizedPrice(string productId)
        {
            if (!IsIAPReady) return string.Empty;
            var product = StoreController.GetProductById(productId);
            return product?.metadata.localizedPriceString ?? string.Empty;
        }

        private void UpdateAnalyticPlayerTier()
        {
            var playerSaveData = _saveManager.GetFirstSaveObjectOfType<PlayerRecordSaveObject>()
                .GetSaveData<PlayerRecordSaveData>();
            if (playerSaveData == null) return;
            var spender = playerSaveData.HasPurchasedAtLeastOnce;
            if (HasActiveSubscription())
            {
                GameAnalytics.SetCustomDimension01(GACustomDimension01.Premium);
            }
            else if (spender)
            {
                GameAnalytics.SetCustomDimension01(GACustomDimension01.Spender);
            }
            else
            {
                GameAnalytics.SetCustomDimension01(GACustomDimension01.F2P);
            }
        }
        #endregion
    }
}