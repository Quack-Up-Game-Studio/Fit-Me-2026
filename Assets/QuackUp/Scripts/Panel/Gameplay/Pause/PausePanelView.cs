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
        [SerializeField] private Button musicToggleButton;
        [SerializeField] private Image musicSlashImage;
        [SerializeField] private Button sfxToggleButton;
        [SerializeField] private Image sfxSlashImage;
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
            _bindings = disposableBuilder.Build();
        }

        public override void Dispose()
        {
            base.Dispose();
            _bindings.Dispose();
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

        private void OnResume()
        {
            ViewModel.ApplyChangesCommand.Execute(Unit.Default);
            ViewModel.ResumeCommand.Execute(Unit.Default);
            if (!TryGetCrossfadeRule(gameplayPanelId, out var rule)) return;
            ViewModel.CrossfadeCommand.Execute(new(gameplayPanelId, rule.crossfadeSettings));
        }
        
        private void OnMainMenu()
        {
            ViewModel.ApplyChangesCommand.Execute(Unit.Default);
            ViewModel.ToMainMenuCommand.Execute(Unit.Default);
        }
    }
}