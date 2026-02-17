using QuackUp.Save;
using Sirenix.OdinInspector;
using UnityEngine;

namespace FitMe.GameData
{
    [CreateAssetMenu(fileName = "EnergyManagerSaveData", menuName = "FitMe/GameData/EnergyManagerSaveData")]
    [ShowOdinSerializedPropertiesInInspector]
    public class EnergyManagerSaveObject : MessagePackSaveObject<EnergyManagerSaveData>
    {
        
    }
}