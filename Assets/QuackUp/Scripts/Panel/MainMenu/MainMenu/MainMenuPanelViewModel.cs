using R3;
using UnityEngine;
using VContainer;

namespace FitMe.Panel
{
    public class MainMenuPanelViewModel : PanelViewModel
    {
        public ReactiveProperty<string> GameVersion { get; private set; } = new(Application.version);
        
        [Inject]
        public MainMenuPanelViewModel(PanelManager panelManager) : base(panelManager)
        {
        }
    }
}