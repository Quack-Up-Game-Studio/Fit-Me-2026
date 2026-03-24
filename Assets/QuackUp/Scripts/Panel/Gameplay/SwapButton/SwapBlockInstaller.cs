using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Panel
{
    [Serializable]
    public class SwapBlockInstaller : IInstaller
    {
        [SerializeField] private SwapBlockView swapBlockView;
        
        public void Install(IContainerBuilder builder)
        {
            builder.Register<SwapBlockViewModel>(Lifetime.Singleton);
            builder.RegisterComponent(swapBlockView);
        }
    }
}