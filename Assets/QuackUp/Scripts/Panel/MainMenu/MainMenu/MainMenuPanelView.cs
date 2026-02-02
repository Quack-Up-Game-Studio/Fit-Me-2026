using System;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace FitMe.Panel
{
    public class MainMenuPanelView : PanelView
    {
        [SerializeField] private TMP_Text gameVersionText;
        [SerializeField] private Button challengeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private string challengePanelId = "Challenge";
        [SerializeField] private string settingsPanelId = "Settings";
        
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
            challengeButton.OnClickAsObservable()
                .Subscribe(_ => OnChallengeButtonClicked())
                .AddTo(ref disposableBuilder);
            settingsButton.OnClickAsObservable()
                .Subscribe(_ => OnSettingsButtonClicked())
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

        private void OnChallengeButtonClicked()
        {
            if (!TryGetCrossfadeRule(challengePanelId, out var rule)) return;
            ViewModel.CrossfadeCommand.Execute(new CrossfadeCommandData(challengePanelId, rule.crossfadeSettings));
        }

        private void OnSettingsButtonClicked()
        {
            if (!TryGetCrossfadeRule(settingsPanelId, out var rule)) return;
            ViewModel.CrossfadeCommand.Execute(new CrossfadeCommandData(settingsPanelId, rule.crossfadeSettings));
        }
    }
}
