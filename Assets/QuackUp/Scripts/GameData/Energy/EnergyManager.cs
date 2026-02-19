using System;
using QuackUp.Save;
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
        public ReadOnlyReactiveProperty<int> CurrentEnergy => _currentEnergy;
        private readonly ReactiveProperty<int> _currentEnergy = new(0);
        [ShowInInspector] private int DebugCurrentEnergy => _currentEnergy.Value;
        private readonly MessagePackSaveManager _saveManager;
        private readonly EnergyManagerConfig _config;

        private EnergyManagerSaveObject _saveObject;
        private IDisposable _energyTimer;
        
        [Inject]
        public EnergyManager(
            EnergyManagerConfig config,
            MessagePackSaveManager saveManager)
        {
            _config = config;
            _saveManager = saveManager;
        }

        public void PostInitialize()
        {
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
            var saveData = _saveObject.GetSaveData<EnergyManagerSaveData>();
            _energyTimer = Observable.Interval(TimeSpan.FromSeconds(1)) //NOTE: Check every second as we do not need that much precision.
                .Subscribe(_ =>
                {
                    if (_currentEnergy.Value >= _config.MaxEnergy) return;
                    var timeDifference = DateTime.UtcNow - saveData.LastEnergyUpdateTime;
                    if (timeDifference < _config.EnergyRechargeTime) return;
                    var changeAmount = Mathf.FloorToInt((float)(timeDifference / _config.EnergyRechargeTime));
                    var remains = TimeSpan.FromSeconds(timeDifference.TotalSeconds % _config.EnergyRechargeTime.TimeSpan.TotalSeconds);
                    ChangeEnergy(changeAmount);
                    saveData.LastEnergyUpdateTime = DateTime.UtcNow - remains;
                    _saveManager.Save(_saveObject);
                });
        }

        [Button("Change Energy")]
        public void ChangeEnergy(int amount)
        {
            var newEnergy = Mathf.Clamp(_currentEnergy.Value + amount, 0, _config.MaxEnergy);
            var saveData = _saveObject.GetSaveData<EnergyManagerSaveData>();
            if (_currentEnergy.Value >= _config.MaxEnergy && newEnergy < _config.MaxEnergy)
            {
                saveData.LastEnergyUpdateTime = DateTime.UtcNow;
            }
            _currentEnergy.Value = newEnergy;
            saveData.CurrentEnergy = newEnergy;
            _saveManager.Save(_saveObject);
        }
    }
}