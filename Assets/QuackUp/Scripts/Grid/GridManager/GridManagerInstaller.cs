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
        
        [SerializeField] private CellConfig cellConfig;
        [SerializeField] private CellView cellViewPrefab;
        [SerializeField] private Transform cellParent;
        [SerializeField] private GridPreview gridPreview;
        
        public void Install(IContainerBuilder builder)
        {
            //Shared
            builder.RegisterComponent(grid);
            builder.RegisterInstance(gridManagerConfig);
            
            //Cell
            builder.RegisterInstance(cellConfig);
            builder.RegisterInstance(cellViewPrefab);
            builder.RegisterInstance(cellParent).Keyed(CellFactory.CellParentKey);
            builder.Register<CellFactory>(Lifetime.Scoped);
            
            //GridManager
            builder.Register<GridManager>(Lifetime.Singleton);
            builder.RegisterComponent(gridPreview);
            
            builder.RegisterBuildCallback(x =>
            {
                x.Resolve<GridManager>();
                x.Resolve<GridPreview>();
            });
        }
    }
}