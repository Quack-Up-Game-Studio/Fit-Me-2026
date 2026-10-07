using System.Linq;
using FitMe.GameData;
using FitMe.Shared;
using QuackUp.Analytics;
using QuackUp.GoogleAdMob;
using QuackUp.Save;
using QuackUp.SocialService;
using UnityEngine.Purchasing;

namespace QuackUp.IAP
{
    public interface IStorePurchaseEffects
    {
        void ApplyProductEffects(Order order);
        void ApplySubscriptionState(bool active);
    }

    public interface IPurchasePersistenceAndAnalytics
    {
        void RecordPurchase(Order order);
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

        public void ApplyProductEffects(Order order)
        {
            var id = order.CartOrdered.Items().FirstOrDefault()?.Product?.definition.id;
            switch (id)
            {
                case ProductIds.Energy2: _energyManager.ChangeEnergy(3, true, GAItemType.IAP, id); break;
                case ProductIds.Energy3: _energyManager.ChangeEnergy(5, true, GAItemType.IAP, id); break;
                case ProductIds.MaxEnergy: _energyManager.ChangeEnergy(_energyManager.Config.MaxEnergy, true, GAItemType.IAP, id); break;
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

        public PurchasePersistenceAndAnalytics(MessagePackSaveManager saveManager,
            ICloudSaveService cloudSaveService, IAnalyticsService analyticsService)
        {
            _saveManager = saveManager;
            _cloudSaveService = cloudSaveService;
            _analyticsService = analyticsService;
        }

        public void RecordPurchase(Order order)
        {
            var product = order.CartOrdered.Items().FirstOrDefault()?.Product;
            var id = product?.definition.id;
            var saveObject = _saveManager.GetFirstSaveObjectOfType<PlayerRecordSaveObject>();
            if (saveObject)
            {
                var saveData = saveObject.GetSaveData<PlayerRecordSaveData>();
                if (saveData is { HasPurchasedAtLeastOnce: false })
                {
                    saveData.HasPurchasedAtLeastOnce = true;
                    _saveManager.Save(saveObject);
                    _ = _cloudSaveService.SaveToService(SaveToServiceParameters.Default);
                }
            }

            var currency = product?.metadata.isoCurrencyCode ?? "USD";
            var amount = IapCurrencyHelper.GetAmountInMinorUnits(product?.metadata.localizedPrice ?? 0m, currency);
            _analyticsService.TrackBusinessEvent(currency, amount,
                product?.definition.type.ToString() ?? "Unknown", id, GACartType.Store);
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
