using FitMe.GameData;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Scene
{
    public sealed class ShapeLevelSelectSceneInstaller : IInstaller
    {
        [SerializeField, Required] private LevelManagerConfig levelManagerConfig;

        public void Install(IContainerBuilder builder)
        {
            var catalog = levelManagerConfig ? levelManagerConfig.ShapeLevelCatalogAsset : null;
            if (catalog) builder.RegisterInstance(catalog).AsSelf();
            builder.RegisterInstance(new ShapeLevelSelectionService(catalog)).AsSelf();
            builder.RegisterEntryPoint<ShapeLevelSelectManager>().AsSelf();
            builder.RegisterBuildCallback(x => x.Resolve<ShapeLevelSelectManager>());
        }
    }
}
