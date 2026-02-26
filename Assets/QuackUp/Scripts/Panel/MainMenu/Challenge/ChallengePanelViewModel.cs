using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using FitMe.Achievement;
using FitMe.GameData;
using FitMe.Shared;
using QuackUp.Save;
using R3;
using VContainer;

namespace FitMe.Panel
{
    public class ChallengePanelViewModel : PanelViewModel
    {
        public ReactiveCommand SaveButtonClickedCommand { get; } = new();
        public ReactiveCommand LoadButtonClickCommand { get; } = new();
        public ReactiveCommand AuthenticateButtonClickCommand { get; } = new();
        public IReadOnlyDictionary<string, AchievementInstance> Achievements => _achievementManager.Achievements;
        public PlayerRecordSaveData PlayerRecordData => _saveManager.GetFirstSaveObjectOfType<PlayerRecordSaveObject>().GetSaveData<PlayerRecordSaveData>();
        public IUserDataProvider UserDataProvider { get; private set; }
        public IAuthenticationService AuthenticationService { get; private set; }
        public ICloudSaveService CloudSaveService { get; private set; }
        
        private readonly AchievementManager _achievementManager;
        private readonly MessagePackSaveManager _saveManager;
        private IDisposable _bindings;
        
        [Inject]
        public ChallengePanelViewModel(
            AchievementManager achievementManager,
            MessagePackSaveManager saveManager,
            PanelManager panelManager,
            IUserDataProvider userDataProvider,
            IAuthenticationService authenticationService,
            ICloudSaveService cloudSaveService) : base(panelManager)
        {
            _achievementManager = achievementManager;
            _saveManager = saveManager;
            UserDataProvider = userDataProvider;
            AuthenticationService = authenticationService;
            CloudSaveService = cloudSaveService;
            Bind();
        }

        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            SaveButtonClickedCommand
                .SubscribeAwait((_,_) => OnSaveButtonClicked(), AwaitOperation.Drop)
                .AddTo(ref disposableBuilder);
            LoadButtonClickCommand
                .SubscribeAwait((_, _) => OnLoadButtonClicked(), AwaitOperation.Drop)
                .AddTo(ref disposableBuilder);
            AuthenticateButtonClickCommand
                .SubscribeAwait((_, _) => OnAuthenticateButtonClicked(), AwaitOperation.Drop)
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }
        
        private async UniTask OnSaveButtonClicked()
        {
            await CloudSaveService.SaveToService();
        }

        private async UniTask OnLoadButtonClicked()
        {
            await CloudSaveService.LoadFromService();
        }

        private async UniTask OnAuthenticateButtonClicked()
        {
            await AuthenticationService.Authenticate();
        }

        public override void Dispose()
        {
            base.Dispose();
            _bindings.Dispose();
        }
    }
}