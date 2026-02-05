using System;
using FitMe.Achievement;
using FitMe.Entity;
using FitMe.GameData;
using FitMe.Grid;
using FitMe.Scene.UI.Score;
using FitMe.Shared;
using QuackUp.Audio;
using QuackUp.Save;
using QuackUp.SceneManagement;
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
        private readonly IAudioManager _audioManager;
        private readonly IMessageHub _messageHub;
        private readonly GridManager _gridManager;
        private readonly MessagePackSaveManager _saveManager;
        private readonly AchievementManager _achievementManager;
        private readonly PopUpScoreFactory _popUpScoreFactory;
        
        private PlayerRecordSaveObject _playerRecordSaveObject;
        private AudioReference _bgmReference;
        private GameState _previousStateBeforePause;
        private IDisposable _subscriptions;
        
        [Inject]
        public LevelManager(
            LevelManagerConfig config, 
            EntityManager entityManager,
            IAudioManager audioManager,
            [Key(LevelManagerMessageHub.MessageHubKey)] IMessageHub messageHub,
            GridManager gridManager,
            MessagePackSaveManager saveManager,
            AchievementManager achievementManager,
            PopUpScoreFactory popUpScoreFactory)
        {
            _config = config;
            _entityManager = entityManager;
            _audioManager = audioManager;
            _messageHub = messageHub;
            _gridManager = gridManager;
            _popUpScoreFactory = popUpScoreFactory;
            _saveManager = saveManager;
            _achievementManager = achievementManager;
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
            _messageHub.GetObservable<LoadSceneStageEvent>()
                .Where(x => x.Stage is LoadSceneStage.StartOut)
                .Subscribe(_ => OnSceneStartOut())
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
            
            _bgmReference = _audioManager.PlayAudio(_config.GameplayBgm, Vector3.zero);
            
            _playerRecordSaveObject = _saveManager.GetFirstSaveObjectOfType<PlayerRecordSaveObject>();
            DebugUtils.Log($"PlayerRecordSaveObject found: {_playerRecordSaveObject}");
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
        
        private void OnSceneStartOut()
        {
            _audioManager.StopAudio(_bgmReference);
        }
        
        private void ChangeScore(int value)
        {
            Score.Value += value;
            if (!_playerRecordSaveObject) return;
            var saveData = _playerRecordSaveObject.GetSaveData<PlayerRecordSaveData>();
            if (saveData == null) return;
            saveData.cumulativeScore += value;
        }
        
        private void ChangeFitMe(int value)
        {
            FitMeScore.Value += value;
            if (!_playerRecordSaveObject) return;
            var saveData = _playerRecordSaveObject.GetSaveData<PlayerRecordSaveData>();
            if (saveData == null) return;
            saveData.cumulativeFitMe += value;
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