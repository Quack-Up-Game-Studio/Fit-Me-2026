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
        private PlayerRecordSaveData _playerRecordSaveData;
        private IDisposable _bindings;
        
        public CumulativeScoreAchievement(
            PlayerRecordSaveData playerRecordSaveData, 
            CumulativeScoreAchievementPreset basePreset, 
            AchievementData data) 
            : base(basePreset, data)
        {
            _playerRecordSaveData = playerRecordSaveData;
            _bindings = Observable.EveryValueChanged(_playerRecordSaveData, x => x.cumulativeScore)
                .Subscribe(_ => Apply());
        }

        public void Dispose()
        {
            _bindings?.Dispose();
        }

        public override void Apply()
        {
            if (AchievementData.completed) return;
            if (_playerRecordSaveData.cumulativeScore < Preset.TargetCumulativeScore)
            {
                SaveAchievementData();
                DebugUtils.Log("CumulativeScoreAchievement not completed yet. Current: " + _playerRecordSaveData.cumulativeScore + ", Target: " + Preset.TargetCumulativeScore);
                return;
            }
            Complete();
            DebugUtils.Log("CumulativeScoreAchievement completed! Cumulative Score: " + _playerRecordSaveData.cumulativeScore);
        }

        public override Vector2 GetProgress()
        {
            return new Vector2(_playerRecordSaveData.cumulativeScore, Preset.TargetCumulativeScore);
        }
    }
}