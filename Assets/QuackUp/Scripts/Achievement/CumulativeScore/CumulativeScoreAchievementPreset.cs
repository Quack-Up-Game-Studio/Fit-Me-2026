using FitMe.GameData;
using QuackUp.Save;
using QuackUp.Utils;
using UnityEngine;
using VContainer;

namespace FitMe.Achievement
{
    [CreateAssetMenu(fileName = "CumulativeScoreAchievementPreset", menuName = "FitMe/Achievement/Preset/CumulativeScoreAchievementPreset", order = 0)]
    public class CumulativeScoreAchievementPreset : AchievementPreset
    {
        [field: SerializeField] public float TargetCumulativeScore { get; private set; }

        [Inject] private MessagePackSaveManager _saveManager;
        
        public override IAchievement CrateAchievement(out AchievementData outData, AchievementData data = null)
        {
            CumulativeScoreAchievementData achievementData;
            outData = null;
            if (_saveManager == null)
            {
                DebugUtils.LogError("SaveManager is not injected yet.");
                return null;
            }
            var saveObject = _saveManager.GetFirstSaveObjectOfType<PlayerRecordSaveObject>();
            if (!saveObject) return null;
            if (data == null)
            {
                achievementData = new CumulativeScoreAchievementData();
            }
            else
            {
                achievementData = data as CumulativeScoreAchievementData;
            }
            outData = achievementData;
            var achievement = new CumulativeScoreAchievement(saveObject, this, achievementData);
            return achievement;
        }
    }
}