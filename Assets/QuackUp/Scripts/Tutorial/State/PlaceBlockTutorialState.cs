using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using FitMe.Grid;
using FitMe.Panel;
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
        private IMessageHub _messageHub;
        private IDisposable _blockSpawnedSubscription;
        private DisposableBag _blockPlacedSubscription;
        
        private CancellationTokenSource _hintCts = new();
        private CancellationTokenSource _hideCts;
        
        [Inject]
        public void SetBlockSubscription(BlockManager blockManager, [Key(BlockManagerMessageHub.MessageHubKey)] IMessageHub messageHub)
        {
            _blockManager  = blockManager;
            _messageHub = messageHub;
            placeBlockHint.gameObject.SetActive(false);
        }

        public override async UniTask Enter()
        {
            _blockSpawnedSubscription = _messageHub
                .Subscribe<BlockSpawnedEvent>(x => OnBlockSpawned(x.BlockInstances));
            OnBlockSpawned(_blockManager.BlockOnHand);
            await base.Enter();
            placeBlockHint.Initialize();
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
            var tokenSource = _hintCts;
            _hintCts = null;
            _hideCts = tokenSource;
            try
            {
                await placeBlockHint.TransitionOut(tokenSource.Token);
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
            HideHint().Forget();
            Dispose();
        }

        public void Dispose()
        {
            var tokenSource = _hintCts;
            _hintCts = null;
            CancelAndDispose(tokenSource);
            _blockSpawnedSubscription?.Dispose();
            _blockSpawnedSubscription = null;
            _blockPlacedSubscription.Dispose();
            _blockPlacedSubscription = default;
        }
    }
}