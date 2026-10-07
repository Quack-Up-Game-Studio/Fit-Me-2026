using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using FitMe.GameData;
using FitMe.Shared;
using QuackUp.Analytics;
using QuackUp.GoogleAdMob;
using QuackUp.Save;
using QuackUp.SocialService;
using QuackUp.Utils;
using UnityEngine.Purchasing;

namespace QuackUp.IAP
{
    public interface IStorePurchaseEffects
    {
        // Implementations must deduplicate non-idempotent grants by transactionId across save/reload.
        void ApplyProductEffects(Order order, string transactionId);
        void ApplySubscriptionState(bool active);
    }

    public interface IPurchasePersistenceAndAnalytics
    {
        UniTask WaitForSaveDataReady { get; }
        bool IsPurchaseCompleted(string transactionId);
        void MarkPurchaseCompleted(string transactionId);
        UniTask RecordPurchase(Order order, string transactionId);
        void UpdatePlayerTier(bool activeSubscription);
    }

    public sealed class StorePurchaseEffects : IStorePurchaseEffects
    {
        private readonly EnergyManager _energyManager;
        private readonly AdsService _adsService;

        public StorePurchaseEffects(EnergyManager energyManager, AdsService adsService)
        {
            _energyManager = energyManager;
            _adsService = adsService;
        }

        public void ApplyProductEffects(Order order, string transactionId)
        {
            var id = order.CartOrdered.Items().FirstOrDefault()?.Product?.definition.id;
            switch (id)
            {
                case ProductIds.Energy2: _energyManager.ApplyPurchaseEnergy(transactionId, 3, GAItemType.IAP, id); break;
                case ProductIds.Energy3: _energyManager.ApplyPurchaseEnergy(transactionId, 5, GAItemType.IAP, id); break;
                case ProductIds.MaxEnergy: _energyManager.ApplyPurchaseEnergy(transactionId, _energyManager.Config.MaxEnergy, GAItemType.IAP, id); break;
            }
        }

        public void ApplySubscriptionState(bool active)
        {
            _energyManager.SetInfiniteEnergy(active);
            _adsService.SetEnableStateAll(!active);
        }
    }

    public sealed class PurchasePersistenceAndAnalytics : IPurchasePersistenceAndAnalytics
    {
        private readonly MessagePackSaveManager _saveManager;
        private readonly ICloudSaveService _cloudSaveService;
        private readonly IAnalyticsService _analyticsService;
        private readonly HashSet<string> _purchaseAnalyticsAttemptedTransactions = new(StringComparer.Ordinal);
        private string _pendingFirstPurchaseCloudSyncTransaction;
        private bool _firstPurchaseCloudSyncCompleted;

        public PurchasePersistenceAndAnalytics(MessagePackSaveManager saveManager,
            ICloudSaveService cloudSaveService, IAnalyticsService analyticsService)
        {
            _saveManager = saveManager;
            _cloudSaveService = cloudSaveService;
            _analyticsService = analyticsService;
        }

        public UniTask WaitForSaveDataReady => _saveManager.WaitForSaveDataReady;

        public bool IsPurchaseCompleted(string transactionId)
        {
            var saveObject = _saveManager.GetFirstSaveObjectOfType<PlayerRecordSaveObject>();
            var saveData = saveObject ? saveObject.GetSaveData<PlayerRecordSaveData>() : null;
            return saveData?.IsPurchaseCompleted(transactionId) == true;
        }

        public void MarkPurchaseCompleted(string transactionId)
        {
            var saveObject = _saveManager.GetFirstSaveObjectOfType<PlayerRecordSaveObject>();
            var saveData = saveObject ? saveObject.GetSaveData<PlayerRecordSaveData>() : null;
            if (saveData == null)
                throw new InvalidOperationException("Player purchase save data is unavailable.");
            if (!saveData.TryMarkPurchaseCompleted(transactionId)) return;
            try
            {
                _saveManager.SaveRequired(saveObject);
            }
            catch
            {
                // Only a persisted completion may veto a later confirmation retry.
                saveData.CompletedPurchaseTransactionIds.Remove(transactionId);
                throw;
            }
        }

        public async UniTask RecordPurchase(Order order, string transactionId)
        {
            var product = order.CartOrdered.Items().FirstOrDefault()?.Product;
            var id = product?.definition.id;
            var saveObject = _saveManager.GetFirstSaveObjectOfType<PlayerRecordSaveObject>();
            if (saveObject)
            {
                var saveData = saveObject.GetSaveData<PlayerRecordSaveData>();
                if (saveData != null)
                {
                    var isFirstPurchase = !saveData.HasPurchasedAtLeastOnce;
                    var isNewTransaction = saveData.TryMarkPurchaseRecorded(transactionId);
                    if (isFirstPurchase)
                    {
                        saveData.HasPurchasedAtLeastOnce = true;
                        _pendingFirstPurchaseCloudSyncTransaction = transactionId;
                        _firstPurchaseCloudSyncCompleted = false;
                    }

                    if (isNewTransaction || isFirstPurchase)
                        _saveManager.SaveRequired(saveObject);

                    if (_pendingFirstPurchaseCloudSyncTransaction == transactionId && !_firstPurchaseCloudSyncCompleted)
                    {
                        await _cloudSaveService.SaveToService(SaveToServiceParameters.Default);
                        _firstPurchaseCloudSyncCompleted = true;
                    }

                    if (saveData.TryMarkPurchaseAnalyticsAttempted(transactionId))
                    {
                        try
                        {
                            _saveManager.SaveRequired(saveObject);
                        }
                        catch
                        {
                            saveData.TryUnmarkPurchaseAnalyticsAttempted(transactionId);
                            throw;
                        }
                        TrackPurchaseAnalytics(product, id, transactionId);
                    }
                    return;
                }
            }

            if (_purchaseAnalyticsAttemptedTransactions.Add(transactionId))
                TrackPurchaseAnalytics(product, id, transactionId);
        }

        private void TrackPurchaseAnalytics(Product product, string id, string transactionId)
        {
            var currency = product?.metadata.isoCurrencyCode ?? "USD";
            var amount = IapCurrencyHelper.GetAmountInMinorUnits(product?.metadata.localizedPrice ?? 0m, currency);
            try
            {
                _analyticsService.TrackBusinessEvent(currency, amount,
                    product?.definition.type.ToString() ?? "Unknown", id, GACartType.Store);
            }
            catch (Exception exception)
            {
                DebugUtils.LogError($"IAP: Purchase analytics failed for transaction {transactionId}: {exception}");
            }
        }

        public void UpdatePlayerTier(bool activeSubscription)
        {
            var saveObject = _saveManager.GetFirstSaveObjectOfType<PlayerRecordSaveObject>();
            var playerSaveData = saveObject ? saveObject.GetSaveData<PlayerRecordSaveData>() : null;
            if (playerSaveData == null) return;
            _analyticsService.SetPlayerTier(activeSubscription ? GACustomDimension01.Premium :
                playerSaveData.HasPurchasedAtLeastOnce ? GACustomDimension01.Spender : GACustomDimension01.F2P);
        }
    }
}
