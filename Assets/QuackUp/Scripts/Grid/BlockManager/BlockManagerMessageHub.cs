using System.Collections.Generic;
using FitMe.Shared;
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
            IPublisher<NoPlaceableBlockEvent> noPlaceableBlockPublisher,
            IPublisher<GameOverEvent> gameOverPublisher)
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
            MessageWrappers[typeof(GameOverEvent)] = new MessageWrapper<GameOverEvent>(
                gameOverPublisher,
                null);
        }
    }
    
    #region Events
    public struct StartSpawnEvent
    {
        public readonly BlockPreset BlockPreset;
        public readonly bool AllowRotation;
        
        public StartSpawnEvent(BlockPreset blockPreset = null, bool allowRotation = true)
        {
            AllowRotation = allowRotation;
            BlockPreset = blockPreset;
        }
    }
    
    public struct BlockSpawnedEvent
    {
        public readonly List<BlockInstance> BlockInstances;
        
        public BlockSpawnedEvent(List<BlockInstance> blockInstances)
        {
            BlockInstances = blockInstances;
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