using System;
using FitMe.GameData;
using QuackUp.Save;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Panel
{
    [Serializable]
    public sealed class ShapeLevelSelectLifetimeScope : PanelLifetimeScope
    {
        [SerializeField, Required] private ShapeLevelSelectView panelView;

        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);
            if (!panelView)
            {
                DebugUtils.LogError("Shape Level Select LifetimeScope requires a ShapeLevelSelectView reference.");
                return;
            }

            builder.RegisterComponent(panelView).AsSelf().As<IPanelView>();
        }

        public override IPanelViewModel CreatPanel()
        {
            var saveManager = Container.Resolve<MessagePackSaveManager>();
            var saveObject = saveManager.GetFirstSaveObjectOfType<PlayerRecordSaveObject>();
            var highestUnlockedOrder = saveObject
                ? saveObject.GetSaveData<PlayerRecordSaveData>().HighestUnlockedShapeOrder
                : 1;
            Container.TryResolve<ShapeLevelCatalog>(out var catalog);

            return new ShapeLevelSelectViewModel(
                Container.Resolve<PanelManager>(),
                catalog,
                highestUnlockedOrder);
        }
    }
}
