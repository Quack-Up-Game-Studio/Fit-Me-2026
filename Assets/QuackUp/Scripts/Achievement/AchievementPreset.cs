using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace FitMe.Achievement
{
    public abstract class AchievementPreset : SerializedScriptableObject
    {
        [field: SerializeField] public string AchievementId { get; private set; }
        [field: SerializeField] public string AchievementName { get; private set; }
        [field: SerializeField] public string AchievementDescription { get; private set; }
        [field: SerializeField] public Sprite AchievementIcon { get; private set; }

        public abstract IAchievement CrateAchievement(out AchievementData outData, AchievementData data = null);
    }
}