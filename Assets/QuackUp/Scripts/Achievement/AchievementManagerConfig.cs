using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace FitMe.Achievement
{
    [CreateAssetMenu(fileName = "AchievementManagerConfig", menuName = "FitMe/Achievement/AchievementManagerConfig")]
    public class AchievementManagerConfig : SerializedScriptableObject
    {
        [SerializeField] private List<AchievementPreset> achievementPresets = new();
        public IReadOnlyList<AchievementPreset> AchievementPresets => achievementPresets;
    }
}