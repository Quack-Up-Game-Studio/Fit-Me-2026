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
        [SerializeField] private Button challengeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button leaderboardButton;
        [SerializeField] private Button shopButton;
        [SerializeField] private Button tutorialButton;
        [SerializeField] private string challengePanelId = "Challenge";
        [SerializeField] private string settingsPanelId = "Settings";
        [SerializeField] private string leaderboardPanelId = "Leaderboard";
        [SerializeField] private string shopPanelId = "Shop";
        
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
            ViewModel.CompletedTutorial
                .Subscribe(OnTutorialCompletionChanged)
                .AddTo(ref disposableBuilder);
            challengeButton.OnClickAsObservable()
                .Subscribe(_ => OnChallengeButtonClicked())
                .AddTo(ref disposableBuilder);
            settingsButton.OnClickAsObservable()
                .Subscribe(_ => OnSettingsButtonClicked())
                .AddTo(ref disposableBuilder);
            leaderboardButton.OnClickAsObservable()
                .Subscribe(_ => OnLeaderboardButtonClicked())
                .AddTo(ref disposableBuilder);
            shopButton.OnClickAsObservable()
                .Subscribe(_ => OnShopButtonClicked())
                .AddTo(ref disposableBuilder);
            tutorialButton.OnClickAsObservable()
                .Subscribe(_ => OnTutorialButtonClicked())
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }

        public override void Dispose()
        {
            base.Dispose();
            _bindings?.Dispose();
        }
        
        private void OnTutorialCompletionChanged(bool completed)
        {
            tutorialButton.gameObject.SetActive(completed);
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

        private void OnLeaderboardButtonClicked()
        {
            if (!TryGetCrossfadeRule(leaderboardPanelId, out var rule)) return;
            ViewModel.CrossfadeCommand.Execute(new CrossfadeCommandData(leaderboardPanelId, rule.crossfadeSettings));
        }

        private void OnShopButtonClicked()
        {
            if (!TryGetCrossfadeRule(shopPanelId, out var rule)) return;
            ViewModel.CrossfadeCommand.Execute(new CrossfadeCommandData(shopPanelId, rule.crossfadeSettings));
        }

        private void OnTutorialButtonClicked()
        {
            ViewModel.ToTutorial.Execute(Unit.Default);
        }
    }
}
