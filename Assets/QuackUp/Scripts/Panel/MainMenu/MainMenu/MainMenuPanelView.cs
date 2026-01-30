using System;
using R3;
using TMPro;
using UnityEngine;
using VContainer;

namespace FitMe.Panel
{
    public class MainMenuPanelView : PanelView
    {
        [SerializeField] private TMP_Text gameVersionText;
        
        private MainMenuPanelViewModel ViewModel => (MainMenuPanelViewModel)BaseViewModel;
        private IDisposable _bindings;
        
        [Inject]
        public override void Construct(IPanelViewModel viewModel)
        {
            base.Construct(viewModel);
            Bind();
        }

        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            ViewModel.GameVersion
                .Subscribe(OnGameVersionChanged)
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }

        public override void Dispose()
        {
            base.Dispose();
            _bindings?.Dispose();
        }
        
        private void OnGameVersionChanged(string version)
        {
            gameVersionText.text = version;
        }
    }
}