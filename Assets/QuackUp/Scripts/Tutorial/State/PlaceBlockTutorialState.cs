using System;
using Cysharp.Threading.Tasks;
using FitMe.Grid;
using QuackUp.Utils;
using R3;
using VContainer;

namespace FitMe.Tutorial
{
    public class PlaceBlockTutorialState : TextTutorialState, IDisposable
    {
        private BlockManager _blockManager;
        private DisposableBag _blockPlacedSubscription;
        
        [Inject]
        public void SetBlockManager(BlockManager blockManager)
        {
            _blockManager = blockManager;
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
        }

        private void OnBlockInteractionStateChanged(BlockInteractionState state)
        {
            switch (state)
            {
                case BlockInteractionState.PlacedOnSpawn:
                    ViewModel.Show().Forget();
                    break;
                case BlockInteractionState.PickUp:
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