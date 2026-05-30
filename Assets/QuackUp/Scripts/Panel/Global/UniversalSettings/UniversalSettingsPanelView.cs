using System;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace FitMe.Panel
{
    public class UniversalSettingsPanelView : PanelView
    {
        [SerializeField] private TMP_Text gameVersionText;
        [SerializeField] private Button musicToggleButton;
        [SerializeField] private Image musicSlashImage;
        [SerializeField] private Button sfxToggleButton;
        [SerializeField] private Image sfxSlashImage;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button facebookButton;
        [SerializeField] private string facebookCommunityUrl;
        [SerializeField] private string backPanelId = "MainMenu";
        [SerializeField] private Button mainMenuButton;
        
        private UniversalSettingsPanelViewModel ViewModel => (UniversalSettingsPanelViewModel)BaseViewModel;
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
            ViewModel.BGMMuteState
                .Subscribe(OnMusicToggleStateChanged)
                .AddTo(ref disposableBuilder);
            ViewModel.SfxMuteState
                .Subscribe(OnSfxToggleStateChanged)
                .AddTo(ref disposableBuilder);
            musicToggleButton.OnClickAsObservable()
                .Subscribe(_ => OnMusicToggleButtonClicked())
                .AddTo(ref disposableBuilder);
            sfxToggleButton.OnClickAsObservable()
                .Subscribe(_ => OnSfxToggleButtonClicked())
                .AddTo(ref disposableBuilder);
            closeButton.OnClickAsObservable()
                .Subscribe(_ => OnCloseButtonClicked())
                .AddTo(ref disposableBuilder);
            
            facebookButton.OnClickAsObservable()
                .Subscribe(_ => OnFacebookButtonClicked())
                .AddTo(ref disposableBuilder);

            if (mainMenuButton)
            {
                mainMenuButton.OnClickAsObservable()
                    .Subscribe(_ => OnMainMenuButtonClicked())
                    .AddTo(ref disposableBuilder);
            }
            
            _bindings = disposableBuilder.Build();
        }

        protected override void OnVisibilityStateChanged(VisibilityState state)
        {
            base.OnVisibilityStateChanged(state);
            if (state == VisibilityState.Hidden) return;
            if (!gameVersionText) return;
            gameVersionText.text = $"v{Application.version}";
        }

        public override void Dispose()
        {
            base.Dispose();
            _bindings?.Dispose();
        }
        
        private void OnMusicToggleStateChanged(bool isMuted)
        {
            if (!musicSlashImage) return;
            musicSlashImage.enabled = isMuted;
        }
        
        private void OnSfxToggleStateChanged(bool isMuted)
        {
            if (!sfxSlashImage) return;
            sfxSlashImage.enabled = isMuted;
        }
        
        private void OnMusicToggleButtonClicked()
        {
            ViewModel.BGMMuteState.Value = !ViewModel.BGMMuteState.Value;
        }
        
        private void OnSfxToggleButtonClicked()
        {
            ViewModel.SfxMuteState.Value = !ViewModel.SfxMuteState.Value;
        }

        private void OnCloseButtonClicked()
        {
            ViewModel.ApplyChangesCommand.Execute(Unit.Default);
            if (!TryGetCrossfadeRule(backPanelId, out var rule)) return;
            ViewModel.CrossfadeCommand.Execute(new CrossfadeCommandData(backPanelId, rule.crossfadeSettings));
        }

        private void OnFacebookButtonClicked()
        {
            if (string.IsNullOrEmpty(facebookCommunityUrl)) return;
            Application.OpenURL(facebookCommunityUrl);
        }

        private void OnMainMenuButtonClicked()
        {
            ViewModel.ApplyChangesCommand.Execute(Unit.Default);
            ViewModel.ToMainMenuCommand.Execute(Unit.Default);
        }
    }
}
