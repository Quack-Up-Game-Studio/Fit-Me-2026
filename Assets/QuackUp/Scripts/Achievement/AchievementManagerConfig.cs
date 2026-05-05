using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace FitMe.Achievement
{
    [CreateAssetMenu(fileName = "AchievementManagerConfig", menuName = "FitMe/Achievement/AchievementManagerConfig")]
    public class AchievementManagerConfig : SerializedScriptableObject
    {
        [field: SerializeField] public bool Enabled { get; set; }
        [SerializeField] private List<AchievementPreset> achievementPresets = new();
        public IReadOnlyList<AchievementPreset> AchievementPresets => achievementPresets;
    }
}