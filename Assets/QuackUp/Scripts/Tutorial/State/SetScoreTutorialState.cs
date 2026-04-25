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
    public class SetScoreTutorialState : TutorialState, IDisposable
    {
        [SerializeField] private bool waitForScoreUpdate = true;
        [SerializeField] private bool setScore;
        [SerializeField, ShowIf(nameof(setScore))] private int scoreToSet;
        [SerializeField] private bool setFitMe;
        [SerializeField, ShowIf(nameof(setFitMe))] private int fitMeToSet;
        
        private IScoreManager _scoreManager;
        private IDisposable _onScoreUpdated;
        
        [Inject]
        public void SetScoreManager(IScoreManager scoreManager)
        {
            _scoreManager = scoreManager;
        }

        public override async UniTask Enter()
        {
            await base.Enter();
            if (!waitForScoreUpdate)
            {
                OnScoreUpdated();
                return;
            }
            _onScoreUpdated = _scoreManager.OnScoreUpdated.Subscribe(_ => OnScoreUpdated());
        }

        public override async UniTask Exit()
        {
            await base.Exit();
            Dispose();
        }

        private void OnScoreUpdated()
        {
            if (setScore) _scoreManager.SetScore(scoreToSet);
            if (setFitMe) _scoreManager.SetFitMe(fitMeToSet);
            StateMachine.Next().Forget();
        }

        public void Dispose()
        {
            _onScoreUpdated?.Dispose();
        }
    }
}