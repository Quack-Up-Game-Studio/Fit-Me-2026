using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Panel
{
    [Serializable]
    public class DragMeToPlayInstaller : IInstaller
    {
        [SerializeField] private DragMeToPlayView dragMeToPlayView;
        
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterComponent(dragMeToPlayView);
            builder.Register<DragMeToPlayViewModel>(Lifetime.Singleton);
        }
    }
}