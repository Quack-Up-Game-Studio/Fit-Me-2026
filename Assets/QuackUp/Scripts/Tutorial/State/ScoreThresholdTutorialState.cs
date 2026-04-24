using System;
using Cysharp.Threading.Tasks;
using FitMe.Shared;
using R3;
using UnityEngine;
using VContainer;

namespace FitMe.Tutorial
{
    [Serializable]
    public class ScoreThresholdTutorialState : TutorialState, IDisposable
    {
        [SerializeField] private int scoreThreshold;
        
        private IScoreManager _scoreManager;
        private IDisposable _subscription;
        
        [Inject]
        public void SetScoreManager(IScoreManager scoreManager)
        {
            _scoreManager = scoreManager;
        }

        public override async UniTask Enter()
        {
            await base.Enter();
            _subscription = _scoreManager.Score
                .Where(score => score >= scoreThreshold)
                .Subscribe(_ => StateMachine.Next().Forget());
        }
        
        public override async UniTask Exit()
        {
            await base.Exit();
            Dispose();
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }
    }
}