using System;
using FitMe.GameData;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Scene
{
    [Serializable]
    public sealed class ShapeLevelWorldCatalogInstaller : IInstaller
    {
        [SerializeField, Required] private ShapeLevelWorldCatalog worldCatalog;

        public void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(new ShapeLevelWorldCatalogService(worldCatalog)).AsSelf();
        }
    }
}
