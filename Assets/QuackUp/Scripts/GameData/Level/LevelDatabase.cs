using System.Collections.Generic;
using FitMe.Grid;
using UnityEngine;

namespace FitMe.GameData
{
    [CreateAssetMenu(fileName = "LevelDatabase", menuName = "FitMe/GameData/Level/Level Database")]
    public class LevelDatabase : ScriptableObject
    {
        public List<GridPreset> LevelPresets; 

        public GridPreset GetPreset(int levelId)
        {
            int index = levelId - 1; // ด่าน 1 คือ index 0
            if (index >= 0 && index < LevelPresets.Count)
            {
                return LevelPresets[index];
            }
            Debug.LogWarning($"ไม่พบข้อมูลด่าน {levelId}!");
            return null;
        }
    }
}