using System;
using Cysharp.Threading.Tasks;
using FitMe.GameData;
using FitMe.Shared;
using QuackUp.Audio;
using QuackUp.Save;
using QuackUp.SceneManagement;
using QuackUp.Utils;
using R3;
using TMPro;
using UnityEngine.SceneManagement;
using VContainer;

namespace FitMe.Panel
{
    public struct DisplayResultCommandData
    {
        public Promise<Unit> Promise;
        public bool IsNewHighScore;
        public bool IsNewFitMe;
        
        public DisplayResultCommandData(Promise<Unit> promise, bool isNewHighScore, bool isNewFitMe)
        {
            Promise = promise;
            IsNewHighScore = isNewHighScore;
            IsNewFitMe = isNewFitMe;
        }
    }
    public class ResultPanelViewModel : PanelViewModel
    {
        public ReactiveCommand ToMainMenuCommand { get; } = new();
        public ReactiveCommand ToRetryCommand { get; } = new();
        public ReactiveCommand<DisplayResultCommandData> DisplayResultCommand { get; } = new();
        
        public ReadOnlyReactiveProperty<string> ScoreText { get; private set; }
        public ReadOnlyReactiveProperty<string> FitText { get; private set; }
        
        public IAudioManager AudioManager { get; private set; }

        private readonly LoadSceneManager _loadSceneManager;
        private readonly MessagePackSaveManager _saveManager;
        private readonly ICloudSaveService _cloudSaveService;
        private readonly ILevelManager _levelManager;

        private Promise<Unit> _displayResultPromise;
        private PlayerRecordSaveObject _saveObject;
        private IDisposable _bindings;
        
        [Inject]
        public ResultPanelViewModel(
            PanelManager panelManager,
            LoadSceneManager loadSceneManager,
            MessagePackSaveManager saveManager,
            ICloudSaveService cloudSaveService,
            ILevelManager levelManager,
            IAudioManager audioManager) : base(panelManager)
        {
            _loadSceneManager = loadSceneManager;
            _levelManager = levelManager;
            _saveManager = saveManager;
            _cloudSaveService = cloudSaveService;
            AudioManager = audioManager;
            Bind();
        }
        
        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            
            ToMainMenuCommand
                .SubscribeAwait((_, _) => OnMainMenu(), AwaitOperation.Drop)
                .AddTo(ref disposableBuilder);
            
            ToRetryCommand
                .SubscribeAwait((_, _) => OnRetry(), AwaitOperation.Drop)
                .AddTo(ref disposableBuilder);
            
            ScoreText = _levelManager.Score
                .Select(score => score.ToString("N0")) 
                .ToReadOnlyReactiveProperty();
            FitText = _levelManager.FitMeScore
                .Select(fit => fit.ToString("N0")) 
                .ToReadOnlyReactiveProperty();
            
            _bindings = disposableBuilder.Build();
        }
        
        public override void Dispose()
        {
            base.Dispose();
            _bindings?.Dispose();
            ScoreText?.Dispose();
            FitText?.Dispose();
        }

        protected override void OnVisible()
        {
            base.OnVisible();
            _saveObject = _saveManager.GetFirstSaveObjectOfType<PlayerRecordSaveObject>();
            var saveData = _saveObject.GetSaveData<PlayerRecordSaveData>();
            var highScoreBefore = saveData.highScore.score;
            var mostFitMeBefore = saveData.mostFitMe.fitMe;
            saveData.AddRunData(new PlayerRecordSaveData.RunData
            {
                dateTime = DateTime.Now,
                score = _levelManager.Score.Value,
                fitMe = _levelManager.FitMeScore.Value
            });
            _saveManager.Save(_saveObject);
            _cloudSaveService.SaveToService();
            var isNewHighScore = _levelManager.Score.Value > highScoreBefore;
            var isNewFitMe = _levelManager.FitMeScore.Value > mostFitMeBefore;
            _displayResultPromise = new Promise<Unit>();
            DisplayResultCommand.Execute(new DisplayResultCommandData(_displayResultPromise, isNewHighScore, isNewFitMe));
        }

        private async UniTask OnMainMenu()
        {
            _displayResultPromise.Cancel();
            await _loadSceneManager.LoadScene(SceneType.MainMenu, LoadSceneMode.Single, false);
        }
        
        private async UniTask OnRetry()
        {
            _displayResultPromise.Cancel();
            await _loadSceneManager.LoadScene(SceneType.Gameplay, LoadSceneMode.Single, false);
        }
    }
}
