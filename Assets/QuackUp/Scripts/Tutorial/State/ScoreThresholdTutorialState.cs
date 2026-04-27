using System;
using Cysharp.Threading.Tasks;
using FitMe.Shared;
using R3;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace FitMe.Tutorial
{
    [Serializable]
    public class ScoreThresholdTutorialState : TutorialState, IDisposable
    {
        [SerializeField] private int scoreThreshold;
        [SerializeField] private string jumpToWhenFail;
        
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
            _subscription = _scoreManager.OnScoreUpdated.Subscribe(_ => OnScoreUpdated());
        }

        private void OnScoreUpdated()
        {
            if (_scoreManager.Score.CurrentValue >= scoreThreshold)
            {
                StateMachine.Next().Forget();
            }
            else
            {
                StateMachine.JumpTo(jumpToWhenFail).Forget();
            }
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