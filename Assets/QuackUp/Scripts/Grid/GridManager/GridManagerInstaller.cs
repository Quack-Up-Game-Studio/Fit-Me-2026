using System;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Grid
{
    [Serializable]
    public class GridManagerDebugData : DebugDataBase
    {
        [ShowInInspector] private GridManager _gridManager;
        
        public GridManagerDebugData(GridManager gridManager)
        {
            _gridManager = gridManager;
        }
    }
    
    [Serializable]
    public class GridManagerInstaller : DebugableInstaller<GridManagerDebugData>
    {
        [SerializeField] private UnityEngine.Grid grid;
        [SerializeField] private GridManagerConfig gridManagerConfig;
        
        [SerializeField] private CellConfig cellConfig;
        [SerializeField] private CellView cellViewPrefab;
        [SerializeField] private Transform cellParent;
        [SerializeField] private GridPreview gridPreview;
        
        public override void Install(IContainerBuilder builder)
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
            builder.Register<IMessageHub, GridManagerMessageHub>(Lifetime.Singleton)
                .Keyed(GridManagerMessageHub.GridManagerMessageHubKey);
            builder.Register<GridManager>(Lifetime.Singleton);
            builder.Register<ObstacleHandler>(Lifetime.Singleton);
            builder.RegisterComponent(gridPreview);
            
            builder.RegisterBuildCallback(x =>
            {
                var gridManager = x.Resolve<GridManager>();
                DebugData = new GridManagerDebugData(gridManager);
                x.Resolve<GridPreview>();
                x.Resolve<ObstacleHandler>();
            });
        }
    }
}