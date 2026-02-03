using System;
using FitMe.Shared;
using QuackUp.Utils;
using R3;
using VContainer;

namespace FitMe.Panel
{
    public class GameOverPanelViewModel : PanelViewModel
    {
        public ReactiveCommand adsContinueCommand { get; } = new();
        public ReactiveCommand skipCommand { get; } = new();
        
        private IDisposable _bindings;
        
        [Inject]
        public GameOverPanelViewModel(
            PanelManager panelManager) : base(panelManager)
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
        
        private void OnAdsContinue()
        {
        }
        
        private void OnSkip()
        {
        }
    }
}
