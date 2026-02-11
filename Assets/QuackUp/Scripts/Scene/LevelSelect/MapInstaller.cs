using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Scene
{
    [Serializable]
    public class MapInstaller : IInstaller
    {
        // ลาก MapPage จากใน Scene มาใส่ตรงนี้
        [SerializeField] private MapPageView mapPageView; 

        public void Install(IContainerBuilder builder)
        {
            // 1. ลงทะเบียน View (MapPage)
            builder.RegisterComponent(mapPageView);

            // 2. ลงทะเบียน ViewModel (MapPageViewModel)
            // สมมติ MaxLevel = 100 (ของจริงอาจจะดึงจาก UserData)
            builder.Register<MapPageViewModel>(Lifetime.Scoped)
                .WithParameter("playerMaxLevel", 1); 
        }
    }
}
