using System;
using System.Collections.Generic;
using FitMe.Achievement;
using FitMe.Entity;
using FitMe.GameData;
using FitMe.Grid;
using FitMe.Shared;
using FitMe.Panel;
using QuackUp.Audio;
using QuackUp.Save;
using QuackUp.SceneManagement;
using QuackUp.Utils;
using R3;
using Redcode.Extensions;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Scene
{
    public class LevelManager : ILevelManager, IStartable, IDisposable
    {
        public ReactiveProperty<int> Score { get; } = new(0);
        public ReactiveProperty<int> FitMeScore { get; } = new(0);
        /// <remarks>
        /// Use <see cref="SetGameState"/> to change game state.
        /// For pausing, use <see cref="Pause"/> to pause the game or use <see cref="Unpause"/> to restore the previous state before pausing.
        /// </remarks>
        public ReadOnlyReactiveProperty<GameState> GameState => _gameState.ToReadOnlyReactiveProperty();
        
        public static GameMode GameMode { get; set; }
        public static GridPreset GridPreset { get; set; }
        
        private readonly ReactiveProperty<GameState> _gameState = new(Shared.GameState.CountOff);
        private readonly LevelManagerConfig _config;
        private readonly EntityManager _entityManager;
        private readonly IAudioManager _audioManager;
        private readonly IMessageHub _messageHub;
        private readonly GridManager _gridManager;
        private readonly MessagePackSaveManager _saveManager;
        private readonly AchievementManager _achievementManager;
        private readonly PopUpScoreFactory _popUpScoreFactory;
        private readonly PanelManager _panelManager;
        
        private List<GridPreset> _presets;
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
            PopUpScoreFactory popUpScoreFactory,
            PanelManager panelManager)
        {
            _config = config;
            _entityManager = entityManager;
            _audioManager = audioManager;
            _messageHub = messageHub;
            _gridManager = gridManager;
            _popUpScoreFactory = popUpScoreFactory;
            _saveManager = saveManager;
            _achievementManager = achievementManager;
            _panelManager = panelManager;
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
            _gridManager.OnScoreAdded
                .Where(x => x.ScoreType is ScoreTypes.FitMe)
                .Subscribe(_ => OnFit())
                .AddTo(ref disposableBuilder);
            _messageHub.GetObservable<LoadSceneStageEvent>()
                .Where(x => x.Stage is LoadSceneStage.StartOut)
                .Subscribe(_ => OnSceneStartOut())
                .AddTo(ref disposableBuilder);
            _messageHub.Subscribe<GameOverEvent>(OnGameOverEvent)
                .AddTo(ref disposableBuilder);
            _subscriptions = disposableBuilder.Build();
        }
        
        public void Dispose()
        {
            _subscriptions?.Dispose();
        }
        
        public void Start()
        {
            CheckGameMode();
            
            if (!GridPreset) 
                _messageHub.Publish(new StartCreateGridEvent());
            else
                _messageHub.Publish(new SpawnWithGridPresetEvent(GridPreset));
            _messageHub.Publish(new SpawnWithBlockPresetEvent(null));
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
                case ScoreTypes.Chain:
                    if (scoreEvent.Contacts.Count <= 1) return;
                    finalScore = (int)Mathf.Pow(scoreEvent.Contacts.Count - 1, 2f) * _config.ScorePerChain;
                    break;
                case ScoreTypes.FitMe:
                    ChangeFitMe(1);
                    _popUpScoreFactory.Create(1, scoreEvent.WorldPosition, "Fitme");
                    finalScore = _config.ScorePerFitMe * scoreEvent.Contacts.Count; 
                    break;
            }
            DebugUtils.Log($"Score added: {finalScore} (Type: {scoreEvent.ScoreType}, Contacts: {scoreEvent.Contacts.Count})");
            ChangeScore(finalScore);
            _popUpScoreFactory.Create(finalScore, scoreEvent.WorldPosition, "Score");
        }
        
        #region GameMode
        public void CheckGameMode()
        {
            switch (GameMode)
            {
                case GameMode.Original:
                    OriginalMode();
                    break;
                case GameMode.LevelShape:
                    LevelShapeMode();
                    break;
            }
        }
        
        private void OriginalMode()
        {
            GridPreset = _config.OriginalLevel;
        }
        
        private void LevelShapeMode()
        {
            GridPreset = GetLevelFromPool();
        }
        
        private GridPreset GetLevelFromPool()
        {
            if (_presets == null || _presets.Count == 0)
            {
                CreateLevelPool();
            }
            var preset = _presets.GetRandomElement();
            _presets?.Remove(preset);
            return preset;
        }

        private void CreateLevelPool()
        {
            _presets = new List<GridPreset>(_config.LevelShapeLevel);
        }
        
        public void ResetLevelPool()
        {
            _presets = null;
            GridPreset = null;
        }
        #endregion

        private void OnFit()
        {
            GridPreset = GetLevelFromPool();
            _messageHub.Publish(new SpawnWithGridPresetEvent(GridPreset));
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
            _saveManager.Save(_playerRecordSaveObject);
        }
        
        private void ChangeFitMe(int value)
        {
            FitMeScore.Value += value;
            if (!_playerRecordSaveObject) return;
            var saveData = _playerRecordSaveObject.GetSaveData<PlayerRecordSaveData>();
            if (saveData == null) return;
            saveData.cumulativeFitMe += value;
            _saveManager.Save(_playerRecordSaveObject);
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

        private void OnGameOverEvent(GameOverEvent evt)
        {
            if (!evt.IsOver) return;
            Debug.Log("Game Over Event Received!");
            GameOver();
        }
        
        private void GameOver()
        {
            _panelManager.TryGetPanel(_config.GameOverPanelId, out var panel);
            if (panel is GameOverPanelViewModel gameOverPanelViewModel)
            {
                if (gameOverPanelViewModel.RemainingContinueCount.CurrentValue <= 0)
                {
                    _panelManager.Crossfade(_config.GameplayPanelId, _config.ResultPanelId, 
                        new CrossfadeSettings
                        {
                            crossFadeType = CrossfadeType.InOnly
                        });
                    return;
                }
            }
            _panelManager.Crossfade(_config.GameplayPanelId, _config.GameOverPanelId, 
                new CrossfadeSettings
                {
                    crossFadeType = CrossfadeType.InOnly
                });
        }
    }
}