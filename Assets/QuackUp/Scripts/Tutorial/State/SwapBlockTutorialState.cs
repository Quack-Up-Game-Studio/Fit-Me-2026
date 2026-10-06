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
        private CancellationTokenSource _hideCts;

        [Inject]
        public void SetBlockManager(BlockManager blockManager)
        {
            _blockManager = blockManager;
            swapBlockHint.gameObject.SetActive(false);
        }

        public override async UniTask Enter()
        {
            await base.Enter();
            swapBlockHint.Initialize();
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
            var tokenSource = _hintCts;
            _hintCts = null;
            _hideCts = tokenSource;
            try
            {
                await swapBlockHint.TransitionOut(tokenSource.Token);
            }
            finally
            {
                if (ReferenceEquals(_hideCts, tokenSource)) _hideCts = null;
                tokenSource.Dispose();
            }
        }
        
        private void CancelHint()
        {
            var tokenSource = _hintCts;
            _hintCts = new CancellationTokenSource();
            CancelAndDispose(tokenSource);

            tokenSource = _hideCts;
            _hideCts = null;
            CancelAndDispose(tokenSource);
        }

        private static void CancelAndDispose(CancellationTokenSource tokenSource)
        {
            if (tokenSource == null) return;
            try { tokenSource.Cancel(); }
            finally { tokenSource.Dispose(); }
        }

        public void Dispose()
        {
            var tokenSource = _hintCts;
            _hintCts = null;
            CancelAndDispose(tokenSource);
            _subscription?.Dispose();
            _subscription = null;
        }
    }
}