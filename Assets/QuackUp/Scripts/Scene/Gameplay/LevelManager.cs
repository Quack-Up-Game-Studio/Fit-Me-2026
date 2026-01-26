using FitMe.Entity;
using FitMe.Grid;
using QuackUp.Utils;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Scene
{
    public class LevelManager : IStartable
    {
        private readonly LevelManagerConfig _config;
        private readonly EntityManager _entityManager;
        private readonly IMessageHub _messageHub;
        
        [Inject]
        public LevelManager(
            LevelManagerConfig config, 
            EntityManager entityManager,
            [Key(LevelManagerMessageHub.MessageHubKey)] IMessageHub messageHub)
        {
            _config = config;
            _entityManager = entityManager;
            _messageHub = messageHub;
        }

        public void Start()
        {
            _messageHub.Publish(new StartSpawnEvent());
            _entityManager.CreateSceneEntities();
            _entityManager.TryCreateEntity<PlayerEntity>(EntityType.Player, out _, out _);
            _entityManager.TryGetEntityOfType<PlayerEntity>(out var player);
            player.TryGetComponent<HealthComponent>(out var healthComponent);
            Debug.Log($"Player current health: {healthComponent.CurrentHealth.Value}");
        }
    }
}