using System;
using FitMe.Shared;
using R3;
using VContainer;

namespace FitMe.Panel
{
    public class ModeSelectViewModel : PanelViewModel
    {
        public ReactiveCommand<GameMode> SelectGameModeCommand { get; } = new();

        [Inject]
        public ModeSelectViewModel(
            PanelManager panelManager) : base(panelManager)
        {
            
        }
    }
}