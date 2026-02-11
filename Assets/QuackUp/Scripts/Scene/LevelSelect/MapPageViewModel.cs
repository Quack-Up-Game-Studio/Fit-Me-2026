using System.Collections.Generic;
using UnityEngine;

namespace FitMe.Scene
{
    public class MapPageViewModel
    {
        public int PlayerMaxLevel { get; }
        private readonly int _levelsPerChunk = 10;

        public MapPageViewModel(int playerMaxLevel)
        {
            PlayerMaxLevel = playerMaxLevel;
        }

        // คำนวณว่าต้องโหลด Chunk ไหนบ้าง (อดีต-ปัจจุบัน-อนาคต)
        public List<int> GetChunksToLoad()
        {
            int currentChunk = (PlayerMaxLevel - 1) / _levelsPerChunk;
            var chunks = new List<int>();
        
            if (currentChunk > 0) chunks.Add(currentChunk - 1); // ก่อนหน้า
            chunks.Add(currentChunk);      // ปัจจุบัน
            chunks.Add(currentChunk + 1);  // ถัดไป
        
            return chunks;
        }

        // Factory Method: สร้าง VM ให้ปุ่มลูก
        public LevelButtonViewModel CreateButtonViewModel(int levelId)
        {
            // จริงๆ ตรงนี้ควรดึง Save Data จริงๆ มาใส่
            var mockStars = new Dictionary<int, int>(); 
            return new LevelButtonViewModel(levelId, PlayerMaxLevel, mockStars);
        }
    }
}
