using System;
using Cysharp.Threading.Tasks;
using QuackUp.Save;
using QuackUp.SocialService;
using R3;
using Sirenix.OdinInspector;
using VContainer;
using VContainer.Unity;

namespace FitMe.GameData
{
    public sealed class HintManager : IPostInitializable, IDisposable
    {
        private readonly HintManagerConfig _config;
        private readonly MessagePackSaveManager _saveManager;
        private readonly ICloudSaveService _cloudSaveService;
        private readonly ReactiveProperty<int> _currentHints = new(0);
        private readonly ReactiveProperty<bool> _isReady = new(false);
        private readonly ReadOnlyReactiveProperty<bool> _readOnlyReadiness;
        private HintManagerSaveObject _saveObject;
        public bool IsReady => _isReady.CurrentValue;
        public ReadOnlyReactiveProperty<int> CurrentHints => _currentHints;
        public ReadOnlyReactiveProperty<bool> Readiness => _readOnlyReadiness;

        [Inject]
        public HintManager(HintManagerConfig config, MessagePackSaveManager saveManager,
            ICloudSaveService cloudSaveService)
        {
            _config = config;
            _saveManager = saveManager;
            _cloudSaveService = cloudSaveService;
            _readOnlyReadiness = _isReady.ToReadOnlyReactiveProperty();
        }

        public void PostInitialize() => InitializeAsync().Forget();

        private async UniTaskVoid InitializeAsync()
        {
            await _saveManager.WaitForSaveDataReady;
            _saveObject = _saveManager.GetFirstSaveObjectOfType<HintManagerSaveObject>();
            var saveData = _saveObject.GetSaveData<HintManagerSaveData>();
            _currentHints.Value = Math.Max(0, saveData.CurrentHints);
            ApplyDailyReward(DateTime.Now);
            _isReady.Value = true;
        }

        [Button("Change Hints")]
        public void ChangeHints(int amount, bool allowOverflow = false)
        {
            if (!IsReady || amount == 0) return;
            if (amount > 0)
            {
                GrantHints(amount, allowOverflow);
                return;
            }

            var requested = -(long)amount;
            var spendAmount = (int)Math.Min(requested, _currentHints.Value);
            if (spendAmount > 0) TrySpendHints(spendAmount);
        }

        public bool TrySpendHints(int amount)
        {
            if (!IsReady) throw new InvalidOperationException("Hint save data is not ready.");
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (_currentHints.Value < amount) return false;
            SetBalance(_currentHints.Value - amount);
            return true;
        }

        public void GrantHints(int amount, bool allowOverflow)
        {
            if (!IsReady) throw new InvalidOperationException("Hint save data is not ready.");
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            var granted = allowOverflow
                ? amount
                : Math.Min(amount, Math.Max(0, _config.FreeHintCap - _currentHints.Value));
            if (granted == 0) return;
            SetBalance((int)Math.Min(int.MaxValue, (long)_currentHints.Value + granted));
        }

        private void ApplyDailyReward(DateTime localNow)
        {
            var saveData = _saveObject.GetSaveData<HintManagerSaveData>();
            if (!DailyHintRewardPolicy.TryResolve(localNow, _currentHints.Value, _config.FreeHintCap,
                    _config.DailyHintGrant, saveData.LastDailyHintCheckDate,
                    out var updatedHints, out var checkedDate)) return;

            saveData.CurrentHints = updatedHints;
            saveData.LastDailyHintCheckDate = checkedDate;
            if (updatedHints != _currentHints.Value)
                _currentHints.Value = updatedHints;
            _saveManager.SaveRequired(_saveObject);
            _cloudSaveService.SaveToService(SaveToServiceParameters.Default).Forget();
        }

        private void SetBalance(int value)
        {
            _currentHints.Value = value;
            var saveData = _saveObject.GetSaveData<HintManagerSaveData>();
            saveData.CurrentHints = value;
            _saveManager.SaveRequired(_saveObject);
            _cloudSaveService.SaveToService(SaveToServiceParameters.Default).Forget();
        }

        public void Dispose()
        {
            _readOnlyReadiness.Dispose();
            _isReady.Dispose();
            _currentHints.Dispose();
        }
    }
}
