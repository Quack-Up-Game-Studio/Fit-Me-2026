using System;
using Cysharp.Threading.Tasks;
using FitMe.GameData;
using FitMe.Shared;
using FitMe.Tutorial;
using GameAnalyticsSDK;
using QuackUp.Save;
using QuackUp.SceneManagement;
using QuackUp.SocialService;
using QuackUp.Utils;
using R3;
using Sirenix.OdinInspector;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace FitMe.Scene
{
    [Serializable]
    public class TutorialManager : IStartable, IDisposable
    {
        private readonly TutorialStateMachine _stateMachine;
        private readonly LoadSceneManager _loadSceneManager;
        private readonly MessagePackSaveManager  _saveManager;
        private readonly ICloudSaveService _cloudSaveService;
        private readonly string _overrideStart;
        private IDisposable _subscriptions;
        
        public const string OverrideStartKey = "OverrideStart";
        
        [Inject]
        public TutorialManager(
            LevelManager levelManager,
            TutorialStateMachine stateMachine,
            LoadSceneManager loadSceneManager,
            MessagePackSaveManager saveManager,
            ICloudSaveService cloudSaveService,
            [Key(OverrideStartKey)] string startKey)
        {
            levelManager.IsTutorial = true;
            _stateMachine = stateMachine;
            _loadSceneManager = loadSceneManager;
            _saveManager = saveManager;
            _cloudSaveService = cloudSaveService;
            _overrideStart = startKey;
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
        
        [Button("Complete Tutorial")]
        private void OnTutorialCompleted()
        {
            var saveObject = _saveManager.GetFirstSaveObjectOfType<PlayerRecordSaveObject>();
            var saveData = saveObject.GetSaveData<PlayerRecordSaveData>();
            var sceneToLoad = saveData.CompletedTutorial ? SceneType.MainMenu : SceneType.Gameplay;
            saveData.CompletedTutorial = true;
            _saveManager.Save(saveObject);
            _cloudSaveService.SaveToService(SaveToServiceParameters.Default).Forget();
            GameAnalytics.NewProgressionEvent(GAProgressionStatus.Complete, GAProgression01.Tutorial);
            _loadSceneManager.LoadScene(sceneToLoad, LoadSceneMode.Single, false).Forget();
        }

        public void Start()
        {
            _stateMachine.StartTutorial(_overrideStart);
            GameAnalytics.NewProgressionEvent(GAProgressionStatus.Start, GAProgression01.Tutorial);
        }
    }
}