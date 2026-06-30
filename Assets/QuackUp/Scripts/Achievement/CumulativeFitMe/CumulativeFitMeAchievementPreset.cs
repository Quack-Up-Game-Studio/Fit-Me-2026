using FitMe.GameData;
using Debug = QuackUp.Utils.DebugUtils;
using QuackUp.Save;
using UnityEngine;
using VContainer;

namespace FitMe.Achievement
{
    [CreateAssetMenu(fileName = "CumulativeFitMeAchievementPreset", menuName = "FitMe/Achievement/Preset/CumulativeFitMeAchievementPreset", order = 0)]
    public class CumulativeFitMeAchievementPreset : AchievementPreset
    {
        [field: SerializeField] public float TargetCumulativeFitMe { get; private set; }
        
        private MessagePackSaveManager _saveManager;
        
        [Inject] 
        public void Construct(MessagePackSaveManager saveManager)
        {
            _saveManager = saveManager;
        }
        
        public override IAchievement CrateAchievement(out AchievementData outData, AchievementData data = null)
        {
            CumulativeFitMeAchievementData achievementData;
            outData = null;
            if (_saveManager == null)
            {
                Debug.LogError("SaveManager is not injected yet.");
                return null;
            }
            var saveObject = _saveManager.GetFirstSaveObjectOfType<PlayerRecordSaveObject>();
            if (!saveObject) return null;
            if (data == null)
            {
                achievementData = new CumulativeFitMeAchievementData();
            }
            else
            {
                achievementData = data as CumulativeFitMeAchievementData;
            }
            outData = achievementData;
            var achievement = new CumulativeFitMeAchievement(saveObject, this, achievementData);
            return achievement;
        }
    }
}