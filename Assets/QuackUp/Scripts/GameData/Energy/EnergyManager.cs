using System;
using Cysharp.Threading.Tasks;
using FitMe.Shared;
using MessagePipe;
using QuackUp.Analytics;
using QuackUp.Save;
using QuackUp.SocialService;
using QuackUp.Utils;
using R3;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.GameData
{
    [Serializable]
    public class EnergyManager : IPostInitializable, IDisposable
    {
        /// <remarks>
        /// Use <see cref="ChangeEnergy"/> to change the energy value.
        /// </remarks>
        public ReadOnlyReactiveProperty<int> CurrentEnergy => _currentEnergy.ToReadOnlyReactiveProperty();
        /// <remarks>
        /// Use <see cref="SetInfiniteEnergy"/> to change the infinite energy state.
        /// </remarks>
        public ReadOnlyReactiveProperty<bool> InfiniteEnergy => _infiniteEnergy.ToReadOnlyReactiveProperty();
        public ReadOnlyReactiveProperty<TimeSpan> TimeUntilNextRecharge => _timeUntilNextRecharge;
        private readonly ReactiveProperty<TimeSpan> _timeUntilNextRecharge = new(TimeSpan.Zero);
        private readonly ReactiveProperty<int> _currentEnergy = new(0);
        private readonly ReactiveProperty<bool> _infiniteEnergy = new(false);
        [ShowInInspector] private int DebugCurrentEnergy => _currentEnergy.Value;
        private readonly MessagePackSaveManager _saveManager;
        private readonly EnergyManagerConfig _config;
        private readonly ICloudSaveService _cloudSaveService;
        private readonly IMessageHub _notificationMessageHub;
        private readonly IAnalyticsService _analyticsService;
        
        public EnergyManagerConfig Config => _config;

        private EnergyManagerSaveObject _saveObject;
        private IDisposable _energyTimer;
        
        [Inject]
        public EnergyManager(
            EnergyManagerConfig config,
            MessagePackSaveManager saveManager,
            ICloudSaveService cloudSaveService,
            [Key(NotificationMessageHub.MessageHubKey)] IMessageHub notificationMessageHub,
            IAnalyticsService analyticsService)
        {
            _config = config;
            _saveManager = saveManager;
            _cloudSaveService = cloudSaveService;
            _notificationMessageHub = notificationMessageHub;
            _analyticsService = analyticsService;
        }

        public void PostInitialize()
        {
            InitializeAsync().Forget();
        }

        private async UniTaskVoid InitializeAsync()
        {
            await _saveManager.WaitForSaveDataReady;
            _saveObject = _saveManager.GetFirstSaveObjectOfType<EnergyManagerSaveObject>();
            var saveData = _saveObject.GetSaveData<EnergyManagerSaveData>();
            var playerSaveData = _saveManager.GetFirstSaveObjectOfType<PlayerRecordSaveObject>().GetSaveData<PlayerRecordSaveData>();
            _currentEnergy.Value = playerSaveData.IsFirstTimePlayer ? _config.MaxEnergy : saveData.CurrentEnergy;
            _saveManager.Save(_saveObject);
            StartEnergyTimer();
        }

        public void Dispose()
        {
            _energyTimer?.Dispose();
        }
        
        private void StartEnergyTimer()
        {
            _energyTimer = Observable.Interval(TimeSpan.FromSeconds(1)) //NOTE: Check every second as we do not need that much precision.
                .Subscribe(_ =>
                {
                    if (_currentEnergy.Value >= _config.MaxEnergy)
                    {
                        _timeUntilNextRecharge.Value = TimeSpan.Zero;
                        return;
                    }
                    var saveData = _saveObject.GetSaveData<EnergyManagerSaveData>();
                    var timeDifference = DateTime.UtcNow - saveData.LastEnergyUpdateTime;
                    if (timeDifference < _config.EnergyRechargeTime)
                    {
                        _timeUntilNextRecharge.Value = _config.EnergyRechargeTime - timeDifference;
                        return;
                    }
                    _timeUntilNextRecharge.Value = _config.EnergyRechargeTime;
                    var changeAmount = Mathf.FloorToInt((float)(timeDifference / _config.EnergyRechargeTime));
                    var remains = TimeSpan.FromSeconds(timeDifference.TotalSeconds % _config.EnergyRechargeTime.TimeSpan.TotalSeconds);
                    saveData.LastEnergyUpdateTime = DateTime.UtcNow - remains;
                    ChangeEnergy(changeAmount, itemType: GAItemType.Recharge, itemId: GAItemId.PassiveRefill);
                });
        }

        [Button("Change Energy")]
        public void ChangeEnergy(int amount, bool allowOverflow = false, string itemType = "Unknown", string itemId = "Unknown")
        {
            if (amount < 0 && _infiniteEnergy.Value)
            {
                return;
            }
            if (amount <= 0) allowOverflow = true; //NOTE: Negative or zero change amount will always allow overflow 
            // as not doing so will make player lose the overflow energy due to the condition below.
            if (!allowOverflow)
            {
                amount = _currentEnergy.Value + amount > _config.MaxEnergy ? 
                    _config.MaxEnergy - _currentEnergy.Value : 
                    amount;
            }
            if (amount == 0) return; //Prevent unnecessary save operation.
            var newEnergy = Mathf.Clamp(_currentEnergy.Value + amount, 0, int.MaxValue); //Allow energy overflow
            var saveData = _saveObject.GetSaveData<EnergyManagerSaveData>();
            if (_currentEnergy.Value >= _config.MaxEnergy && newEnergy < _config.MaxEnergy)
            {
                saveData.LastEnergyUpdateTime = DateTime.UtcNow;
            }
            _currentEnergy.Value = newEnergy;
            saveData.CurrentEnergy = newEnergy;
            _saveManager.Save(_saveObject);
            _cloudSaveService.SaveToService(SaveToServiceParameters.Default);
            
            /* Analytics */
            var absoluteAmount = Mathf.Abs(amount);
            var flowType = amount > 0 ? ResourceFlowType.Source : ResourceFlowType.Sink;
            _analyticsService.TrackResourceFlow(flowType, GACurrency.Energy, absoluteAmount, itemType, itemId);
        }

        public bool ApplyPurchaseEnergy(string transactionId, int amount, string itemType, string itemId)
        {
            var saveData = _saveObject.GetSaveData<EnergyManagerSaveData>();
            var applied = saveData.TryApplyPurchaseGrant(transactionId, amount);

            _currentEnergy.Value = saveData.CurrentEnergy;
            // A transaction marker prevents another grant, not a retry of a failed write.
            _saveManager.SaveRequired(_saveObject);
            _cloudSaveService.SaveToService(SaveToServiceParameters.Default);
            if (!applied) return false;

            try
            {
                _analyticsService.TrackResourceFlow(ResourceFlowType.Source, GACurrency.Energy, amount, itemType, itemId);
            }
            catch (Exception exception)
            {
                DebugUtils.LogError($"Energy purchase analytics failed for transaction {transactionId}: {exception}");
            }

            return true;
        }
        
        public bool HasEnoughEnergy(uint amount)
        {
            if (_infiniteEnergy.Value) return true;
            return amount <= _currentEnergy.Value;
        }
        
        [Button("Set Infinite Energy")]
        public void SetInfiniteEnergy(bool value)
        {
            _infiniteEnergy.Value = value;
        }


        public async UniTask ShowNotEnoughEnergyNotification()
        {
            var promise = new Promise<Unit>();
            _notificationMessageHub.Publish(new NotificationDisplayEvent(
                NotificationType.General, 
                new GeneralNotificationData 
                { 
                    message = _config.NotEnoughEnergyMessage,
                    icon = _config.NotEnoughEnergySprite
                },
                promise));
            await promise.Task;
        }
    }
}