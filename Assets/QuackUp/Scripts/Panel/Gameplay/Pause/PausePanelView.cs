using System;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace FitMe.Panel
{
    public class PausePanelView : PanelView
    {
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private string gameplayPanelId = "Gameplay";

        private PausePanelViewModel ViewModel => (PausePanelViewModel)BaseViewModel;

        private IDisposable _bindings;

        public override void Construct(IPanelViewModel viewModel)
        {
            base.Construct(viewModel);
            Bind();
        }

        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            resumeButton.OnClickAsObservable()
                .Subscribe(_ => OnResume())
                .AddTo(ref disposableBuilder);
            mainMenuButton.OnClickAsObservable()
                .Subscribe(_ => OnMainMenu())
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }

        public override void Dispose()
        {
            base.Dispose();
            _bindings.Dispose();
        }

        private void OnResume()
        {
            ViewModel.ResumeCommand.Execute(Unit.Default);
            if (!TryGetCrossfadeRule(gameplayPanelId, out var rule)) return;
            ViewModel.CrossfadeCommand.Execute(new(gameplayPanelId, rule.crossfadeSettings));
        }
        
        private void OnMainMenu()
        {
            ViewModel.ToMainMenuCommand.Execute(Unit.Default);
        }
    }
}