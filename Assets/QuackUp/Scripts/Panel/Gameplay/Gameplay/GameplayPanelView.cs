using System;
using R3;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace FitMe.Panel
{
    public class GameplayPanelView : PanelView
    {
        [SerializeField] private Button pauseButton;
        [SerializeField] private string pausePanelId = "Pause";

        private GameplayPanelViewModel ViewModel => (GameplayPanelViewModel)BaseViewModel;
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
            pauseButton.onClick.AsObservable()
                .Subscribe(_ => OnPauseButtonClicked())
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }

        public override void Dispose()
        {
            base.Dispose();
            _bindings?.Dispose();
        }

        private void OnPauseButtonClicked()
        {
            ViewModel.PauseCommand.Execute(Unit.Default);
            if (!TryGetCrossfadeRule(pausePanelId, out var rule)) return;
            ViewModel.CrossfadeCommand.Execute(new CrossfadeCommandData(pausePanelId, rule.crossfadeSettings));
        }
    }
}