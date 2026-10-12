using System;
using FitMe.GameData;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Scene
{
    [Serializable]
    public class LevelSelectManagerInstaller : IInstaller
    {
        [SerializeField, Required] private LevelManagerConfig levelManagerConfig;

        public void Install(IContainerBuilder builder)
        {
            var catalog = levelManagerConfig ? levelManagerConfig.ShapeLevelCatalogAsset : null;
            if (catalog) builder.RegisterInstance(catalog).AsSelf();
            builder.RegisterInstance(new ShapeLevelSelectionService(catalog)).AsSelf();
            builder.RegisterEntryPoint<LevelSelectManager>().AsSelf();
            builder.RegisterBuildCallback(x =>
            {
                x.Resolve<LevelSelectManager>();
            });
        }
    }
}
