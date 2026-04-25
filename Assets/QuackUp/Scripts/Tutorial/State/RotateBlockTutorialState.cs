using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using FitMe.Grid;
using FitMe.Panel;
using MessagePipe;
using QuackUp.Utils;
using R3;
using UnityEngine;
using VContainer;
using DisposableBag = R3.DisposableBag;

namespace FitMe.Tutorial
{
    [Serializable]
    public class RotateBlockTutorialState : TutorialState, IDisposable
    {
        [SerializeField] private GeneralFloatingUIElement rotateBlockHint;
        
        private BlockManager _blockManager;
        private ISubscriber<BlockSpawnedEvent> _blockSpawnedEvent;
        private IDisposable _blockSpawnedSubscription;
        private DisposableBag _blockRotateSubscription;
        
        private CancellationTokenSource _hintCts = new();
        
        [Inject]
        public void SetBlockManager(BlockManager blockManager, ISubscriber<BlockSpawnedEvent> blockSpawnedEvent)
        {
            _blockManager  = blockManager;
            _blockSpawnedEvent = blockSpawnedEvent;
            rotateBlockHint.Initialize();
            rotateBlockHint.gameObject.SetActive(false);
           
        }
        
        public override async UniTask Enter()
        {
            _blockSpawnedSubscription = _blockSpawnedEvent
                .Subscribe(x => OnBlockSpawned(x.BlockInstances));
            OnBlockSpawned(_blockManager.BlockOnHand);
            await base.Enter();
            ShowHint().Forget();
        }
        
        public override async UniTask Exit()
        {
            await base.Exit();
            HideHint().Forget();
            Dispose();
        }
        
        private void OnBlockSpawned(IEnumerable<BlockInstance> data)
        {
            _blockRotateSubscription.Dispose();
            _blockRotateSubscription = new DisposableBag();
            foreach (var block in data)
            {
                block.Controller.AllowDrag = false;
                block.Controller.OnRotate
                    .Subscribe(_ => 
                    { 
                        StateMachine.Next().Forget(); 
                    })
                    .AddTo(ref _blockRotateSubscription);
            }
        }
        
        private async UniTask ShowHint()
        {
            CancelHint();
            var token = _hintCts.Token;
            rotateBlockHint.Reset();
            await rotateBlockHint.TransitionIn(token);
            rotateBlockHint.Animate(token).Forget();
        }
        
        private async UniTask HideHint()
        {
            CancelHint();
            var token = _hintCts.Token;
            await rotateBlockHint.TransitionOut(token);
        }
        
        private void CancelHint()
        {
            _hintCts?.Cancel();
            _hintCts?.Dispose();
            _hintCts = new CancellationTokenSource();
        }

        public void Dispose()
        {
            _blockSpawnedSubscription?.Dispose();
            _blockRotateSubscription.Dispose();
        }
    }
}