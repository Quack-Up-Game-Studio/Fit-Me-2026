using System;
using Cysharp.Threading.Tasks;
using FitMe.GameData;
using FitMe.Shared;
using QuackUp.Audio;
using QuackUp.Save;
using QuackUp.SceneManagement;
using QuackUp.SocialService;
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
        public Observable<Unit> OnResultVisible => _onResultVisible;
        private readonly Subject<Unit> _onResultVisible = new();
        
        public ReadOnlyReactiveProperty<string> ScoreText { get; private set; }
        public ReadOnlyReactiveProperty<string> FitText { get; private set; }
        
        public IAudioManager AudioManager { get; private set; }

        private readonly LoadSceneManager _loadSceneManager;
        
        private readonly ILevelManager _levelManager;

        private int _scoreBeforeSave;
        private int _fitMeBeforeSave;
        private Promise<Unit> _displayResultPromise;
        private PlayerRecordSaveObject _saveObject;
        private IDisposable _bindings;
        
        [Inject]
        public ResultPanelViewModel(
            PanelManager panelManager,
            LoadSceneManager loadSceneManager,
            ILevelManager levelManager,
            IAudioManager audioManager) : base(panelManager)
        {
            _loadSceneManager = loadSceneManager;
            _levelManager = levelManager;
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
            FitText = _levelManager.FitMe
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

        public void CacheScoreAndFit(int score, int fitMe)
        {
            _scoreBeforeSave = score;
            _fitMeBeforeSave = fitMe;
        }

        protected override void OnVisible()
        {
            base.OnVisible();
            _onResultVisible.OnNext(Unit.Default);
            var isNewHighScore = _levelManager.Score.Value > _scoreBeforeSave;
            var isNewFitMe = _levelManager.FitMe.Value > _fitMeBeforeSave;
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
