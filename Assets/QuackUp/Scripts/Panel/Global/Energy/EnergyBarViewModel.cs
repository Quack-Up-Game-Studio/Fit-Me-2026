using System;
using FitMe.GameData;
using QuackUp.Utils;
using R3;
using VContainer;

namespace FitMe.Panel
{
    public class EnergyBarViewModel : IDisposable
    {
        public ReactiveProperty<bool> AllowWatchAd { get; } = new(true);
        public ReactiveCommand WatchAdCommand { get; } = new();
        public ReadOnlyReactiveProperty<int> CurrentEnergy => _energyManager.CurrentEnergy;
        public ReadOnlyReactiveProperty<TimeSpan> TimeUntilNextRecharge => _energyManager.TimeUntilNextRecharge;
        public EnergyManagerConfig Config => _energyManager.Config;
        
        private readonly EnergyManager _energyManager;
        private readonly AdsService _adsService;

        private IDisposable _bindings;
        private IDisposable _adSubscription;

        [Inject]
        public EnergyBarViewModel(
            EnergyManager energyManager,
            AdsService adsService)
        {
            _energyManager = energyManager;
            _adsService = adsService;
            Bind();
        }

        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            
            WatchAdCommand
                .Subscribe(_ => OnWatchAd())
                .AddTo(ref disposableBuilder);
            
            _bindings = disposableBuilder.Build();
        }
        
        public void Dispose()
        {
            _bindings?.Dispose();
            _adSubscription?.Dispose();
        }
        
        private void OnWatchAd()
        {
            if (!AllowWatchAd.Value) return;
            if (!_adsService.TryGetAdsInstance<RewardedAdInstance>(out var rewardedAd)) return;
            _adSubscription = rewardedAd.OnUserEarnedReward
                .Subscribe(_ => OnAdSuccess());
            rewardedAd.TryShow();
        }
        
        private void OnAdSuccess()
        {
            _energyManager.ChangeEnergy(1);
            _adSubscription?.Dispose();
        }
    }
}