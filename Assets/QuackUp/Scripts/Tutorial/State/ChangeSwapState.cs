using System;
using Cysharp.Threading.Tasks;
using FitMe.Grid;
using UnityEngine;
using VContainer;

namespace FitMe.Tutorial
{
    [Serializable]
    public class ChangeSwapState : TutorialState
    {
        [SerializeField] private bool allowSwapping;
        
        private BlockManager _blockManager;
        
        [Inject]
        public void SetBlockManager(BlockManager blockManager)
        {
            _blockManager = blockManager;
        }

        public override async UniTask Enter()
        {
            await base.Enter();
            _blockManager.AllowSwapping = allowSwapping;
            StateMachine.Next().Forget();
        }
    }
}