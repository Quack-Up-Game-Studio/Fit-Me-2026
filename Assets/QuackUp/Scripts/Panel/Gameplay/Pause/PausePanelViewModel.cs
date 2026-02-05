using System;
using Cysharp.Threading.Tasks;
using FitMe.Shared;
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

        private readonly LoadSceneManager _loadSceneManager;
        private readonly ILevelManager _levelManager;
        private IDisposable _bindings;
        
        [Inject]
        public PausePanelViewModel(
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
                .SubscribeAwait((_, _) => ToMainMenu(), AwaitOperation.Drop)
                .AddTo(ref disposableBuilder);
            ResumeCommand
                .Subscribe(_ => Resume())
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }

        public override void Dispose()
        {
            base.Dispose();
            _bindings.Dispose();
        }

        private async UniTask ToMainMenu()
        {
            await _loadSceneManager.LoadScene(SceneType.MainMenu, LoadSceneMode.Single, false);
        }
        
        private void Resume()
        {
            _levelManager.Unpause();
        }
    }
}