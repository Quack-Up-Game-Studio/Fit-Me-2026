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
        private readonly IGameStateManager _gameStateManager;
        private IDisposable _bindings;
        private IDisposable _timer;
        
        [Inject]
        public RunTimerViewModel(IGameStateManager gameStateManager)
        {
            _gameStateManager = gameStateManager;
             Bind();
        }
        
        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            _gameStateManager.IsPaused
                .Subscribe(OnPauseStateChanged)
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }
        
        public void Dispose()
        {
            _bindings?.Dispose();
            StopTimer();
        }

        private void OnPauseStateChanged(bool paused)
        {
            if (!paused && _timer == null)
            {
                StartTimer();
                return;
            }
            StopTimer();
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