using System;
using QuackUp.Utils;
using R3;
using VContainer;

namespace FitMe.Panel
{
    public class OutOfEnergyViewModel : IDisposable
    {
        public ReactiveCommand<Promise<Unit>> TransitionInCommand { get; } = new();
        public ReactiveCommand<Promise<Unit>> TransitionOutCommand { get; } = new();
        public ReactiveCommand WatchAdsCommand { get; } = new();
        public ReactiveCommand ToShopCommand { get; } = new();
        public ReadOnlyReactiveProperty<int> RemainingAdCount => _manager.RemainingAdCount;
        public ReadOnlyReactiveProperty<TimeSpan> TimeUntilNextWatchAd => _manager.TimeUntilNextWatchAd;
        public int MaxAdCount => _manager.MaxAdCount;

        private readonly OutOfEnergyManager _manager;
        private IDisposable _bindings;
        
        [Inject]
        public OutOfEnergyViewModel(OutOfEnergyManager manager)
        {
            _manager = manager;
            Bind();
        }

        private void Bind()
        {
            var builder = Disposable.CreateBuilder();
            WatchAdsCommand
                .Subscribe(_ => _manager.WatchAds())
                .AddTo(ref builder);
            _manager.TransitionInCommand
                .Subscribe(transitionInPromise => TransitionInCommand.Execute(transitionInPromise))
                .AddTo(ref builder);
            _manager.TransitionOutCommand
                .Subscribe(transitionOutPromise => TransitionOutCommand.Execute(transitionOutPromise))
                .AddTo(ref builder);
            ToShopCommand
                .Subscribe(_ => _manager.ToShop())
                .AddTo(ref builder);
            _bindings = builder.Build();
        }

        public void Dispose()
        {
            _bindings?.Dispose();
        }
    }
}