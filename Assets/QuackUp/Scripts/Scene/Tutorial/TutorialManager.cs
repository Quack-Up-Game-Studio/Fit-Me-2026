using System;
using Cysharp.Threading.Tasks;
using FitMe.Tutorial;
using QuackUp.SceneManagement;
using R3;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace FitMe.Scene
{
    public class TutorialManager : IStartable, IDisposable
    {
        private readonly TutorialStateMachine _stateMachine;
        private readonly LoadSceneManager _loadSceneManager;
        private IDisposable _subscriptions;
        
        [Inject]
        public TutorialManager(
            LevelManager levelManager,
            TutorialStateMachine stateMachine,
            LoadSceneManager loadSceneManager)
        {
            levelManager.IsTutorial = true;
            _stateMachine = stateMachine;
            _loadSceneManager = loadSceneManager;
            Subscribe();
        }
        
        private void Subscribe()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            _stateMachine.OnTutorialCompleted
                .Subscribe(_ => OnTutorialCompleted())
                .AddTo(ref disposableBuilder);
            _subscriptions = disposableBuilder.Build();
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }
        
        private void OnTutorialCompleted()
        {
            _loadSceneManager.LoadScene(SceneType.Gameplay, LoadSceneMode.Single, false).Forget();
        }

        public void Start()
        {
            _stateMachine.StartTutorial();
        }
    }
}