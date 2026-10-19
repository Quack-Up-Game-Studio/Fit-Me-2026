using System;
using FitMe.GameData;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Panel
{
    [Serializable]
    public sealed class ShapeWorldSelectLifetimeScope : PanelLifetimeScope
    {
        [SerializeField, Required] private ShapeWorldSelectView panelView;
        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);
            if (!panelView) { DebugUtils.LogError("World Select LifetimeScope requires a view."); return; }
            builder.RegisterComponent(panelView).AsSelf().As<IPanelView>();
            builder.Register<ShapeWorldSelectViewModel>(Lifetime.Singleton).AsSelf().As<IPanelViewModel>();
        }
        public override IPanelViewModel CreatPanel() => Container.Resolve<ShapeWorldSelectViewModel>();
    }
}
