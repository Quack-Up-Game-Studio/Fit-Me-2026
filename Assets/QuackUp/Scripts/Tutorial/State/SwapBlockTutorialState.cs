using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using FitMe.Grid;
using FitMe.Panel;
using R3;
using UnityEngine;
using VContainer;

namespace FitMe.Tutorial
{
    [Serializable]
    public class SwapBlockTutorialState : TutorialState, IDisposable
    {
        [SerializeField] private GeneralFloatingUIElement swapBlockHint; 
        
        private IDisposable _subscription;
        private BlockManager _blockManager;
        
        private CancellationTokenSource _hintCts = new();
        
        [Inject]
        public void SetBlockManager(BlockManager blockManager)
        {
            _blockManager = blockManager;
            swapBlockHint.Initialize();
            swapBlockHint.gameObject.SetActive(false);
        }

        public override async UniTask Enter()
        {
            await base.Enter();
            _subscription = _blockManager.OnSwap
                .Subscribe(_ => 
                { 
                    StateMachine.Next().Forget(); 
                });
            ShowHint().Forget();
        }

        public override async UniTask Exit()
        {
            await base.Exit();
            foreach (var block in _blockManager.BlockOnHand)
            {
                block.Controller.AllowDrag = true;
            }
            HideHint().Forget();
            Dispose();
        }
        
        private async UniTask ShowHint()
        {
            CancelHint();
            var token = _hintCts.Token;
            swapBlockHint.Reset();
            await swapBlockHint.TransitionIn(token);
            swapBlockHint.Animate(token).Forget();
        }
        
        private async UniTask HideHint()
        {
            CancelHint();
            var token = _hintCts.Token;
            await swapBlockHint.TransitionOut(token);
        }
        
        private void CancelHint()
        {
            _hintCts?.Cancel();
            _hintCts?.Dispose();
            _hintCts = new CancellationTokenSource();
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }
    }
}