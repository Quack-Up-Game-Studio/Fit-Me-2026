using QuackUp.Save;
using UnityEngine;

namespace FitMe.GameData
{
    [CreateAssetMenu(fileName = "PlayerRecordSaveObject", menuName = "FitMe/GameData/PlayerRecordSaveObject", order = 0)]
    public class PlayerRecordSaveObject : MessagePackSaveObject<PlayerRecordSaveData>
    {
        
    }
}