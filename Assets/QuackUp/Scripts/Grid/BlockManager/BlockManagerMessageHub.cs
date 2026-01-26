using System.Collections.Generic;
using MessagePipe;
using QuackUp.Utils;
using VContainer;

namespace FitMe.Grid
{
    public class BlockManagerMessageHub : MessageHub
    {
        public const string MessageHubKey = "BlockManagerMessageHub";
        
        [Inject]
        public BlockManagerMessageHub(
            ISubscriber<StartSpawnEvent> startSpawnSubscription,
            IPublisher<BlockSpawnedEvent> blockSpawnedPublisher,
            IPublisher<NoPlaceableBlockEvent> noPlaceableBlockPublisher)
        {
            MessageWrappers[typeof(StartSpawnEvent)] = new MessageWrapper<StartSpawnEvent>(
                null,
                startSpawnSubscription);
            MessageWrappers[typeof(BlockSpawnedEvent)] = new MessageWrapper<BlockSpawnedEvent>(
                blockSpawnedPublisher,
                null);
            MessageWrappers[typeof(NoPlaceableBlockEvent)] = new MessageWrapper<NoPlaceableBlockEvent>(
                noPlaceableBlockPublisher,
                null);
        }
    }
    
    #region Events
    public struct StartSpawnEvent
    {
        public readonly BlockPreset BlockPreset;
        
        public StartSpawnEvent(BlockPreset blockPreset = null)
        {
            BlockPreset = blockPreset;
        }
    }
    
    public struct BlockSpawnedEvent
    {
        public readonly List<BlockModel> BlockModels;
        
        public BlockSpawnedEvent(List<BlockModel> blockModels)
        {
            BlockModels = blockModels;
        }
    }
    
    public struct NoPlaceableBlockEvent
    {
        public readonly int VacantCount;
        
        public NoPlaceableBlockEvent(int vacantCount)
        {
            VacantCount = vacantCount;
        }
    }
    #endregion
}