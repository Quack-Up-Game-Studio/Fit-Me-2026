using System.Collections.Generic;
using UnityEngine;

namespace FitMe.Panel
{
    public class MapChunkView : MonoBehaviour
    {
        [SerializeField] private List<LevelButtonView> buttonViews; 

        public void Setup(int startLevelID, LevelSelectViewModel pageVM)
        {
            for (int i = 0; i < buttonViews.Count; i++)
            {
                int realLevelID = startLevelID + i;
                var btnVM = pageVM.CreateButtonViewModel(realLevelID);
                buttonViews[i].Construct(btnVM);
            }
        }
    }
}
