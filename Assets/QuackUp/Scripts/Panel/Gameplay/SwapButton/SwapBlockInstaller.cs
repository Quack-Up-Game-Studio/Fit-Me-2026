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

            var rotateBlockView = swapBlockView.transform.parent.GetComponentInChildren<RotateBlockView>(true);
            if (!rotateBlockView) return;

            builder.Register<RotateBlockViewModel>(Lifetime.Singleton);
            builder.RegisterComponent(rotateBlockView);
        }
    }
}