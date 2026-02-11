using System.Collections.Generic;
using R3;
using UnityEngine;

namespace FitMe.Scene
{
    public class LevelButtonViewModel
    {
        public int LevelID { get; }
        // Reactive Property ให้ View คอยฟังการเปลี่ยนแปลง
        public ReadOnlyReactiveProperty<bool> IsLocked { get; }
        public ReadOnlyReactiveProperty<bool> IsCurrent { get; }
        public ReadOnlyReactiveProperty<int> Stars { get; }

        public ReactiveCommand OnClickCommand { get; } = new();

        public LevelButtonViewModel(int levelId, int playerMaxLevel, Dictionary<int, int> starsData)
        {
            LevelID = levelId;

            // Logic คำนวณสถานะ (Reactive)
            // ถ้า LevelID มากกว่า MaxLevel -> ล็อค
            IsLocked = Observable.Return(levelId > playerMaxLevel).ToReadOnlyReactiveProperty();
            IsCurrent = Observable.Return(levelId == playerMaxLevel).ToReadOnlyReactiveProperty();
        
            // ดึงดาวจาก Save data (ถ้าไม่มีคือ 0)
            int starCount = starsData.GetValueOrDefault(levelId, 0);
            Stars = Observable.Return(starCount).ToReadOnlyReactiveProperty();
        }
    }
}
