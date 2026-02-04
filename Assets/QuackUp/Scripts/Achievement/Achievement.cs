using System;
using MessagePack;
using QuackUp.Utils;
using R3;
using Sirenix.OdinInspector;
using UnityEngine;

namespace FitMe.Achievement
{
    public interface IAchievement
    {
        AchievementPreset BasePreset { get; }
        void Apply();
        Vector2 GetProgress();
        void Complete();
        public ReactiveCommand<IAchievement> SaveRequestCommand { get; }
    }
    
    public interface IAchievement<out T> : IAchievement where T : AchievementData
    {
        T AchievementData { get; }
    }

    [Serializable]
    [MessagePackObject]
    public abstract partial record AchievementData
    {
        [Key("Completed")]
        public bool completed;
        public AchievementData(bool completed)
        {
            this.completed = completed;
        }
    }

    [Serializable]
    public abstract class Achievement<T> : IAchievement<T> where T : AchievementData
    {
        [field: SerializeField, ReadOnly] public T AchievementData { get; private set; }
        
        [Button("Test Complete Achievement")]
        [HideInEditorMode]
        private void TestCompleteAchievement()
        {
            Complete();
        }

        public ReactiveCommand<IAchievement> SaveRequestCommand { get; } = new();
        public AchievementPreset BasePreset { get; private set; }
        
        protected Achievement(AchievementPreset basePreset, AchievementData data)
        {
            BasePreset = basePreset;
            if (data is not T achievementData)
            {
                DebugUtils.LogError($"Achievement created with invalid data type. Expected {typeof(T)}, but got {data.GetType()}.");
                return;
            }
            AchievementData = achievementData;
        }
        
        public abstract void Apply();

        public abstract Vector2 GetProgress();

        protected virtual void SaveAchievementData()
        {
            SaveRequestCommand.Execute(this);
        }
        
        public virtual void Complete()
        {
            AchievementData.completed = true;
            SaveAchievementData();
        }
    }
}