using System;
using Cysharp.Threading.Tasks;
using FitMe.Shared;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace FitMe.Tutorial
{
    [Serializable]
    public class SetScoreTutorialState : TutorialState
    {
        [SerializeField] private bool setScore;
        [SerializeField, ShowIf(nameof(setScore))] private int scoreToSet;
        [SerializeField] private bool setFitMe;
        [SerializeField, ShowIf(nameof(setFitMe))] private int fitMeToSet;
        
        private IScoreManager _scoreManager;
        
        [Inject]
        public void SetScoreManager(IScoreManager scoreManager)
        {
            _scoreManager = scoreManager;
        }

        public override async UniTask Enter()
        {
            await base.Enter();
            if (setScore) _scoreManager.SetScore(scoreToSet);
            if (setFitMe) _scoreManager.SetFitMe(fitMeToSet);
            StateMachine.Next().Forget();
        }
    }
}