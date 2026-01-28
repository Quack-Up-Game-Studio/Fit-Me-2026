using UnityEngine;
using VContainer;

namespace FitMe.Panel
{
    public class GameplayPanelLifetimeScope : PanelLifetimeScope
    {
        [SerializeField] private GameplayPanelView panelView;
        public override IPanelViewModel CreatPanel()
        {
            var viewModel = new GameplayPanelViewModel(ParentPanelManager);
            panelView.Construct(viewModel);
            return viewModel;
        }
    }
}