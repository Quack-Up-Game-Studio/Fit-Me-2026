using System;
using Cysharp.Threading.Tasks;
using QuackUp.Save;
using QuackUp.Utils;
using R3;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace FitMe.GameData
{
    public class PlaytimeTracker : IPostInitializable, IDisposable
    {
        private static readonly TimeSpan _playtimeCheckpointInterval = TimeSpan.FromMinutes(1);

        private readonly MessagePackSaveManager _saveManager;

        private PlayerRecordSaveObject _saveObject;
        private IDisposable _playtimeTracker;
        private PlaytimePauseHandler _pauseHandler;
        private TimeSpan _timeSinceLastCheckpointAttempt;
        private bool _hasUncheckpointedPlaytime;
        private bool _isDisposed;
        
        [Inject]
        public PlaytimeTracker(MessagePackSaveManager saveManager)
        {
            _saveManager = saveManager;
        }
        
        public void PostInitialize()
        {
            InitializeAsync().Forget();
        }

        private async UniTaskVoid InitializeAsync()
        {
            await _saveManager.WaitForSaveDataReady;
            if (_isDisposed) return;
            _saveObject = _saveManager.GetFirstSaveObjectOfType<PlayerRecordSaveObject>();
            StartTimer();
            Application.focusChanged += OnApplicationFocusChanged;
            Application.quitting += OnApplicationQuitting;
            CreatePauseHandler();
        }
        
        private void CreatePauseHandler()
        {
            var handlerObject = new GameObject("PlaytimePauseHandler")
            {
                hideFlags = HideFlags.HideAndDontSave // Hides from hierarchy
            };
            Object.DontDestroyOnLoad(handlerObject); // Persists across scenes
            _pauseHandler = handlerObject.AddComponent<PlaytimePauseHandler>();
            _pauseHandler.Construct(this);
        }
        
        private void OnApplicationFocusChanged(bool hasFocus)
        {
            if (hasFocus)
            {
                StartTimer();
            }
            else
            {
                StopTimer();
            }
        }
        
        private void OnApplicationQuitting()
        {
            Dispose(checkpointPendingPlaytime: false);
            var playerSaveObject = _saveManager.GetFirstSaveObjectOfType<PlayerRecordSaveObject>();
            _saveManager.Save(playerSaveObject);
        }

        public void StartTimer()
        {
            if (_isDisposed || _playtimeTracker != null || !_saveObject) return;
            _playtimeTracker = Observable.Interval(TimeSpan.FromSeconds(1)) // Update every second to reduce overhead, instead of every frame
                .Subscribe(_ => OnPlaytimeTick());
        }

        private void OnPlaytimeTick()
        {
            var saveData = _saveObject.GetSaveData<PlayerRecordSaveData>();
            if (saveData == null) return;

            saveData.TotalPlayTime += TimeSpan.FromSeconds(1);
            _hasUncheckpointedPlaytime = true;
            _timeSinceLastCheckpointAttempt += TimeSpan.FromSeconds(1);
            if (_timeSinceLastCheckpointAttempt < _playtimeCheckpointInterval) return;

            _timeSinceLastCheckpointAttempt = TimeSpan.Zero;
            CheckpointPlaytime();
        }

        private void CheckpointPlaytime()
        {
            if (!_hasUncheckpointedPlaytime || !_saveObject) return;

            try
            {
                _saveManager.Save(_saveObject);
                _hasUncheckpointedPlaytime = false;
                _timeSinceLastCheckpointAttempt = TimeSpan.Zero;
            }
            catch (Exception exception)
            {
                DebugUtils.LogError($"Failed to checkpoint playtime: {exception}");
            }
        }

        public void StopTimer()
        {
            StopTimer(checkpointPendingPlaytime: true);
        }

        private void StopTimer(bool checkpointPendingPlaytime)
        {
            var timer = _playtimeTracker;
            _playtimeTracker = null;
            timer?.Dispose();
            if (checkpointPendingPlaytime) CheckpointPlaytime();
        }

        public void Dispose()
        {
            Dispose(checkpointPendingPlaytime: true);
        }

        private void Dispose(bool checkpointPendingPlaytime)
        {
            if (_isDisposed) return;
            _isDisposed = true;
            StopTimer(checkpointPendingPlaytime);
            Application.focusChanged -= OnApplicationFocusChanged;
            Application.quitting -= OnApplicationQuitting;
            if (_pauseHandler)
            {
                Object.Destroy(_pauseHandler.gameObject);
                _pauseHandler = null;
            }
        }
    }
}