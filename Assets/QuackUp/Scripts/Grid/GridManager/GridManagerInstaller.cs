using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Grid
{
    [Serializable]
    public class GridManagerInstaller : IInstaller
    {
        [SerializeField] private UnityEngine.Grid grid;
        [SerializeField] private GridManagerConfig gridManagerConfig;
        [SerializeField] private CellFactory cellFactory;
        
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterComponent(grid);
            builder.RegisterInstance(gridManagerConfig);
            builder.Register(x =>
            {
                x.Inject(cellFactory);
                return cellFactory;
            }, Lifetime.Scoped);
            builder.Register<GridManager>(Lifetime.Singleton);
            builder.RegisterBuildCallback(x =>
            {
                x.Resolve<GridManager>();
            });
        }
    }
}