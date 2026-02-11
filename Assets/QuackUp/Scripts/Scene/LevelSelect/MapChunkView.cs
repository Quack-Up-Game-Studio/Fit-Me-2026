using System.Collections.Generic;
using UnityEngine;

namespace FitMe.Scene
{
    public class MapChunkView : MonoBehaviour
    {
        // ลากปุ่มวางเรียงไว้แล้วใน Inspector (แบบ A)
        [SerializeField] private List<LevelButtonView> buttonViews; 

        public void Setup(int startLevelID, MapPageViewModel pageVM)
        {
            for (int i = 0; i < buttonViews.Count; i++)
            {
                int realLevelID = startLevelID + i;
            
                // 1. ขอ ViewModel จากแม่ (MapPage)
                var btnVM = pageVM.CreateButtonViewModel(realLevelID);
            
                // 2. ยัดใส่ให้ลูก (LevelButtonView)
                buttonViews[i].Construct(btnVM);
            }
        }
    }
}
