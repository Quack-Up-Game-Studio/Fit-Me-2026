using System;
using FitMe.GameData;
using MessagePack;
using QuackUp.Utils;
using R3;
using UnityEngine;

namespace FitMe.Achievement
{
    [Union(1, typeof(CumulativeScoreAchievementData))]
    public partial record AchievementData;
    
    [Serializable]
    [MessagePackObject]
    public record CumulativeScoreAchievementData : AchievementData
    {
        // Parameterless constructor for serialization
        public CumulativeScoreAchievementData() : base(false)
        {
        }
    }

    [Serializable]
    public class CumulativeScoreAchievement : Achievement<CumulativeScoreAchievementData>, IDisposable
    {
        public CumulativeScoreAchievementPreset Preset => (CumulativeScoreAchievementPreset)BasePreset;
        
        private PlayerRecordSaveObject _playerRecordSaveObject;
        private IDisposable _subscriptions;
        private IDisposable _listener;
        
        public CumulativeScoreAchievement(
            PlayerRecordSaveObject playerRecordSaveObject,
            CumulativeScoreAchievementPreset basePreset, 
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
            _listener = Observable.EveryValueChanged(saveData, x => x.cumulativeScore)
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
            if (saveData.cumulativeScore < Preset.TargetCumulativeScore)
            {
                SaveAchievementData();
                DebugUtils.Log("CumulativeScoreAchievement not completed yet. Current: " + saveData.cumulativeScore + ", Target: " + Preset.TargetCumulativeScore);
                return;
            }
            Complete();
            DebugUtils.Log("CumulativeScoreAchievement completed! Cumulative Score: " + saveData.cumulativeScore);
        }

        public override Vector2 GetProgress()
        {
            var saveData = _playerRecordSaveObject.GetSaveData<PlayerRecordSaveData>();
            return new Vector2(saveData.cumulativeScore, Preset.TargetCumulativeScore);
        }
    }
}