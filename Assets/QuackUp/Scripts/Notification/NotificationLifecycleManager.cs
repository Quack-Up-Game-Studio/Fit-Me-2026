using System;
using FitMe.GameData;
using QuackUp.Utils;
using R3;
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
        private IDisposable _energySubscription;

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

            // Pre-schedule initial daily reminder so we always have one in queue
            _notificationService.ScheduleDailyReminder();

            // Subscribe to energy changes to pre-schedule energy full notifications instantly
            _energySubscription = _energyManager.CurrentEnergy
                .Subscribe(currentEnergy =>
                {
                    int maxEnergy = _energyManager.Config.MaxEnergy;
                    float secondsPerEnergy = (float)_energyManager.Config.EnergyRechargeTime.TimeSpan.TotalSeconds;
                    double timeUntilNext = _energyManager.TimeUntilNextRecharge.CurrentValue.TotalSeconds;
                    
                    _notificationService.ScheduleEnergyFullNotification(currentEnergy, maxEnergy, secondsPerEnergy, timeUntilNext);
                });

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

            int currentEnergy = _energyManager.CurrentEnergy.CurrentValue;
            int maxEnergy = _energyManager.Config.MaxEnergy;
            float secondsPerEnergy = (float)_energyManager.Config.EnergyRechargeTime.TimeSpan.TotalSeconds;
            double timeUntilNext = _energyManager.TimeUntilNextRecharge.CurrentValue.TotalSeconds;

            _notificationService.ScheduleEnergyFullNotification(currentEnergy, maxEnergy, secondsPerEnergy, timeUntilNext);
            _notificationService.ScheduleDailyReminder();
        }

        private void OnAppResumed()
        {
            if (!_isPaused) return;
            _isPaused = false;

            DebugUtils.Log("Application resumed. Cancelling notifications.");

            // Cancel all notifications when returning to the game
            _notificationService.CancelAllNotifications();

            // Re-schedule daily reminder immediately in case of swipe/force close during gameplay
            _notificationService.ScheduleDailyReminder();
        }

        public void Dispose()
        {
            Cleanup();
        }

        private void Cleanup()
        {
            _energySubscription?.Dispose();
            _energySubscription = null;

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
