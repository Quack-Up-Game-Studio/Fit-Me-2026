using UnityEngine;

namespace FitMe.GameData
{
    [CreateAssetMenu(fileName = "HintManagerConfig", menuName = "FitMe/GameData/Hint/HintManagerConfig")]
    public class HintManagerConfig : ScriptableObject
    {
        [field: SerializeField, Min(0)] public int FreeHintCap { get; private set; } = 5;
        [field: SerializeField, Min(0)] public int DailyHintGrant { get; private set; } = 1;
    }
}
