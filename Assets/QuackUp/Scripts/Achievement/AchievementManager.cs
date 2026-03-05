using System;
using System.Collections.Generic;
using System.Linq;
using FitMe.Shared;
using MessagePipe;
using QuackUp.Save;
using QuackUp.SocialService;
using QuackUp.Utils;
using R3;
using VContainer;
using VContainer.Unity;

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
    public class AchievementManager : IStartable, IDisposable
    {
        private readonly MessagePackSaveManager _saveManager;
        private readonly AchievementManagerConfig _config;
        private readonly IPublisher<NotificationDisplayEvent> _notificationDisplayPublisher;
        private readonly ICloudSaveService _cloudSaveService;
        private readonly Dictionary<string, AchievementInstance> _achievements = new();
        private Dictionary<string, AchievementPreset> _presetsById = new();
        
        private AchievementSaveObject _saveObject;
        private IDisposable _resetSubscription;
        
        public IReadOnlyDictionary<string, AchievementInstance> Achievements => _achievements;
        
        [Inject]
        public AchievementManager(
            MessagePackSaveManager saveManager,
            AchievementManagerConfig config,
            IPublisher<NotificationDisplayEvent> notificationDisplayPublisher,
            ICloudSaveService cloudSaveService)
        {
            _saveManager = saveManager;
            _config = config;
            _notificationDisplayPublisher = notificationDisplayPublisher;
            _cloudSaveService = cloudSaveService;
        }

        public void Dispose()
        {
            _resetSubscription?.Dispose();
            foreach (var achievement in _achievements.Values)
            {
                achievement.subscription.Dispose();
                if (achievement.achievement is IDisposable disposableAchievement) 
                {
                    disposableAchievement.Dispose();
                }
            }
        }
        
        public void Start()
        {
            _saveObject = _saveManager.GetFirstSaveObjectOfType<AchievementSaveObject>();
            if (!_saveObject)
            {
                DebugUtils.LogError($"Save object of type {nameof(AchievementSaveObject)} not found. AchievementManager will not function properly.");
                return;
            }
            _resetSubscription = _saveObject.OnResetEvent
                .Subscribe(_ => RegisterAchievements());
            RegisterAchievements();
        }

        private void RegisterAchievements()
        {
            var saveData = _saveObject.GetSaveData<AchievementSaveData>();
            if (saveData == null) return;
            _achievements.Clear();
            _presetsById = _config.AchievementPresets.ToDictionary(x => x.AchievementId, x => x);
            var tempPresetsById = new Dictionary<string, AchievementPreset>(_presetsById);
            foreach (var saveAchievement in saveData.Achievements)
            {
                if (!tempPresetsById.TryGetValue(saveAchievement.Key, out var preset))
                {
                    DebugUtils.LogWarning($"AchievementManager: Achievement preset with ID {saveAchievement.Key} not found. Skipping.");
                    continue;
                }
                var instance = CreateAchievementInstance(preset, out _, saveAchievement.Value);
                if (instance is null) continue;
                _achievements.Add(saveAchievement.Key, instance.Value);
                tempPresetsById.Remove(saveAchievement.Key);
            }
            // Add remaining presets as new achievements
            foreach (var remainingPreset in tempPresetsById.Values)
            {
                var instance = CreateAchievementInstance(remainingPreset, out var achievementData);
                if (instance is null) continue;
                _achievements.Add(remainingPreset.AchievementId, instance.Value);
                saveData.Achievements.Add(remainingPreset.AchievementId, achievementData);
            }
            DebugUtils.Log($"Number of achievements initialized: {_achievements.Count}");
        }

        private AchievementInstance? CreateAchievementInstance(AchievementPreset preset, out AchievementData achievementData, AchievementData dataFromSave = null)
        {
            var newAchievement = preset.CrateAchievement(out achievementData, dataFromSave);
            if (newAchievement is null)
            {
                DebugUtils.LogError($"AchievementManager: Failed to create achievement with ID {preset.AchievementId}. Skipping.");
                return null;
            }
            var disposableBuilder = Disposable.CreateBuilder();
            newAchievement.SaveRequestCommand
                .Subscribe(OnSaveRequested)
                .AddTo(ref disposableBuilder);
            newAchievement.OnComplete
                .Subscribe(OnComplete)
                .AddTo(ref disposableBuilder);
            var subscription = disposableBuilder.Build();
            var instance = new AchievementInstance(subscription, newAchievement);
            return instance;
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

        private void OnComplete(IAchievement achievement)
        {
            _notificationDisplayPublisher.Publish(new NotificationDisplayEvent(NotificationType.Challenge, new AchievementNotificationData(achievement)));
            _cloudSaveService.SaveToService(SaveToServiceParameters.Default);
        }
    }
}