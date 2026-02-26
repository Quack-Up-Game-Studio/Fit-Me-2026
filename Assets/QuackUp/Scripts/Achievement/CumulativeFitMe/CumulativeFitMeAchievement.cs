using System;
using FitMe.GameData;
using MessagePack;
using R3;
using UnityEngine;

namespace FitMe.Achievement
{
    [Union(0, typeof(CumulativeFitMeAchievementData))]
    public partial record AchievementData;
    
    [Serializable]
    [MessagePackObject]
    public record CumulativeFitMeAchievementData : AchievementData
    {
        // Parameterless constructor for serialization
        public CumulativeFitMeAchievementData() : base(false)
        {
        }
    }

    [Serializable]
    public class CumulativeFitMeAchievement : Achievement<CumulativeFitMeAchievementData>, IDisposable
    {
        public CumulativeFitMeAchievementPreset Preset => (CumulativeFitMeAchievementPreset)BasePreset;

        private PlayerRecordSaveObject _playerRecordSaveObject;
        private IDisposable _subscriptions;
        private IDisposable _listener;
        
        public CumulativeFitMeAchievement(
            PlayerRecordSaveObject playerRecordSaveObject,
            CumulativeFitMeAchievementPreset basePreset, 
            AchievementData data) 
            : base(basePreset, data)
        {
            _playerRecordSaveObject = playerRecordSaveObject;
            Subscribe();
        }

        private void Subscribe()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            _playerRecordSaveObject.OnLoadCompleteEvent
                .Subscribe(_ => OnLoadCompleteEvent())
                .AddTo(ref disposableBuilder);
            _subscriptions = disposableBuilder.Build();
        }

        private void OnLoadCompleteEvent()
        {
            _listener?.Dispose();
            var saveData = _playerRecordSaveObject.GetSaveData<PlayerRecordSaveData>();
            _listener = Observable.EveryValueChanged(saveData, x => x.cumulativeFitMe)
                .Subscribe(_ => Apply());
        }
        
        public void Dispose()
        {
            _subscriptions?.Dispose();
            _listener?.Dispose();
        }

        public override void Apply()
        {
            if (AchievementData.completed) return;
            var saveData = _playerRecordSaveObject.GetSaveData<PlayerRecordSaveData>();
            if (saveData.cumulativeFitMe < Preset.TargetCumulativeFitMe)
            {
                SaveAchievementData();
                return;
            }
            Complete();
        }

        public override Vector2 GetProgress()
        {
            var saveData = _playerRecordSaveObject.GetSaveData<PlayerRecordSaveData>();
            return new Vector2(saveData.cumulativeFitMe, Preset.TargetCumulativeFitMe);
        }
    }
}