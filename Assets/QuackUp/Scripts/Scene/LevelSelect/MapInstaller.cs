using System;
using QuackUp.Utils;
using VContainer;
using VContainer.Unity;
using UnityEngine;

namespace FitMe.Scene
{
    [Serializable]
    public class MapInstaller : IInstaller
    {
        [SerializeField] private MapPageView mapPageView;
        [SerializeField] private LevelDatabase levelDatabase; 

        public void Install(IContainerBuilder builder)
        {
            builder.RegisterComponent(mapPageView);

            builder.RegisterInstance(levelDatabase);
            int currentPlayerLevel = 10;

            builder.Register<MapPageViewModel>(Lifetime.Scoped)
                .WithParameter("playerMaxLevel", currentPlayerLevel);
            
        }
    }
}