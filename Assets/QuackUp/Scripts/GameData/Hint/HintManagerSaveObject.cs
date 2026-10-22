using QuackUp.Save;
using Sirenix.OdinInspector;
using UnityEngine;

namespace FitMe.GameData
{
    [CreateAssetMenu(fileName = "HintManagerSaveObject", menuName = "FitMe/GameData/Hint/HintManagerSaveObject")]
    [ShowOdinSerializedPropertiesInInspector]
    public class HintManagerSaveObject : MessagePackSaveObject<HintManagerSaveData>
    {
    }
}
