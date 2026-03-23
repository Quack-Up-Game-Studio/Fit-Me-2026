using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using FitMe.Grid;
using FitMe.Panel;
using QuackUp.Utils;
using R3;
using UnityEngine;
using VContainer;

namespace FitMe.Tutorial
{
    [Serializable]
    public class PlaceBlockTutorialState : TextTutorialState, IDisposable
    {
        [SerializeField] private GeneralFloatingUIElement placeBlockHint;
        
        private BlockManager _blockManager;
        private DisposableBag _blockPlacedSubscription;
        private CancellationTokenSource _hintCts = new();
        
        [Inject]
        public void SetBlockManager(BlockManager blockManager)
        {
            _blockManager = blockManager;
            placeBlockHint.Initialize();
            placeBlockHint.gameObject.SetActive(false);
        }

        public override async UniTask Enter()
        {
            if (_blockManager == null)
            {
                DebugUtils.LogError($"{GetType().Name}: BlockManager is not set.");
                return;
            }
            _blockPlacedSubscription = new DisposableBag();
            foreach (var block in _blockManager.BlockOnHand)
            {
                block.ViewModel.BlockInteractionState
                    .IgnoreFirstValueWhenSubscribe()
                    .Subscribe(OnBlockInteractionStateChanged)
                    .AddTo(ref _blockPlacedSubscription);
            }
            await base.Enter();
            await ViewModel.ChangeInputBlockState(false);
            ShowHint().Forget();
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
                    ViewModel.Show().Forget();
                    break;
                case BlockInteractionState.PickUp:
                    HideHint().Forget();
                    ViewModel.Hide().Forget();
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
            _blockPlacedSubscription.Dispose();
        }
    }
}