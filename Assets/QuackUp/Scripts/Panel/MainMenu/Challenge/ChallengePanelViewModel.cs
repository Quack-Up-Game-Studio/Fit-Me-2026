using System;
using System.Collections.Generic;
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
        public IReadOnlyDictionary<string, AchievementInstance> Achievements { get; private set; }
        public PlayerRecordSaveData PlayerRecordData { get; private set; }
        public IUserDataProvider UserDataProvider { get; private set; }
        public IAuthenticationService AuthenticationService { get; private set; }
        
        private readonly ICloudSaveService _cloudSaveService;
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
            Achievements = achievementManager.Achievements;
            PlayerRecordData = saveManager.GetFirstSaveObjectOfType<PlayerRecordSaveObject>().GetSaveData<PlayerRecordSaveData>();
            _saveManager = saveManager;
            UserDataProvider = userDataProvider;
            AuthenticationService = authenticationService;
            _cloudSaveService = cloudSaveService;
            Bind();
        }

        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            SaveButtonClickedCommand
                .Subscribe(_ => OnSaveButtonClicked())
                .AddTo(ref disposableBuilder);
            LoadButtonClickCommand
                .Subscribe(_ => OnLoadButtonClicked())
                .AddTo(ref disposableBuilder);
            AuthenticateButtonClickCommand
                .Subscribe(_ => OnAuthenticateButtonClicked())
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }
        
        private void OnSaveButtonClicked()
        {
            var bytes = _saveManager.GetZipBytes();
            _cloudSaveService.SaveToService(bytes);
        }

        private void OnLoadButtonClicked()
        {
            _cloudSaveService.LoadFromService();
        }

        private void OnAuthenticateButtonClicked()
        {
            AuthenticationService.Authenticate();
        }

        public override void Dispose()
        {
            base.Dispose();
            _bindings.Dispose();
        }
    }
}