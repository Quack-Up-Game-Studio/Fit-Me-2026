using System;
using Cysharp.Threading.Tasks;
using FitMe.GameData;
using QuackUp.Save;
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
        
        private PlayerRecordSaveObject _playerRecordSaveObject;
        private readonly MessagePackSaveManager _saveManager;
        private readonly EnergyManager _energyManager;
        private readonly OutOfEnergyManager _outOfEnergyManager;
        private IDisposable _bindings;
        
        [Inject]
        public MainMenuPanelViewModel(
            PanelManager panelManager,
            EnergyManager energyManager,
            MessagePackSaveManager saveManager,
            OutOfEnergyManager outOfEnergyManager) : base(panelManager)
        {
            _saveManager  = saveManager;
            _energyManager = energyManager;
            _outOfEnergyManager = outOfEnergyManager;
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
            await _saveManager.SaveDataReady;
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
            _bindings = disposableBuilder.Build();
        }
        
        public override void Dispose()
        {
            base.Dispose();
            _bindings?.Dispose();
        }
    }
}