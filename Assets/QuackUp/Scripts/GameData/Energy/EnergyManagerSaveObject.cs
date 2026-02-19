using QuackUp.Save;
using Sirenix.OdinInspector;
using UnityEngine;

namespace FitMe.GameData
{
    [CreateAssetMenu(fileName = "EnergyManagerSaveObject", menuName = "FitMe/GameData/Energy/EnergyManagerSaveObject")]
    [ShowOdinSerializedPropertiesInInspector]
    public class EnergyManagerSaveObject : MessagePackSaveObject<EnergyManagerSaveData>
    {
        
    }
}