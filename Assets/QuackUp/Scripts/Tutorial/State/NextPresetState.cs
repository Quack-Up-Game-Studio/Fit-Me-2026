using System;
using Cysharp.Threading.Tasks;
using FitMe.Shared;
using R3;
using UnityEngine;
using VContainer;

namespace FitMe.Tutorial
{
    [Serializable]
    public class NextPresetState : TutorialState, IDisposable
    {
        [SerializeField] private bool playFitMeSound;
        
        private ILevelManager  _levelManager;
        private IDisposable _delayTimer;
        
        [Inject]
        public void SetLevelManager(ILevelManager levelManager)
        {
             _levelManager = levelManager;
        }
        
        public override async UniTask Enter()
        {
            await base.Enter();
            await _levelManager.NextTutorialPreset(playFitMeSound);
            _delayTimer = Observable.TimerFrame(1)
                .Subscribe(_ => StateMachine.Next().Forget());
        }

        public void Dispose()
        {
            _delayTimer?.Dispose();
        }
    }
}