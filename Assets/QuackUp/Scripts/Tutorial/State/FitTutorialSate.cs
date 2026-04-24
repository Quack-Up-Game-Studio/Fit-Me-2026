using System;
using Cysharp.Threading.Tasks;
using FitMe.Grid;
using R3;
using VContainer;

namespace FitMe.Tutorial
{
    public class FitTutorialSate : TutorialState, IDisposable
    {
        private GridManager _gridManager;
        private IDisposable _subscription;
        
        [Inject]
        public void SetUpGridManager(GridManager gridManager)
        {
            _gridManager = gridManager;
        }

        public override async UniTask Enter()
        {
            await base.Enter();
            _subscription = _gridManager.OnFitCheck
                .Where(x => x.FitType is FitType.FitMe)
                .Subscribe(_ => StateMachine.Next().Forget());
        }
        
        public override async UniTask Exit()
        {
            await base.Exit();
            Dispose();
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }
    }
}