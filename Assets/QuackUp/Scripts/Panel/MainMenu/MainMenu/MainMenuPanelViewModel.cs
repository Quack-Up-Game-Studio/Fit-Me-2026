using System;
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
        public ReadOnlyReactiveProperty<int> RemainingAdCount => _outOfEnergyManager.RemainingAdCount;
        public ReadOnlyReactiveProperty<TimeSpan> TimeUntilNextWatchAd => _outOfEnergyManager.TimeUntilNextWatchAd;
        public int MaxAdCount => _outOfEnergyManager.MaxAdCount;
        public ReadOnlyReactiveProperty<int> CurrentEnergy => _energyManager.CurrentEnergy;
        
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
            _playerRecordSaveObject = _saveManager.GetFirstSaveObjectOfType<PlayerRecordSaveObject>();
            CompletedTutorial.Value = _playerRecordSaveObject.GetSaveData<PlayerRecordSaveData>().CompletedTutorial;
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