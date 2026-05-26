using System;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using PrimeTween;

namespace FitMe.Panel
{
    public class MainMenuPanelView : PanelView
    {
        [SerializeField] private Button challengeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button leaderboardButton;
        [SerializeField] private Button shopButton;
        [SerializeField] private Button tutorialButton;
        [SerializeField] private Button watchAdsButton;
        [SerializeField] private TMP_Text timeUntilNextAdText;
        [SerializeField] private RectTransform logoRect;
        [SerializeField] private TweenSettings<Vector3> logoBubbleSettings;

        [SerializeField] private string challengePanelId = "Challenge";
        [SerializeField] private string settingsPanelId = "Settings";
        [SerializeField] private string leaderboardPanelId = "Leaderboard";
        [SerializeField] private string shopPanelId = "Shop";
        [SerializeField] private GameObject loadingOverlay;
        
        private MainMenuPanelViewModel ViewModel => (MainMenuPanelViewModel)BaseViewModel;
        private IDisposable _bindings;
        
        private Vector3 _originalLogoScale = Vector3.one;
        private bool _hasStoredLogoScale;
        private Tween _logoTween;
        
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
            ViewModel.IsSaveLoading
                .Subscribe(loading =>
                {
                    if (loadingOverlay)
                    {
                        loadingOverlay.SetActive(loading);
                    }
                })
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
            watchAdsButton.OnClickAsObservable()
                .Subscribe(_ => ViewModel.WatchAdCommand.Execute(Unit.Default))
                .AddTo(ref disposableBuilder);
            ViewModel.RemainingAdCount
                .Subscribe(OnRemainingAdCountChanged)
                .AddTo(ref disposableBuilder);
            ViewModel.TimeUntilNextWatchAd
                .Subscribe(OnTimeUntilNextAdChanged)
                .AddTo(ref disposableBuilder);
            ViewModel.CurrentEnergy
                .Subscribe(_ => OnEnergyUpdated())
                .AddTo(ref disposableBuilder);
            ViewModel.InfiniteEnergy
                .Subscribe(_ => OnEnergyUpdated())
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }

        public override void Dispose()
        {
            base.Dispose();
            _bindings?.Dispose();
            _logoTween.Stop();
        }

        private void OnDisable()
        {
            _logoTween.Stop();
        }

        protected override void OnVisibilityStateChanged(VisibilityState state)
        {
            base.OnVisibilityStateChanged(state);
            
            if (!logoRect) return;

            if (state != VisibilityState.Visible)
            {
                _logoTween.Stop();
                if (_hasStoredLogoScale)
                {
                    logoRect.localScale = _originalLogoScale;
                }
                return;
            }

            if (!_hasStoredLogoScale)
            {
                _originalLogoScale = logoRect.localScale;
                _hasStoredLogoScale = true;
            }
            
            _logoTween.Stop();
            _logoTween = Tween.Scale(logoRect, logoBubbleSettings);
        }
        
        private void OnEnergyUpdated()
        {
            var shouldDisplay = !ViewModel.HasEnoughEnergy(1);
            watchAdsButton.gameObject.SetActive(shouldDisplay);
            timeUntilNextAdText.gameObject.SetActive(shouldDisplay);
        }
        
        private void OnRemainingAdCountChanged(int count)
        {
            if (ViewModel.RemainingAdCount.CurrentValue >= ViewModel.MaxAdCount)
            {
                timeUntilNextAdText.text = "Full";
            }
            watchAdsButton.interactable = count > 0;
            //remainingAdCount.text = $"Remaining: {count}";
        }

        private void OnTimeUntilNextAdChanged(TimeSpan time)
        {
            if (ViewModel.RemainingAdCount.CurrentValue >= ViewModel.MaxAdCount)
            {
                timeUntilNextAdText.text = "Full";
                return;
            }
            //round up to the nearest second for display purposes
            var roundedTime = TimeSpan.FromSeconds(Mathf.Ceil((float)time.TotalSeconds));
            timeUntilNextAdText.text = $"{roundedTime:mm\\:ss}";
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
