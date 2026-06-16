using System;
using QuackUp.Utils;
using R3;
using VContainer;

namespace FitMe.Panel
{
    public class SplashPanelViewModel : PanelViewModel
    {
        public ReactiveCommand<Unit> SplashFinishedCommand { get; } = new();
        
        private readonly SplashScreenMessageHub _messageHub;
        private IDisposable _bindings;

        [Inject]
        public SplashPanelViewModel(
            PanelManager panelManager,
            SplashScreenMessageHub messageHub) : base(panelManager)
        {
            _messageHub = messageHub;
            Bind();
        }

        private void Bind()
        {
            var builder = Disposable.CreateBuilder();
            
            SplashFinishedCommand
                .Subscribe(_ => OnSplashFinished())
                .AddTo(ref builder);
                
            _bindings = builder.Build();
        }

        private void OnSplashFinished()
        {
            DebugUtils.Log("SplashPanelViewModel: SplashFinishedCommand triggered, publishing SplashScreenFinishedEvent.");
            _messageHub.Publish(new SplashScreenFinishedEvent());
        }

        public override void Dispose()
        {
            base.Dispose();
            _bindings?.Dispose();
        }
    }
}