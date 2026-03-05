using System;
using FitMe.Shared;
using QuackUp.SocialService;
using R3;
using VContainer;

namespace FitMe.Panel
{
    public class LeaderboardPanelViewModel : PanelViewModel
    {
        public ILeaderboardService LeaderboardService { get; }
        
        private IDisposable _bindings;
        
        [Inject]
        public LeaderboardPanelViewModel(
            PanelManager panelManager,
            ILeaderboardService leaderboardService) : base(panelManager)
        {
            LeaderboardService = leaderboardService;
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