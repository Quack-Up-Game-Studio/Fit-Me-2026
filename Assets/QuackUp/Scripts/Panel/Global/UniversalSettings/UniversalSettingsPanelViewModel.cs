using System;
using Cysharp.Threading.Tasks;
using FitMe.Shared;
using QuackUp.Audio;
#if FITME_PERFORMANCE_DIAGNOSTICS
using QuackUp.Core;
#endif
using QuackUp.SceneManagement;
using QuackUp.Utils;
using R3;
using UnityEngine.SceneManagement;
using VContainer;

namespace FitMe.Panel
{
    public class UniversalSettingsPanelViewModel : PanelViewModel
    {
        public ReactiveProperty<bool> BGMMuteState { get; } = new(true);
        public ReactiveProperty<bool> SfxMuteState { get; } = new(true);
        public ReactiveCommand ApplyChangesCommand { get; } = new();
        public ReactiveCommand ToMainMenuCommand { get; } = new();
#if FITME_PERFORMANCE_DIAGNOSTICS
        public ReactiveCommand SharePerformanceLogCommand { get; } = new();
#endif

        private readonly IAudioBusManager _audioBusManager;
        private readonly LoadSceneManager _loadSceneManager;
        private readonly IGameStateManager _gameStateManager;
        private readonly ILevelManager _levelManager;
#if FITME_PERFORMANCE_DIAGNOSTICS
        private readonly PerformanceDiagnosticsRecorder _performanceDiagnosticsRecorder;
#endif
        private IDisposable _bindings;
        
        [Inject]
        public UniversalSettingsPanelViewModel(
            IAudioBusManager audioBusManager,
            LoadSceneManager loadSceneManager,
            IGameStateManager gameStateManager,
            ILevelManager levelManager,
            PanelManager panelManager
#if FITME_PERFORMANCE_DIAGNOSTICS
            , PerformanceDiagnosticsRecorder performanceDiagnosticsRecorder
#endif
        ) : base(panelManager)
        {
            _audioBusManager = audioBusManager;
            _loadSceneManager = loadSceneManager;
            _gameStateManager = gameStateManager;
            _levelManager = levelManager;
#if FITME_PERFORMANCE_DIAGNOSTICS
            _performanceDiagnosticsRecorder = performanceDiagnosticsRecorder;
#endif
            Bind();
        }
        
        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
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
            ToMainMenuCommand
                .SubscribeAwait((_, _) => ToMainMenu(), AwaitOperation.Drop)
                .AddTo(ref disposableBuilder);
#if FITME_PERFORMANCE_DIAGNOSTICS
            SharePerformanceLogCommand
                .Subscribe(_ => _performanceDiagnosticsRecorder.ShareLatestLog())
                .AddTo(ref disposableBuilder);
#endif
            _bindings = disposableBuilder.Build();
        }

        public override void Dispose()
        {
            base.Dispose();
            _bindings?.Dispose();
        }

        protected override void OnVisible()
        {
            base.OnVisible();
            _audioBusManager.GetBusMuteState(BusType.BGM, out var bgmMuteState);
            _audioBusManager.GetBusMuteState(BusType.SFX, out var sfxMuteState);
            BGMMuteState.Value = bgmMuteState;
            SfxMuteState.Value = sfxMuteState;
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
            _gameStateManager.SetPause(false);
        }

        public static SceneType ResolveQuitDestination(GameMode gameMode) => gameMode switch
        {
            GameMode.LevelShape => SceneType.ShapeLevelSelect,
            GameMode.Classic => SceneType.ModeSelect,
            _ => SceneType.ModeSelect
        };

        private async UniTask ToMainMenu()
        {
            await _loadSceneManager.LoadScene(ResolveQuitDestination(_levelManager.CurrentGameMode), LoadSceneMode.Single, false);
        }
    }
}
