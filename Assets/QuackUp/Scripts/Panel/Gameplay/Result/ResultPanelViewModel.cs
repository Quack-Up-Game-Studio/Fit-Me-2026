using System;
using Cysharp.Threading.Tasks;
using FitMe.GameData;
using FitMe.Shared;
using MessagePipe;
using QuackUp.Audio;
using QuackUp.SceneManagement;
using QuackUp.Utils;
using R3;
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
        public ReadOnlyReactiveProperty<int> CurrentEnergy => _energyManager.CurrentEnergy;
        public ReadOnlyReactiveProperty<bool> InfiniteEnergy => _energyManager.InfiniteEnergy;
        public bool HasEnoughEnergy(uint amount) => _energyManager.HasEnoughEnergy(amount);
        
        public IAudioManager AudioManager { get; private set; }

        private readonly LoadSceneManager _loadSceneManager;
        private readonly EnergyManager _energyManager;
        private readonly AdsService _adsService;
        private readonly IScoreManager _scoreManager;
        private readonly int _forceAdsThreshold;

        private int _scoreBeforeSave;
        private int _fitMeBeforeSave;
        private Promise<Unit> _displayResultPromise;
        private PlayerRecordSaveObject _saveObject;
        private IDisposable _bindings;
        private IDisposable _adSubscription;
        
        public const string ForceAdsThresholdKey = "ForceAdsThresholdKey";
        
        [Inject]
        public ResultPanelViewModel(
            PanelManager panelManager,
            LoadSceneManager loadSceneManager,
            EnergyManager energyManager,
            AdsService adsService,
            IScoreManager scoreManager,
            IAudioManager audioManager,
            [Key(ForceAdsThresholdKey)] int forceAdsThreshold)
            : base(panelManager)
        {
            _loadSceneManager = loadSceneManager;
            _scoreManager = scoreManager;
            _energyManager = energyManager;
            _adsService = adsService;
            AudioManager = audioManager;
            _forceAdsThreshold = forceAdsThreshold;
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
            
            ScoreText = _scoreManager.Score
                .Select(score => score.ToString("N0")) 
                .ToReadOnlyReactiveProperty();
            FitText = _scoreManager.FitMe
                .Select(fit => fit.ToString("N0")) 
                .ToReadOnlyReactiveProperty();
            _bindings = disposableBuilder.Build();
        }
        
        public override void Dispose()
        {
            base.Dispose();
            _bindings?.Dispose();
            _adSubscription?.Dispose();
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
            var isNewHighScore = _scoreManager.Score.Value > _scoreBeforeSave;
            var isNewFitMe = _scoreManager.FitMe.Value > _fitMeBeforeSave;
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
            if (!_energyManager.HasEnoughEnergy(1))
            {
                await _energyManager.ShowNotEnoughEnergyNotification();
                return;
            }
            _energyManager.ChangeEnergy(-1, itemType: GAItemType.Play, itemId: GAItemId.GameplayRestart);
            _displayResultPromise.Cancel();
            if (_scoreManager.FitMe.CurrentValue >= _forceAdsThreshold && 
                _adsService.TryGetAdsInstance<InterstitialAdInstance>(out var interstitialAdInstance) && 
                interstitialAdInstance.Enabled)
            {
                _adSubscription = interstitialAdInstance.OnAdClosed
                    .Subscribe(_ => OnAdsClosed());
                interstitialAdInstance.AdContext = GAAdContext.SceneChange;
                interstitialAdInstance.TryShow();
            }
            else
            {
                await _loadSceneManager.LoadScene(SceneType.Gameplay, LoadSceneMode.Single, false);
            }
        }

        private void OnAdsClosed()
        {
            _adSubscription?.Dispose();
            _loadSceneManager.LoadScene(SceneType.Gameplay, LoadSceneMode.Single, false).Forget();
        }
    }
}
