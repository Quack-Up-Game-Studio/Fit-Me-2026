using System;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace FitMe.Panel
{
    public class SettingsPanelView : PanelView
    {
        [SerializeField] private TMP_Text gameVersionText;
        [SerializeField] private Button musicToggleButton;
        [SerializeField] private Image musicSlashImage;
        [SerializeField] private Button sfxToggleButton;
        [SerializeField] private Image sfxSlashImage;
        [SerializeField] private Button closeButton;
        [SerializeField] private string mainMenuPanelId = "MainMenu";
        
        private SettingsPanelViewModel ViewModel => (SettingsPanelViewModel)BaseViewModel;
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
            _bindings = disposableBuilder.Build();
        }

        protected override void OnVisibilityStateChanged(VisibilityState state)
        {
            base.OnVisibilityStateChanged(state);
            if (state == VisibilityState.Hidden) return;
            gameVersionText.text = Application.version;
        }

        public override void Dispose()
        {
            base.Dispose();
            _bindings?.Dispose();
        }
        
        private void OnMusicToggleStateChanged(bool isMuted)
        {
            musicSlashImage.enabled = isMuted;
        }
        
        private void OnSfxToggleStateChanged(bool isMuted)
        {
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
            if (!TryGetCrossfadeRule(mainMenuPanelId, out var rule)) return;
            ViewModel.CrossfadeCommand.Execute(new CrossfadeCommandData(mainMenuPanelId, rule.crossfadeSettings));
        }
    }
}