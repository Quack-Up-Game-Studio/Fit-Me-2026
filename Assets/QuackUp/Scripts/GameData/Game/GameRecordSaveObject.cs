using QuackUp.Save;
using UnityEngine;

namespace FitMe.GameData
{
    [CreateAssetMenu(fileName = "GameRecordSaveObject", menuName = "FitMe/GameData/Game/GameRecordSaveObject", order = 0)]
    public class GameRecordSaveObject : MessagePackSaveObject<GameRecordSaveData>
    {
       
    }
}