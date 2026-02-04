using System;
using System.Collections.Generic;
using System.Linq;
using QuackUp.Save;
using QuackUp.Utils;
using R3;
using VContainer;

namespace FitMe.Achievement
{
    public struct AchievementInstance
    {
        public IDisposable subscription;
        public IAchievement achievement;
        
        public AchievementInstance(IDisposable subscription, IAchievement achievement)
        {
            this.subscription = subscription;
            this.achievement = achievement;
        }
    }
    public class AchievementManager : IDisposable
    {
        private readonly IReadOnlyList<AchievementPreset> _achievementPresets;
        private readonly MessagePackSaveManager _saveManager;
        private readonly Dictionary<string, AchievementInstance> _achievements = new();
        private Dictionary<string, AchievementPreset> _presetsById = new();
        
        private AchievementSaveObject _saveObject;
        
        [Inject]
        public AchievementManager(
            MessagePackSaveManager saveManager,
            IReadOnlyList<AchievementPreset> achievementPresets)
        {
            _saveManager = saveManager;
            _achievementPresets = achievementPresets;
            Initialize();
        }

        public void Dispose()
        {
            foreach (var achievement in _achievements.Values)
            {
                achievement.subscription.Dispose();
                if (achievement.achievement is IDisposable disposableAchievement) 
                {
                    disposableAchievement.Dispose();
                }
            }
        }

        private void Initialize()
        {
            _saveObject = _saveManager.GetFirstSaveObjectOfType<AchievementSaveObject>();
            if (!_saveObject) return;
            var saveData = _saveObject.GetSaveData<AchievementSaveData>();
            if (saveData == null) return;
            _presetsById = _achievementPresets.ToDictionary(x => x.AchievementId, x => x);
            var tempPresetsById = new Dictionary<string, AchievementPreset>(_presetsById);
            foreach (var saveAchievement in saveData.Achievements)
            {
                if (!tempPresetsById.TryGetValue(saveAchievement.Key, out var preset))
                {
                    DebugUtils.LogWarning($"AchievementManager: Achievement preset with ID {saveAchievement.Key} not found. Skipping.");
                    continue;
                }
                var achievement = preset.CrateAchievement(out _, saveAchievement.Value);
                if (achievement is null)
                {
                    DebugUtils.LogError($"AchievementManager: Failed to create achievement with ID {saveAchievement.Key}. Skipping.");
                    continue;
                }
                var subscription = achievement.SaveRequestCommand
                    .Subscribe(OnSaveRequested);
                var instance = new AchievementInstance(subscription, achievement);
                _achievements.Add(saveAchievement.Key, instance);
                tempPresetsById.Remove(saveAchievement.Key);
            }
            // Add remaining presets as new achievements
            foreach (var remainingPreset in tempPresetsById.Values)
            {
                var newAchievement = remainingPreset.CrateAchievement(out var achievementData);
                if (newAchievement is null)
                {
                    DebugUtils.LogError($"AchievementManager: Failed to create achievement with ID {remainingPreset.AchievementId}. Skipping.");
                    continue;
                }
                var subscription = newAchievement.SaveRequestCommand
                    .Subscribe(OnSaveRequested);
                var instance = new AchievementInstance(subscription, newAchievement);
                _achievements.Add(remainingPreset.AchievementId, instance);
                saveData.Achievements.Add(remainingPreset.AchievementId, achievementData);
            }
            DebugUtils.Log($"Number of achievements initialized: {_achievements.Count}");
        }

        public bool TryGetAchievement<T>(string achievementId, out IAchievement<T> achievement) where T : AchievementData
        {
            if (_achievements.TryGetValue(achievementId, out var foundAchievement))
            {
                if (foundAchievement.achievement is IAchievement<T> typedAchievement)
                {
                    achievement = typedAchievement;
                    return true;
                }
            }
            achievement = null;
            return false;
        }

        private void OnSaveRequested(IAchievement achievement)
        {
            if (!_saveObject) return;
            var id = achievement.BasePreset.AchievementId;
            var saveData = _saveObject.GetSaveData<AchievementSaveData>();
            if (saveData == null) return;
            if (achievement is not IAchievement<AchievementData> typedAchievement) return;
            saveData.Achievements[id] = typedAchievement.AchievementData;
            _saveManager.Save(_saveObject);
        }
    }
}