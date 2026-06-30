using System;
using QuackUp.GoogleAdMob;
using Cysharp.Threading.Tasks;
using FitMe.GameData;
using FitMe.Grid;
using MessagePipe;
using QuackUp.Save;
using QuackUp.Utils;
using R3;
using UnityEngine;
using VContainer;

namespace FitMe.Panel
{
    public class MainMenuPanelViewModel : PanelViewModel
    {
        public ReactiveCommand WatchAdCommand { get; } = new();
        public ReactiveCommand ToTutorial { get; private set; } = new();
        public ReactiveProperty<bool> CompletedTutorial { get; private set; } = new(false);
        public ReactiveProperty<bool> IsSaveLoading { get; private set; } = new(true);
        public bool IsSaveReady => _saveManager.IsSaveReady;
        public ReadOnlyReactiveProperty<int> RemainingAdCount => _outOfEnergyManager.RemainingAdCount;
        public ReadOnlyReactiveProperty<TimeSpan> TimeUntilNextWatchAd => _outOfEnergyManager.TimeUntilNextWatchAd;
        public int MaxAdCount => _outOfEnergyManager.MaxAdCount;
        public ReadOnlyReactiveProperty<int> CurrentEnergy => _energyManager.CurrentEnergy;
        public ReadOnlyReactiveProperty<bool> InfiniteEnergy => _energyManager.InfiniteEnergy;
        public bool HasEnoughEnergy(uint amount) => _energyManager.HasEnoughEnergy(amount);
        public ReadOnlyReactiveProperty<bool> AdsEnabled => _adsService.AdsEnabled;
        
        public ReadOnlyReactiveProperty<bool> ShowPlaceBlockHint => _showPlaceBlockHint;
        private readonly ReactiveProperty<bool> _showPlaceBlockHint = new(false);
        
        private PlayerRecordSaveObject _playerRecordSaveObject;
        private readonly MessagePackSaveManager _saveManager;
        private readonly EnergyManager _energyManager;
        private readonly OutOfEnergyManager _outOfEnergyManager;
        private readonly AdsService _adsService;
        private readonly ISubscriber<BlockSpawnedEvent> _blockSpawnedSubscriber;
        private IDisposable _bindings;
        private IDisposable _blockBindings;
        
        [Inject]
        public MainMenuPanelViewModel(
            PanelManager panelManager,
            EnergyManager energyManager,
            MessagePackSaveManager saveManager,
            OutOfEnergyManager outOfEnergyManager,
            AdsService adsService,
            ISubscriber<BlockSpawnedEvent> blockSpawnedSubscriber) : base(panelManager)
        {
            _saveManager  = saveManager;
            _energyManager = energyManager;
            _outOfEnergyManager = outOfEnergyManager;
            _adsService = adsService;
            _blockSpawnedSubscriber = blockSpawnedSubscriber;
            Bind();
        }

        protected override void OnVisible()
        {
            base.OnVisible();
            InitializeAsync().Forget();
        }

        private async UniTaskVoid InitializeAsync()
        {
            IsSaveLoading.Value = true;
            await _saveManager.WaitForSaveDataReady;
            _playerRecordSaveObject = _saveManager.GetFirstSaveObjectOfType<PlayerRecordSaveObject>();
            CompletedTutorial.Value = _playerRecordSaveObject.GetSaveData<PlayerRecordSaveData>().CompletedTutorial;
            IsSaveLoading.Value = false;
        }

        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            WatchAdCommand
                .Subscribe(_ => _outOfEnergyManager.WatchAds())
                .AddTo(ref disposableBuilder);
            _blockSpawnedSubscriber
                .Subscribe(OnBlockSpawned)
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }

        private void OnBlockSpawned(BlockSpawnedEvent data)
        {
            _blockBindings?.Dispose();
            if (data.BlockInstances == null || data.BlockInstances.Count == 0) return;
            
            var disposableBuilder = Disposable.CreateBuilder();
            var viewModel = data.BlockInstances[0].ViewModel;
            
            _showPlaceBlockHint.Value = true;
            
            viewModel.BlockInteractionState
                .Subscribe(state =>
                {
                    switch (state)
                    {
                        case BlockInteractionState.PlacedOnSpawn:
                            _showPlaceBlockHint.Value = true;
                            break;
                        case BlockInteractionState.PickUp:
                            _showPlaceBlockHint.Value = false;
                            break;
                        case BlockInteractionState.PlacedOnGrid:
                            _showPlaceBlockHint.Value = false;
                            break;
                    }
                })
                .AddTo(ref disposableBuilder);
                
            _blockBindings = disposableBuilder.Build();
        }
        
        public override void Dispose()
        {
            base.Dispose();
            _bindings?.Dispose();
            _blockBindings?.Dispose();
        }
    }
}