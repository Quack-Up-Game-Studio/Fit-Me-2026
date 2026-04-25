using System;
using Cysharp.Threading.Tasks;
using FitMe.Grid;
using FitMe.Shared;
using MessagePipe;
using R3;
using UnityEngine;
using VContainer;
using DisposableBag = R3.DisposableBag;

namespace FitMe.Tutorial
{
    [Serializable]
    public class FitTutorialState : TutorialState, IDisposable
    {
        [SerializeField] private string jumpToWhenFail;
        
        private GridManager _gridManager;
        private DisposableBag _subscription;
        private ISubscriber<NoPlaceableBlockEvent> _noPlaceableBlockEvent;
        
        [Inject]
        public void SetUpGridManager(GridManager gridManager, ISubscriber<NoPlaceableBlockEvent> noPlaceableBlockEvent)
        {
            _gridManager = gridManager;
            _noPlaceableBlockEvent = noPlaceableBlockEvent;
        }

        public override async UniTask Enter()
        {
            await base.Enter();
            _subscription.Dispose();
            _subscription = new DisposableBag();
            _gridManager.OnFitCheck
                .Where(x => x.FitType is FitType.FitMe)
                .Subscribe(_ => StateMachine.Next().Forget())
                .AddTo(ref _subscription);
            _noPlaceableBlockEvent
                .Subscribe(_ => StateMachine.JumpTo(jumpToWhenFail).Forget())
                .AddTo(ref _subscription);
        }
        
        public override async UniTask Exit()
        {
            await base.Exit();
            Dispose();
        }

        public void Dispose()
        {
            _subscription.Dispose();
        }
    }
}