using System;
using System.Collections.Generic;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Grid
{
    [Serializable]
    public class BlockManagerInstaller : IInstaller
    {
        [ShowInInspector] private InspectorPlaceholder _title;
        [SerializeField] private BlockManagerConfig blockManagerConfig;
        [SerializeField] private BlockConfig blockConfig;
        [SerializeField] private BlockManager.SpawnPointData[] spawnPoints;
        [SerializeField] private Transform previewSpawnPoint;
        [SerializeField] private AtomView atomViewPrefab;
        [OdinSerialize] private Dictionary<BlockShape, BlockView> blockViewDictionary;
        
        
        //TODO: Register preview transforms
        
        public void Install(IContainerBuilder builder)
        {
            //Shared
            builder.RegisterInstance(blockManagerConfig);
            builder.RegisterInstance(blockConfig);
            
            //Atom
            builder.RegisterInstance(atomViewPrefab);
            builder.Register<AtomFactory>(Lifetime.Scoped);
            
            //Block
            builder.RegisterInstance(blockViewDictionary);
            builder.Register<BlockFactory>(Lifetime.Scoped);
            
            //BlockManager
            builder.RegisterComponent(previewSpawnPoint);
            builder.RegisterInstance(spawnPoints);
            builder.RegisterEntryPoint<BlockManager>(Lifetime.Singleton).As<BlockManager>();
            
            builder.RegisterBuildCallback(x =>
            {
                x.Resolve<BlockManager>();
            });
        }
    }
}