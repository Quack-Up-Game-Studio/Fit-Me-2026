using System;
using Cysharp.Threading.Tasks;
using FitMe.Shared;
using QuackUp.SceneManagement;
using QuackUp.Utils;
using R3;
using TMPro;
using UnityEngine.SceneManagement;
using VContainer;

namespace FitMe.Panel
{
    public class ResultPanelViewModel : PanelViewModel
    {
        public ReactiveCommand ToMainMenuCommand { get; } = new();
        public ReactiveCommand ToRetryCommand { get; } = new();
        
        public ReadOnlyReactiveProperty<string> ScoreText { get; private set; }
        public ReadOnlyReactiveProperty<string> FitText { get; private set; }

        private readonly LoadSceneManager _loadSceneManager;
        private readonly ILevelManager _levelManager;
        private IDisposable _bindings;
        
        [Inject]
        public ResultPanelViewModel(
            PanelManager panelManager,
            LoadSceneManager loadSceneManager,
            ILevelManager levelManager) : base(panelManager)
        {
            _loadSceneManager = loadSceneManager;
            _levelManager = levelManager;
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
        
        private async UniTask OnMainMenu()
        {
            await _loadSceneManager.LoadScene(SceneType.MainMenu, LoadSceneMode.Single, false);
        }
        
        private async UniTask OnRetry()
        {
            await _loadSceneManager.LoadScene(SceneType.Gameplay, LoadSceneMode.Single, false);
        }
    }
}
