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

        private PlayerRecordSaveData _playerRecordSaveData; 
        private IDisposable _bindings;
        
        public CumulativeFitMeAchievement(
            PlayerRecordSaveData playerRecordSaveData,
            CumulativeFitMeAchievementPreset basePreset, 
            AchievementData data) 
            : base(basePreset, data)
        {
            _playerRecordSaveData = playerRecordSaveData;
            _bindings = Observable.EveryValueChanged(_playerRecordSaveData, x => x.cumulativeFitMe)
                .Subscribe(_ => Apply());
        }
        
        public void Dispose()
        {
            _bindings?.Dispose();
        }

        public override void Apply()
        {
            if (AchievementData.completed) return;
            if (_playerRecordSaveData.cumulativeFitMe < Preset.TargetCumulativeFitMe)
            {
                SaveAchievementData();
                return;
            }
            Complete();
        }

        public override Vector2 GetProgress()
        {
            return new Vector2(_playerRecordSaveData.cumulativeFitMe, Preset.TargetCumulativeFitMe);
        }
    }
}