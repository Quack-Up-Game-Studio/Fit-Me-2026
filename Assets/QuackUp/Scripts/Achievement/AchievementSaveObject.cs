using QuackUp.Save;
using Sirenix.OdinInspector;
using UnityEngine;

namespace FitMe.Achievement
{
    [CreateAssetMenu(fileName = "AchievementSaveObject", menuName = "FitMe/Achievement/AchievementSaveObject", order = 0)]
    [ShowOdinSerializedPropertiesInInspector]
    public class AchievementSaveObject : MessagePackSaveObject<AchievementSaveData>
    {
        
    }
}