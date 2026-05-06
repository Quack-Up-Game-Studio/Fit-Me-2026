using System;
using Cysharp.Threading.Tasks;
using FitMe.Grid;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;
using VContainer;

namespace FitMe.Tutorial
{
    [Serializable]
    public class FailState : TextTutorialState
    {
        [SerializeField] private bool revertToPreviousState;
        [SerializeField] private bool playFitSound = true;
        [SerializeField, HideIf(nameof(revertToPreviousState))] private string jumpTo;
        
        private GridManager _gridManager;
        
        [Inject]
        public void SetUpGridManager(GridManager gridManager)
        {
            _gridManager = gridManager;
        }

        public override void OnNext()
        {
            if (revertToPreviousState)
            {
                StateMachine.Revert().Forget();
            }
            else
            {
                StateMachine.JumpTo(jumpTo).Forget();
            }
        }

        public override async UniTask Exit()
        {
            await base.Exit();
            await _gridManager.ResetGrid(playFitSound);
        }
    }
}