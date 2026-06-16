using System;
using FitMe.GameData;
using QuackUp.Utils;
using VContainer.Unity;
using UnityEngine;
using Object = UnityEngine.Object;

namespace QuackUp.Notification
{
    public class NotificationLifecycleManager : IStartable, IDisposable
    {
        private readonly NotificationService _notificationService;
        private readonly EnergyManager _energyManager;
        private GameObject _pauseHandlerObject;

        private bool _isPaused;

        public NotificationLifecycleManager(NotificationService notificationService, EnergyManager energyManager)
        {
            _notificationService = notificationService;
            _energyManager = energyManager;
        }

        public void Start()
        {
            DebugUtils.Log("NotificationLifecycleManager initialized. Cancelling all active notifications.");
            // Cancel any notifications currently scheduled when player opens/resumes the game
            _notificationService.CancelAllNotifications();

            Application.focusChanged += OnApplicationFocusChanged;
            Application.quitting += OnApplicationQuitting;
            CreatePauseHandler();
        }

        private void CreatePauseHandler()
        {
            _pauseHandlerObject = new GameObject("NotificationPauseHandler")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            Object.DontDestroyOnLoad(_pauseHandlerObject);
            var handler = _pauseHandlerObject.AddComponent<NotificationPauseHandler>();
            handler.Construct(this);
        }

        private void OnApplicationFocusChanged(bool hasFocus)
        {
            if (hasFocus)
            {
                OnAppResumed();
            }
            else
            {
                OnAppPaused();
            }
        }

        public void OnApplicationPauseChanged(bool isPaused)
        {
            if (isPaused)
            {
                OnAppPaused();
            }
            else
            {
                OnAppResumed();
            }
        }

        private void OnApplicationQuitting()
        {
            OnAppPaused();
            Cleanup();
        }

        private void OnAppPaused()
        {
            if (_isPaused) return;
            _isPaused = true;

            DebugUtils.Log("Application paused. Scheduling notifications.");

            int currentEnergy = _energyManager.CurrentEnergy.Value;
            int maxEnergy = _energyManager.Config.MaxEnergy;
            float secondsPerEnergy = (float)_energyManager.Config.EnergyRechargeTime.TimeSpan.TotalSeconds;

            _notificationService.ScheduleEnergyFullNotification(currentEnergy, maxEnergy, secondsPerEnergy);
            _notificationService.ScheduleDailyReminder();
        }

        private void OnAppResumed()
        {
            if (!_isPaused) return;
            _isPaused = false;

            DebugUtils.Log("Application resumed. Cancelling notifications.");

            // Cancel all notifications when returning to the game
            _notificationService.CancelAllNotifications();
        }

        public void Dispose()
        {
            Cleanup();
        }

        private void Cleanup()
        {
            Application.focusChanged -= OnApplicationFocusChanged;
            Application.quitting -= OnApplicationQuitting;
            if (_pauseHandlerObject != null)
            {
                Object.Destroy(_pauseHandlerObject);
                _pauseHandlerObject = null;
            }
        }
    }
}
