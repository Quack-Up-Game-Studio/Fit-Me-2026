using System;
using FitMe.Entity;
using FitMe.Grid;
using FitMe.Scene.UI.Score;
using FitMe.Shared;
using QuackUp.Utils;
using R3;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Scene
{
    public class LevelManager : IGameStateManager, IStartable, IDisposable
    {
        public ReactiveProperty<int> Score { get; } = new(0);
        public ReactiveProperty<int> FitMeScore { get; } = new(0);
        /// <remarks>
        /// Use <see cref="SetGameState"/> to change game state.
        /// For pausing, use <see cref="Pause"/> to pause the game or use <see cref="Unpause"/> to restore the previous state before pausing.
        /// </remarks>
        public ReadOnlyReactiveProperty<GameState> GameState => _gameState.ToReadOnlyReactiveProperty();

        private readonly ReactiveProperty<GameState> _gameState = new(Shared.GameState.CountOff);
        private readonly LevelManagerConfig _config;
        private readonly EntityManager _entityManager;
        private readonly IMessageHub _messageHub;
        private readonly GridManager _gridManager;
        private readonly PopUpScoreFactory _popUpScoreFactory;
        
        private GameState _previousStateBeforePause;
        private IDisposable _subscriptions;
        
        [Inject]
        public LevelManager(
            LevelManagerConfig config, 
            EntityManager entityManager,
            [Key(LevelManagerMessageHub.MessageHubKey)] IMessageHub messageHub,
            GridManager gridManager,
            PopUpScoreFactory popUpScoreFactory)
        {
            _config = config;
            _entityManager = entityManager;
            _messageHub = messageHub;
            _gridManager = gridManager;
            _popUpScoreFactory = popUpScoreFactory;
            Initialize();
            Subscribe();
        }
        
        private void Initialize()
        {
            if (!_config.HasCountOff)
            {
                _gameState.Value = Shared.GameState.PlaceBlock;
            }
        }

        private void Subscribe()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            _gridManager.OnScoreAdded
                .Subscribe(OnScoreAdded)
                .AddTo(ref disposableBuilder);
            _subscriptions = disposableBuilder.Build();
        }
        
        public void Dispose()
        {
            _subscriptions?.Dispose();
        }
        
        public void Start()
        {
            _messageHub.Publish(new StartSpawnEvent());
            _entityManager.CreateSceneEntities();
            _entityManager.TryCreateEntity<PlayerEntity>(EntityType.Player, out _, out _);
            _entityManager.TryGetEntityOfType<PlayerEntity>(out var player);
            player.TryGetComponent<HealthComponent>(out var healthComponent);
            DebugUtils.Log($"Player current health: {healthComponent.CurrentHealth.Value}");
        }
        
        private void OnScoreAdded(ScoreEvent scoreEvent)
        {
            int finalScore = 0;
            var previousScore = Score.Value;
            var previousFitMe = FitMeScore.Value;

            switch (scoreEvent.ScoreType)
            {
                case ScoreTypes.Placement:
                    finalScore = _config.ScorePerPlacement;
                    break;
                /*case ScoreTypes.PreInfect:
                    finalScore = scorePerPreInfect;
                    break;*/
                case ScoreTypes.Combo:
                    if (scoreEvent.ContactCount <= 1) return;
                    finalScore = _config.ScorePerCombo * (scoreEvent.ContactCount - 1);
                    break;
                case ScoreTypes.Bomb:
                    if (scoreEvent.ContactCount <= 2) return;
                    finalScore = _config.ScorePerBomb * scoreEvent.ContactCount;    
                    break;
                case ScoreTypes.FitMe:
                    ChangeFitMe(1);
                    _popUpScoreFactory.Create(1, scoreEvent.WorldPosition, "Fitme");
                    finalScore = _config.ScorePerFitMe; 
                    break;
            }
            ChangeScore(finalScore);
            _popUpScoreFactory.Create(finalScore, scoreEvent.WorldPosition, "Score");
        }
        
        private void ChangeScore(int value)
        {
            Score.Value += value;
        }
        
        private void ChangeFitMe(int value)
        {
            FitMeScore.Value += value;
        }
        
        public void SetGameState(GameState newState)
        {
            if (newState is Shared.GameState.Pause)
            {
                DebugUtils.LogWarning("Use Pause() method to pause the game.");
                Pause();
                return;
            }
            _gameState.Value = newState;
        }

        public void Pause()
        {
            _previousStateBeforePause = _gameState.Value;
            _gameState.Value = Shared.GameState.Pause;
        }

        public void Unpause()
        {
            _gameState.Value = _previousStateBeforePause;
        }
    }
}