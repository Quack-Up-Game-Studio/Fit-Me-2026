using System;
using QuackUp.Save;
using R3;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace FitMe.GameData
{
    public class PlaytimeTracker : IPostInitializable, IDisposable
    {
        private readonly MessagePackSaveManager _saveManager;

        private PlayerRecordSaveData _saveData;
        private IDisposable _playtimeTracker;
        
        [Inject]
        public PlaytimeTracker(MessagePackSaveManager saveManager)
        {
            _saveManager = saveManager;
        }
        
        public void PostInitialize()
        {
            _saveData = _saveManager.GetFirstSaveObjectOfType<PlayerRecordSaveObject>()
                .GetSaveData<PlayerRecordSaveData>();
            StartTimer();
            Application.focusChanged += OnApplicationFocusChanged;
            Application.quitting += OnApplicationQuitting;
            CreatePauseHandler();
        }
        
        private void CreatePauseHandler()
        {
            var gameObject = new GameObject("PlaytimePauseHandler")
            {
                hideFlags = HideFlags.HideAndDontSave // Hides from hierarchy
            };
            Object.DontDestroyOnLoad(gameObject); // Persists across scenes
            var handler = gameObject.AddComponent<PlaytimePauseHandler>();
            handler.Construct(this);
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
            StopTimer();
            Application.focusChanged -= OnApplicationFocusChanged;
            Application.quitting -= OnApplicationQuitting;
            var playerSaveObject = _saveManager.GetFirstSaveObjectOfType<PlayerRecordSaveObject>();
            _saveManager.Save(playerSaveObject);
        }

        public void StartTimer()
        {
            _playtimeTracker = Observable.Interval(TimeSpan.FromSeconds(1)) // Update every second to reduce overhead, instead of every frame
                .Subscribe(_ =>
                {
                    _saveData.TotalPlayTime += TimeSpan.FromSeconds(1);
                });
        }

        public void StopTimer()
        {
            _playtimeTracker?.Dispose();
        }

        public void Dispose()
        {
            StopTimer();
        }
    }
}