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
    public class PlaceBlockTutorialState : TutorialState, IDisposable
    {
        [SerializeField] private GeneralFloatingUIElement placeBlockHint;

        private BlockManager _blockManager;
        private ISubscriber<BlockSpawnedEvent> _blockSpawnedEvent;
        private IDisposable _blockSpawnedSubscription;
        private DisposableBag _blockPlacedSubscription;
        
        private CancellationTokenSource _hintCts = new();
        
        [Inject]
        public void SetBlockSubscription(BlockManager blockManager, ISubscriber<BlockSpawnedEvent> blockSpawnedEvent)
        {
            _blockManager  = blockManager;
            _blockSpawnedEvent = blockSpawnedEvent;
            placeBlockHint.Initialize();
            placeBlockHint.gameObject.SetActive(false);
        }

        public override async UniTask Enter()
        {
            _blockSpawnedSubscription = _blockSpawnedEvent
                .Subscribe(x => OnBlockSpawned(x.BlockInstances));
            OnBlockSpawned(_blockManager.BlockOnHand);
            await base.Enter();
            ShowHint().Forget();
        }

        private void OnBlockSpawned(IEnumerable<BlockInstance> data)
        {
            _blockPlacedSubscription.Dispose();
            _blockPlacedSubscription = new DisposableBag();
            foreach (var block in data)
            {
                block.ViewModel.BlockInteractionState
                    .IgnoreFirstValueWhenSubscribe()
                    .Subscribe(OnBlockInteractionStateChanged)
                    .AddTo(ref _blockPlacedSubscription);
            }
        }

        private async UniTask ShowHint()
        {
            CancelHint();
            var token = _hintCts.Token;
            placeBlockHint.Reset();
            await placeBlockHint.TransitionIn(token);
            placeBlockHint.Animate(token).Forget();
        }
        
        private async UniTask HideHint()
        {
            CancelHint();
            var token = _hintCts.Token;
            await placeBlockHint.TransitionOut(token);
        }
        
        private void CancelHint()
        {
            _hintCts?.Cancel();
            _hintCts?.Dispose();
            _hintCts = new CancellationTokenSource();
        }

        private void OnBlockInteractionStateChanged(BlockInteractionState state)
        {
            switch (state)
            {
                case BlockInteractionState.PlacedOnSpawn:
                    ShowHint().Forget();
                    break;
                case BlockInteractionState.PickUp:
                    HideHint().Forget();
                    break;
                case BlockInteractionState.PlacedOnGrid:
                    StateMachine.Next().Forget();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }

        public override async UniTask Exit()
        {
            await base.Exit();
            Dispose();
        }

        public void Dispose()
        {
            _blockSpawnedSubscription?.Dispose();
            _blockPlacedSubscription.Dispose();
        }
    }
}