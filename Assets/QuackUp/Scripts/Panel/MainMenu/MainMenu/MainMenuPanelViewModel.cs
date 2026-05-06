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
        public ReactiveCommand ToTutorial { get; private set; } = new();
        public ReactiveProperty<bool> CompletedTutorial { get; private set; } = new(false);
        
        private PlayerRecordSaveObject _playerRecordSaveObject;
        private MessagePackSaveManager _saveManager;
        private IDisposable _bindings;
        
        [Inject]
        public MainMenuPanelViewModel(
            PanelManager panelManager,
            MessagePackSaveManager saveManager) : base(panelManager)
        {
            _saveManager  = saveManager;
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
            _bindings = disposableBuilder.Build();
        }

        private void ShopUpdate()
        {
            
        }
        
        public override void Dispose()
        {
            base.Dispose();
            _bindings?.Dispose();
        }
    }
}