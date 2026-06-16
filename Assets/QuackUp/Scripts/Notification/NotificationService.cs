using System;
#if UNITY_ANDROID
using Unity.Notifications.Android;
#elif UNITY_IOS
using Unity.Notifications.iOS;
#endif
using UnityEngine;
using VContainer;

namespace FitMe.Notification
{
    public class NotificationService
    {
        private readonly NotificationConfig _config;

        private const int EnergyNotificationId = 1;
        private const string EnergyNotificationIdentifier = "energy_full";
        private const int ReminderNotificationId = 2;
        private const string ReminderNotificationIdentifier = "daily_reminder";

        [Inject]
        public NotificationService(NotificationConfig config)
        {
            _config = config;
            InitializeChannels();
        }

        private void InitializeChannels()
        {
#if UNITY_ANDROID
            var energyChannel = new AndroidNotificationChannel()
            {
                Id = _config.energyChannel.channelId,
                Name = _config.energyChannel.channelName,
                Importance = Importance.Default,
                Description = _config.energyChannel.channelDescription
            };
            AndroidNotificationCenter.RegisterNotificationChannel(energyChannel);

            var reminderChannel = new AndroidNotificationChannel()
            {
                Id = _config.reminderChannel.channelId,
                Name = _config.reminderChannel.channelName,
                Importance = Importance.Default,
                Description = _config.reminderChannel.channelDescription
            };
            AndroidNotificationCenter.RegisterNotificationChannel(reminderChannel);
#endif
        }

        public void ScheduleNotification(string title, string text, DateTime fireTime, string channelId, int id, string identifier)
        {
#if UNITY_ANDROID
            var notification = new AndroidNotification
            {
                Title = title,
                Text = text,
                FireTime = fireTime,
                SmallIcon = "icon_small",
                LargeIcon = "icon_large"
            };
            AndroidNotificationCenter.SendNotificationWithExplicitID(notification, channelId, id);
#elif UNITY_IOS
            TimeSpan delay = fireTime - DateTime.Now;
            if (delay.TotalSeconds <= 0)
            {
                delay = TimeSpan.FromSeconds(1);
            }

            var timeTrigger = new iOSNotificationTimeIntervalTrigger
            {
                TimeInterval = delay,
                Repeats = false
            };

            var notification = new iOSNotification
            {
                Identifier = identifier,
                Title = title,
                Body = text,
                ShowInForeground = false,
                Trigger = timeTrigger
            };
            iOSNotificationCenter.ScheduleNotification(notification);
#endif
        }

        public void CancelNotification(int id, string identifier)
        {
#if UNITY_ANDROID
            AndroidNotificationCenter.CancelNotification(id);
#elif UNITY_IOS
            iOSNotificationCenter.RemoveScheduledNotification(identifier);
            iOSNotificationCenter.RemoveDeliveredNotification(identifier);
#endif
        }

        public void ScheduleEnergyFullNotification(int currentEnergy, int maxEnergy, float secondsPerEnergy, double? overrideTimeUntilNext = null)
        {
            if (currentEnergy >= maxEnergy)
            {
                CancelNotification(EnergyNotificationId, EnergyNotificationIdentifier);
                return;
            }

            int missingEnergy = maxEnergy - currentEnergy;
            double totalSecondsNeeded;
            
            if (overrideTimeUntilNext.HasValue)
            {
                double timeUntilNext = overrideTimeUntilNext.Value;
                if (timeUntilNext <= 0 || timeUntilNext > secondsPerEnergy)
                {
                    timeUntilNext = secondsPerEnergy;
                }
                totalSecondsNeeded = timeUntilNext + (missingEnergy - 1) * secondsPerEnergy;
            }
            else
            {
                totalSecondsNeeded = missingEnergy * secondsPerEnergy;
            }

            DateTime fullChargeTime = DateTime.Now.AddSeconds(totalSecondsNeeded);

            if (_config.energyFullMessages == null || _config.energyFullMessages.Length == 0) return;
            var message = _config.energyFullMessages[UnityEngine.Random.Range(0, _config.energyFullMessages.Length)];

            ScheduleNotification(
                message.title,
                message.body,
                fullChargeTime,
                _config.energyChannel.channelId,
                EnergyNotificationId,
                EnergyNotificationIdentifier
            );
        }

        public void ScheduleDailyReminder()
        {
            DateTime tomorrow = DateTime.Now.AddDays(1);

            if (_config.dailyReminderMessages == null || _config.dailyReminderMessages.Length == 0) return;
            var message = _config.dailyReminderMessages[UnityEngine.Random.Range(0, _config.dailyReminderMessages.Length)];

            ScheduleNotification(
                message.title,
                message.body,
                tomorrow,
                _config.reminderChannel.channelId,
                ReminderNotificationId,
                ReminderNotificationIdentifier
            );
        }

        public void CancelAllNotifications()
        {
#if UNITY_ANDROID
            AndroidNotificationCenter.CancelAllScheduledNotifications();
#elif UNITY_IOS
            iOSNotificationCenter.CancelAllScheduledNotifications();
#endif
        }
    }
}