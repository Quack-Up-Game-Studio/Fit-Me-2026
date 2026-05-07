using System;
using Cysharp.Threading.Tasks;
using FitMe.Shared;
using QuackUp.Audio;
using QuackUp.SceneManagement;
using QuackUp.Utils;
using R3;
using UnityEngine.SceneManagement;
using VContainer;

namespace FitMe.Panel
{
    public class PausePanelViewModel : PanelViewModel
    {
        public ReactiveCommand ToMainMenuCommand { get; } = new();
        public ReactiveCommand ResumeCommand { get; } = new();
        public ReactiveProperty<bool> BGMMuteState { get; } = new(true);
        public ReactiveProperty<bool> SfxMuteState { get; } = new(true);
        public ReactiveCommand ApplyChangesCommand { get; } = new();

        private readonly IAudioBusManager _audioBusManager;
        private readonly LoadSceneManager _loadSceneManager;
        private readonly IGameStateManager _gameStateManager;
        private IDisposable _bindings;
        
        [Inject]
        public PausePanelViewModel(
            PanelManager panelManager,
            LoadSceneManager loadSceneManager,
            IAudioBusManager audioBusManager,
            IGameStateManager gameStateManager) : base(panelManager)
        {
            _loadSceneManager = loadSceneManager;
            _gameStateManager = gameStateManager;
            _audioBusManager = audioBusManager;
            Bind();
        }

        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            ToMainMenuCommand
                .SubscribeAwait((_, _) => ToMainMenu(), AwaitOperation.Drop)
                .AddTo(ref disposableBuilder);
            ResumeCommand
                .Subscribe(_ => Resume())
                .AddTo(ref disposableBuilder);
            BGMMuteState
                .IgnoreFirstValueWhenSubscribe()
                .Subscribe(OnMusicToggled)
                .AddTo(ref disposableBuilder);
            SfxMuteState
                .IgnoreFirstValueWhenSubscribe()
                .Subscribe(OnSfxToggled)
                .AddTo(ref disposableBuilder);
            ApplyChangesCommand
                .Subscribe(_ => OnApplyChanges())
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }

        public override void Dispose()
        {
            base.Dispose();
            _bindings.Dispose();
        }
        
        protected override void OnVisible()
        {
            base.OnVisible();
            _audioBusManager.GetBusMuteState(BusType.BGM, out var bgmMuteState);
            _audioBusManager.GetBusMuteState(BusType.SFX, out var sfxMuteState);
            BGMMuteState.Value = bgmMuteState;
            SfxMuteState.Value = sfxMuteState;
        }
        
        protected override void OnHidden()
        {
            base.OnHidden();
            _audioBusManager.SaveChanges();
        }

        private async UniTask ToMainMenu()
        {
            await _loadSceneManager.LoadScene(SceneType.MainMenu, LoadSceneMode.Single, false);
        }
        
        private void Resume()
        {
            _gameStateManager.SetPause(false);
        }

        private void OnMusicToggled(bool isOn)
        {
            _audioBusManager.SetMuteBus(BusType.BGM, isOn);
        }
        
        private void OnSfxToggled(bool isOn)
        {
            _audioBusManager.SetMuteBus(BusType.SFX, isOn);
        }
        
        private void OnApplyChanges()
        {
            _audioBusManager.SaveChanges();
        }
    }
}