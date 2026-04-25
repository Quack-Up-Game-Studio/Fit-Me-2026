using System;
using Cysharp.Threading.Tasks;
using FitMe.Shared;
using R3;
using UnityEngine;
using VContainer;

namespace FitMe.Tutorial
{
    [Serializable]
    public class NextPresetState : TutorialState
    {
        [SerializeField] private bool playFitMeSound;
        
        private ILevelManager _levelManager;
        
        [Inject]
        public void SetLevelManager(ILevelManager levelManager)
        {
             _levelManager = levelManager;
        }
        
        public override async UniTask Enter()
        {
            await base.Enter();
            await _levelManager.NextTutorialPreset(playFitMeSound);
            StateMachine.Next().Forget();
        }
    }
}