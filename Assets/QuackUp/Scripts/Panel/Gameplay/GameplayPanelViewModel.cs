using VContainer;

namespace FitMe.Panel
{
    public class GameplayPanelViewModel : PanelViewModelBase
    {
        [Inject]
        public GameplayPanelViewModel(PanelManager panelManager) : base(panelManager)
        {
        }
    }
}