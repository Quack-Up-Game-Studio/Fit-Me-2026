using System;
using QuackUp.Audio;
using QuackUp.Save;
using QuackUp.Utils;
using R3;
using VContainer;

namespace FitMe.Panel
{
    public class SettingsPanelViewModel : PanelViewModel
    {
        public ReactiveProperty<bool> BGMMuteState { get; private set; } = new(true);
        public ReactiveProperty<bool> SfxMuteState { get; private set; } = new(true);

        private readonly IAudioBusManager _audioBusManager;
        
        private IDisposable _bindings;
        
        [Inject]
        public SettingsPanelViewModel(
            IAudioBusManager audioBusManager,
            PanelManager panelManager) : base(panelManager)
        {
            _audioBusManager = audioBusManager;
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
        
        protected override void OnHidden()
        {
            base.OnHidden();
            _audioBusManager.SaveChanges();
        }

        private void OnMusicToggled(bool isOn)
        {
            _audioBusManager.SetMuteBus(BusType.BGM, isOn);
        }
        
        private void OnSfxToggled(bool isOn)
        {
            _audioBusManager.SetMuteBus(BusType.SFX, isOn);
        }
    }
}