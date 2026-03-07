using System;
using FitMe.Shared;
using QuackUp.Utils;
using R3;
using VContainer;

namespace FitMe.Panel
{
    public class RunTimerViewModel : IDisposable
    {
        public ReadOnlyReactiveProperty<TimeSpan> ElapsedTime => _elapsedTime.ToReadOnlyReactiveProperty();
        private readonly ReactiveProperty<TimeSpan> _elapsedTime = new(TimeSpan.Zero);
        private readonly ILevelManager _levelManager;
        private IDisposable _bindings;
        private IDisposable _timer;
        
        [Inject]
        public RunTimerViewModel(ILevelManager levelManager)
        {
            _levelManager = levelManager;
             Bind();
        }
        
        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            _levelManager.GameState
                .Subscribe(OnGameStateChanged)
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }
        
        public void Dispose()
        {
            _bindings?.Dispose();
            StopTimer();
        }
        
        private void OnGameStateChanged(GameState gameState)
        {
            DebugUtils.Log($"Run Timer: GameState changed to {gameState}");
            switch (gameState)
            {
                case GameState.CountOff:
                case GameState.PlaceBlock:
                    if (_timer == null)
                    {
                        StartTimer();
                    }
                    break;
                case GameState.GameClear:
                case GameState.GameOver:
                case GameState.Pause:
                    StopTimer();
                    break;
                case GameState.ClearingGrid:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(gameState), gameState, null);
            }
        }
        
        private void StartTimer()
        {
            _timer = Observable.Interval(TimeSpan.FromSeconds(1))
                .Subscribe(_ =>
                {
                    _elapsedTime.Value += TimeSpan.FromSeconds(1);
                });
        }

        private void StopTimer()
        {
            _timer?.Dispose();
            _timer = null;
        }
    }
}