using System;
using FitMe.Entity;
using FitMe.Grid;
using FitMe.Scene.UI.Score;
using QuackUp.Utils;
using R3;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Scene
{
    public class LevelManager : IStartable
    {
        public ReactiveProperty<int> Score { get; private set; } = new(0);
        public ReactiveProperty<int> FitmeScore { get; private set; } = new(0);
        
        private readonly LevelManagerConfig _config;
        private readonly EntityManager _entityManager;
        private readonly IMessageHub _messageHub;
        private readonly GridManager _gridManager;
        private readonly PopUpScoreFactory _popUpScoreFactory;
        
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
            Subscribe();
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
            var previousFitMe = FitmeScore.Value;

            switch (scoreEvent.ScoreType)
            {
                case ScoreTypes.Placement:
                    finalScore = _config.scorePerPlacement;
                    break;
                /*case ScoreTypes.PreInfect:
                    finalScore = scorePerPreInfect;
                    break;*/
                case ScoreTypes.Combo:
                    if (scoreEvent.ContactCount <= 1) return;
                    finalScore = _config.scorePerCombo * (scoreEvent.ContactCount - 1);
                    break;
                case ScoreTypes.Bomb:
                    if (scoreEvent.ContactCount <= 2) return;
                    finalScore = _config.scorePerBomb * scoreEvent.ContactCount;    
                    break;
                case ScoreTypes.FitMe:
                    ChangeFitMe(1);
                    Vector3 screenPosition1 = Camera.main.WorldToScreenPoint(scoreEvent.WorldPosition);
                    _popUpScoreFactory.Create(1, screenPosition1, "Fitme");
                    finalScore = _config.scorePerFitMe; 
                    break;
            }
            ChangeScore(finalScore);
            //บรรทัดล่าง กรณีที่ Render mode = Screen Space - Overlay ห้ามลบเด็ดขาด!!!
            Vector3 screenPosition = Camera.main.WorldToScreenPoint(scoreEvent.WorldPosition);
            _popUpScoreFactory.Create(finalScore, screenPosition, "Score");
        }
        
        private void ChangeScore(int value)
        {
            Score.Value += value;
        }
        
        private void ChangeFitMe(int value)
        {
            FitmeScore.Value += value;
        }
    }
}