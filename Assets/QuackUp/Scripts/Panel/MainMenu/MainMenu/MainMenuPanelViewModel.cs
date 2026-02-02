using System;
using R3;
using UnityEngine;
using VContainer;

namespace FitMe.Panel
{
    public class MainMenuPanelViewModel : PanelViewModel
    {
        public ReactiveProperty<string> GameVersion { get; private set; } = new(Application.version);
        
        private IDisposable _bindings;
        
        [Inject]
        public MainMenuPanelViewModel(PanelManager panelManager) : base(panelManager)
        {
            Bind();
        }
        
        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            _bindings = disposableBuilder.Build();
        }
        
        public override void Dispose()
        {
            base.Dispose();
            _bindings?.Dispose();
        }
    }
}