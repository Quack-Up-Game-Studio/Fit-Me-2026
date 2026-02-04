using QuackUp.Save;
using UnityEngine;

namespace FitMe.GameData
{
    [CreateAssetMenu(fileName = "GameRecordSaveObject", menuName = "FitMe/GameData/GameRecordSaveObject", order = 0)]
    public class GameRecordSaveObject : MessagePackSaveObject<GameRecordSaveData>
    {
        
    }
}