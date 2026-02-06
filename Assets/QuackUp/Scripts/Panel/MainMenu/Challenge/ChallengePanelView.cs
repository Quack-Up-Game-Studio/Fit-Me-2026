using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using R3;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace FitMe.Panel
{
    public class ChallengePanelView : PanelView
    {
        [Title("References")]
        [SerializeField] private Image profileImage;
        [SerializeField] private TMP_Text usernameText;
        [SerializeField] private TMP_Text highscoreText;
        [SerializeField] private TMP_Text mostFitText;
        [SerializeField] private LayoutGroup scrollContent;
        [SerializeField] private LayoutGroup recordParent;
        [SerializeField] private RecordBlock recordBlockPrefab;
        [SerializeField] private LayoutGroup challengeParent;
        [SerializeField] private ChallengeBlock challengeBlockPrefab;
        [SerializeField] private Button authenticateButton;
        [SerializeField] private Button backButton;
        [SerializeField] private Button loadButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private string mainMenuPanelKey = "MainMenu";
        
        [Button("Force Rebuild Layout")]
        public void ForceRebuildLayout()
        {
            OnPlayerDataLoaded().Forget();
            //LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContent);
        }

        private readonly List<ChallengeBlock> _challengeBlocks = new();
        private readonly List<RecordBlock> _recordBlocks = new();
        
        private ChallengePanelViewModel ViewModel => (ChallengePanelViewModel)BaseViewModel; 
// #if UNITY_ANDROID
//         private IRequestHandler<GPGSServiceRequest, GPGSServiceResponse<IGPGSService>> _gpgsServiceRequestHandler;
// #endif
        private IDisposable _bindings;
        private bool _isDataLoaded;

        [Inject]
        public override void Construct(IPanelViewModel viewModel)
        {
            base.Construct(viewModel);
           Bind();
// #if UNITY_ANDROID
//             GPGSManager.OnFinishedAuthentication += OnFinishedAuthentication;
//             _gpgsServiceRequestHandler = GlobalMessagePipe.GetRequestHandler<GPGSServiceRequest, GPGSServiceResponse<IGPGSService>>();
// #else
//             authenticateButton.gameObject.SetActive(false);
// #endif
        }

// #if UNITY_ANDROID
//         private void OnFinishedAuthentication(SignInStatus status)
//         {
//
//             SetPlayerInfo();
//             AuthenticationDone(status);
//
//         }
// #endif

        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            backButton.OnClickAsObservable()
                .Subscribe(_ => OnBackButtonClicked())
                .AddTo(ref disposableBuilder);
            authenticateButton.OnClickAsObservable()
                .Subscribe(_ => OnAuthenticateButtonClicked())
                .AddTo(ref disposableBuilder);
            loadButton.OnClickAsObservable()
                .Subscribe(_ => OnLoadButtonClicked())
                .AddTo(ref disposableBuilder);
            saveButton.OnClickAsObservable()
                .Subscribe(_ => OnSaveButtonClicked())
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }

        public override void Dispose()
        {
            base.Dispose();
            _bindings?.Dispose();
        }

        protected override void OnVisibilityStateChanged(VisibilityState state)
        {
            base.OnVisibilityStateChanged(state);
            if (state != VisibilityState.Visible) return;
            if (!_isDataLoaded) OnPlayerDataLoaded().Forget();
            SetPlayerInfo();
            SetRecords();
            SetChallenges();
            ForceRebuild().Forget();
        }

        private async UniTaskVoid OnPlayerDataLoaded()
        {
            _recordBlocks.ForEach(recordBlock =>
            {
                if (recordBlock)
                    Destroy(recordBlock.gameObject);
            });
            _recordBlocks.Clear();
            var records = ViewModel.PlayerRecordData.RunDataList;
            foreach (var record in records)
            {
                var recordBlock = Instantiate(recordBlockPrefab, recordParent.transform);
                _recordBlocks.Add(recordBlock);
                recordBlock.SetData(record);
            }
            _challengeBlocks.ForEach(challengeBlock =>
            {
                if (challengeBlock)
                    Destroy(challengeBlock.gameObject);
            });
            _challengeBlocks.Clear();
            var achievements = ViewModel.Achievements.Values.ToList();
            foreach (var instance in achievements)
            {
                var challengeBlock = Instantiate(challengeBlockPrefab, challengeParent.transform);
                _challengeBlocks.Add(challengeBlock);
                challengeBlock.SetData(instance.achievement);
            }
            _isDataLoaded = true;
            await UniTask.WaitForEndOfFrame();
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContent.transform as RectTransform);
            await ForceRebuild();
        }
        
        private void OnDestroy()
        {
// #if UNITY_ANDROID
//             GPGSManager.OnFinishedAuthentication -= OnFinishedAuthentication;
//             GPGSManager.OnFinishedAuthentication -= AuthenticationDone;
//             JsonSaveManager.OnLoadCompleted -= OnLoadAfterAuthentication;
// #endif
        }

        private async UniTask ForceRebuild()
        {
            await UniTask.WaitForEndOfFrame();
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContent.transform as RectTransform);
        }
        
        private void OnBackButtonClicked()
        {
            if (!TryGetCrossfadeRule(mainMenuPanelKey, out var rule)) return;
            ViewModel.CrossfadeCommand.Execute(new CrossfadeCommandData(mainMenuPanelKey, rule.crossfadeSettings));
        }
        
        private void OnAuthenticateButtonClicked()
        {
// #if UNITY_ANDROID
//             GPGSManager.Instance.ManualAuthenticate();
//             authenticateButton.interactable = false;
//             GPGSManager.OnFinishedAuthentication += AuthenticationDone;
// #endif
        }
        
// #if UNITY_ANDROID
//         void AuthenticationDone(SignInStatus status)
//         {
//             Debug.Log("Authentication Done with status: " + status);
//             authenticateButton.interactable = true;
//             GPGSManager.OnFinishedAuthentication -= AuthenticationDone;
//             if (status != SignInStatus.Success) return;
//             JsonSaveManager.OnLoadCompleted += OnLoadAfterAuthentication;
//         }
//
//         void OnLoadAfterAuthentication()
//         {
//             Debug.Log("Load after authentication completed.");
//             JsonSaveManager.OnLoadCompleted -= OnLoadAfterAuthentication;
//             LoadAfterAuthentication().Forget();
//         }
//
//         private async UniTaskVoid LoadAfterAuthentication()
//         {
//             await JsonSaveManager.Instance.Save(true);
//             await JsonSaveManager.Instance.Load();
//         }
// #endif
        
        private void OnLoadButtonClicked()
        {
// #if UNITY_ANDROID
//             var service = (GPGSSavedGame)_gpgsServiceRequestHandler.Invoke(GPGSServiceRequest.Create<GPGSSavedGame>()).service;
//             service?.ManualLoadFromService();
// #endif
        }

        private void OnSaveButtonClicked()
        {
// #if UNITY_ANDROID
//             var service = (GPGSSavedGame)_gpgsServiceRequestHandler.Invoke(GPGSServiceRequest.Create<GPGSSavedGame>()).service;
//             service?.ManualSaveToService().Forget();
// #endif
        }

        private void SetPlayerInfo()
        {
            usernameText.text = "Guest";
// #if UNITY_ANDROID
//             authenticateButton.gameObject.SetActive(!PlayGamesPlatform.Instance.IsAuthenticated());
//             if (!PlayGamesPlatform.Instance.IsAuthenticated()) return;
//             var localUser = PlayGamesPlatform.Instance.localUser;
//             usernameText.text = localUser.userName;
//             if (!localUser.image) return;
//             var profilePicture = localUser.image;
//             var sprite = Sprite.Create(profilePicture, new Rect(0, 0, profilePicture.width, profilePicture.height), 
//                 new Vector2(0.5f, 0.5f));
//             profileImage.sprite = sprite;
// #endif
        }

        private void SetChallenges()
        {
            var achievements = ViewModel.Achievements.Values.ToList();
            for (var i = 0; i < _challengeBlocks.Count; i++)
            {
                if (i >= achievements.Count)
                {
                    Debug.LogWarning($"Not enough challenges to fill the challenge blocks. " +
                                     $"Total challenges: {achievements.Count}, Total blocks: {_challengeBlocks.Count}");
                    break;
                }
                _challengeBlocks[i].SetData(achievements[i].achievement);
            }
        }
        
        private void SetRecords()
        {
            var recordData = ViewModel.PlayerRecordData;
            highscoreText.text = recordData.highScore.score.ToString("N0");
            mostFitText.text = recordData.mostFitMe.fitMe.ToString("N0");
            var records = ViewModel.PlayerRecordData.RunDataList;
            for (var i = 0; i < _recordBlocks.Count; i++)
            {
                if (i >= records.Count)
                {
                    Debug.LogWarning($"Not enough records to fill the record blocks. " +
                                     $"Total records: {records.Count}, Total blocks: {_recordBlocks.Count}");
                    break;
                }
                _recordBlocks[i].SetData(records[i]);
            }
        }
    }
}