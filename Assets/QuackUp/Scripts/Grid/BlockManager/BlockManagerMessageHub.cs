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
            ISubscriber<SpawnWithBlockPresetEvent> startSpawnSubscription,
            IPublisher<BlockSpawnedEvent> blockSpawnedPublisher,
            IPublisher<NoPlaceableBlockEvent> noPlaceableBlockPublisher,
            IPublisher<GameOverEvent> gameOverPublisher,
            ISubscriber<ContinueEvent> clearGridSubscriber)
        {
            MessageWrappers[typeof(SpawnWithBlockPresetEvent)] = new MessageWrapper<SpawnWithBlockPresetEvent>(
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
            MessageWrappers[typeof(ContinueEvent)] = new MessageWrapper<ContinueEvent>(
                null,
                clearGridSubscriber);
        }
    }
    
    #region Events
    
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