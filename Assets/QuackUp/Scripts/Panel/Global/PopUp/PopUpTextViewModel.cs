using System;
using FitMe.Grid;
using QuackUp.Utils;
using R3;
using VContainer;

namespace FitMe.Panel
{
    public class PopUpTextViewModel : IDisposable
    {
        public ReactiveCommand<(string text, Promise<Unit> promise)> ShowCommand { get; } = new();
        
        private readonly GridManager _gridManager;
        private IDisposable _subscription;

        [Inject]
        public PopUpTextViewModel(GridManager gridManager)
        {
            _gridManager = gridManager;
            Subscribe();
        }

        private void Subscribe()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            _gridManager.OnScoreAdded
                .Where(x => x.ScoreType is ScoreTypes.FitMe)
                .Subscribe(_ => ShowCommand.Execute(("Fit!", new Promise<Unit>())))
                .AddTo(ref disposableBuilder);
            _subscription = disposableBuilder.Build();
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }
    }
}