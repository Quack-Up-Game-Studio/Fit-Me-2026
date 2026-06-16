using System;
using Cysharp.Threading.Tasks;
using FitMe.GameData;
using FitMe.Shared;
using QuackUp.Save;
using QuackUp.SocialService;
using QuackUp.Utils;
using R3;
using Sirenix.OdinInspector;
using UniLabs.Time;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Panel
{
    [Serializable]
    public class OutOfEnergyManager : IStartable, IDisposable
    {
        public ReactiveCommand<Promise<Unit>> TransitionInCommand { get; } = new();
        public ReactiveCommand<Promise<Unit>> TransitionOutCommand { get; } = new();
        
        private readonly MessagePackSaveManager _saveManager;
        private readonly AdsService _adsService;
        private readonly EnergyManager  _energyManager;
        private readonly TimeSpan _adCooldownTime;
        private readonly ICloudSaveService _cloudSaveService;
        private readonly int _maxAdCount;
        public const string AdCoolDownTimeKey = "AdCoolDownTime";
        public const string AdCountKey = "AdCount";
        
        private IDisposable _onRewardEarned;
        private IDisposable _adsTimer;
        
        public ReadOnlyReactiveProperty<int> RemainingAdCount => _remainingAdCount.ToReadOnlyReactiveProperty();
        private readonly ReactiveProperty<int> _remainingAdCount = new();
        public ReadOnlyReactiveProperty<TimeSpan> TimeUntilNextWatchAd => _timeUntilNextWatchAd.ToReadOnlyReactiveProperty();
        private readonly ReactiveProperty<TimeSpan> _timeUntilNextWatchAd = new();
        public Observable<Unit> OnToShop => _onToShop;
        private readonly Subject<Unit> _onToShop = new();
        public int MaxAdCount => _maxAdCount;
        
        [ShowInInspector] private int DebugRemainingAdCount => _remainingAdCount.Value;
        [ShowInInspector] private UTimeSpan DebugTimeUntilNextWatchAd => _timeUntilNextWatchAd.Value;
        
        private PlayerRecordSaveObject _saveObject;

        public  bool OpenShopAcrossScene { get; set; }

        [Button(nameof(TestTransition))]
        private void TestTransition(bool direction)
        {
            if (direction)
            {
                TransitionInCommand.Execute(new Promise<Unit>());
            }
            else
            { 
                TransitionOutCommand.Execute(new Promise<Unit>());
            }
        }

        [Inject]
        public OutOfEnergyManager(
            MessagePackSaveManager saveManager,
            AdsService adsService,
            EnergyManager energyManager,
            ICloudSaveService cloudSaveService,
            [Key(AdCoolDownTimeKey)] TimeSpan adCooldownTime, 
            [Key(AdCountKey)] int maxAdCount)
        {
            _saveManager = saveManager;
            _adsService = adsService;
            _energyManager = energyManager;
            _cloudSaveService = cloudSaveService;
            _adCooldownTime = adCooldownTime;
            _maxAdCount = maxAdCount;
        }

        public void Start()
        {
            StartAsync().Forget();
        }

        private async UniTaskVoid StartAsync()
        {
            await _saveManager.WaitForSaveDataReady;
            _saveObject = _saveManager.GetFirstSaveObjectOfType<PlayerRecordSaveObject>();
            var saveData = _saveObject.GetSaveData<PlayerRecordSaveData>();
            _remainingAdCount.Value = saveData.IsFirstTimePlayer ? _maxAdCount : saveData.CurrentRemainingAd;
            _saveManager.Save(_saveObject);
            _cloudSaveService.SaveToService(SaveToServiceParameters.Default).Forget();
            StartAdsTimer();
        }

        private void StartAdsTimer()
        {
            _adsTimer = Observable.Interval(TimeSpan.FromSeconds(1)) //NOTE: Check every second as we do not need that much precision.
                .Subscribe(_ =>
                {
                    if (_remainingAdCount.Value >= _maxAdCount)
                    {
                        _timeUntilNextWatchAd.Value = TimeSpan.Zero;
                        return;
                    }
                    var saveData = _saveObject.GetSaveData<PlayerRecordSaveData>();
                    var timeDifference = DateTime.UtcNow - saveData.LastWatchAdTimeStamp;
                    if (timeDifference < _adCooldownTime)
                    {
                        _timeUntilNextWatchAd.Value = _adCooldownTime - timeDifference;
                        return;
                    }
                    _timeUntilNextWatchAd.Value = _adCooldownTime;
                    var changeAmount = Mathf.FloorToInt((float)(timeDifference / _adCooldownTime));
                    var remains = TimeSpan.FromSeconds(timeDifference.TotalSeconds % _adCooldownTime.TotalSeconds);
                    saveData.LastWatchAdTimeStamp = DateTime.UtcNow - remains;
                    ChangeRemainingAdCount(changeAmount);
                });
        }

        [Button(nameof(ChangeRemainingAdCount))]
        public void ChangeRemainingAdCount(int amount)
        {
            if (amount == 0) return;
            var newValue = Mathf.Clamp(_remainingAdCount.Value + amount, 0, _maxAdCount);
            var saveData = _saveObject.GetSaveData<PlayerRecordSaveData>();
            if (_remainingAdCount.Value >= _maxAdCount && newValue < _maxAdCount)
            {
                saveData.LastWatchAdTimeStamp = DateTime.UtcNow;
            }
            _remainingAdCount.Value = newValue;
            saveData.CurrentRemainingAd = newValue;
            _saveManager.Save(_saveObject);
            _cloudSaveService.SaveToService(SaveToServiceParameters.Default);
        }
        
        public void WatchAds()
        {
            if (_remainingAdCount.Value <= 0) return;
            if (!_adsService.TryGetAdsInstance<RewardedAdInstance>(out var rewardedAdInstance)) return;
            if (!rewardedAdInstance.Enabled) return;
            _onRewardEarned?.Dispose();
            _onRewardEarned = rewardedAdInstance.OnUserEarnedReward
                .Subscribe(_ => OnRewardEarned());
            rewardedAdInstance.AdContext = GAAdContext.RefillEnergy;
            rewardedAdInstance.TryShow();
        }

        public void ToShop()
        {
            _onToShop?.OnNext(Unit.Default);
            TransitionOutCommand.Execute(new Promise<Unit>());
        }

        private void OnRewardEarned()
        {
            ChangeRemainingAdCount(-1);
            _energyManager.ChangeEnergy(1, itemType: GAItemType.Ads, itemId: GAItemId.OutOfEnergyAds);
            TransitionOutCommand.Execute(new Promise<Unit>());
            _onRewardEarned?.Dispose();
        }
        
        public void Dispose()
        {
            _onRewardEarned?.Dispose();
            _adsTimer?.Dispose();
        }
    }
}