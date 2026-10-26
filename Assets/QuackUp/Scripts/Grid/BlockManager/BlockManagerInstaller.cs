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
    public class BlockManagerDebugData : DebugDataBase
    {
        [ShowInInspector] private BlockManager blockManager;
        
        public BlockManagerDebugData(BlockManager blockManager)
        {
            this.blockManager = blockManager;
        }
    }
    [Serializable]
    public class BlockManagerInstaller : DebugableInstaller<BlockManagerDebugData>
    {
        [ShowInInspector] private InspectorPlaceholder _title;
        [SerializeField] private BlockManagerConfig blockManagerConfig;
        [SerializeField] private BlockConfig blockConfig;
        [SerializeField] private BlockManager.SpawnPointData[] spawnPoints;
        [SerializeField] private Transform previewSpawnPoint;
        [SerializeField] private BlockManager.SpawnPointData[] previewsPoint;
        [SerializeField] private AtomView atomViewPrefab;
        [SerializeField] private BlockView blockViewPrefab;
        [SerializeField] private RectTransform shapeTrayPanel;
        
        public override void Install(IContainerBuilder builder)
        {
            if (!blockManagerConfig || !blockManagerConfig.ExplosionVfxPoolConfig)
                throw new InvalidOperationException("BlockManagerInstaller requires an ExplosionVfxPoolConfig on BlockManagerConfig.");

            //Shared
            builder.RegisterInstance(blockManagerConfig);
            builder.RegisterInstance(blockConfig);
            builder.RegisterInstance(blockManagerConfig.ExplosionVfxPoolConfig);
            
            //Atom
            builder.RegisterInstance(atomViewPrefab);
            builder.Register<AtomFactory>(Lifetime.Scoped);
            
            //Block
            builder.RegisterInstance(blockViewPrefab);
            builder.Register<BlockControllerFactory>(Lifetime.Scoped);
            builder.Register<BlockFactory>(Lifetime.Scoped);
            builder.Register<ExplosionVfxPool>(Lifetime.Scoped);
            
            //BlockManager
            builder.RegisterInstance(previewSpawnPoint).Keyed(BlockManager.PreviewTransformKey);
            builder.RegisterInstance(spawnPoints);
            builder.RegisterInstance(previewsPoint).Keyed(BlockManager.PreviewTransformKey);
            // Keyed optional parameters still need a binding in VContainer. Legacy
            // MainMenu/Tutorial scenes deliberately resolve a null tray.
            builder.Register<RectTransform>(_ => shapeTrayPanel, Lifetime.Scoped).Keyed(BlockManager.ShapeTrayKey);
            builder.RegisterEntryPoint<BlockManager>(Lifetime.Singleton).As<BlockManager>();
            builder.Register<IMessageHub, BlockManagerMessageHub>(Lifetime.Singleton)
                .Keyed(BlockManagerMessageHub.MessageHubKey);
            
            builder.RegisterBuildCallback(x =>
            {
                var manager = x.Resolve<BlockManager>();
                DebugData = new BlockManagerDebugData(manager);
            });
        }
    }
}