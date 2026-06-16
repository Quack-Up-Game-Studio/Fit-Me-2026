using System;
#if UNITY_ANDROID
using Unity.Notifications.Android;
#elif UNITY_IOS
using Unity.Notifications.iOS;
#endif
using UnityEngine;

namespace QuackUp.Notification
{
    public class NotificationService
    {
        private const string EnergyChannelId = "energy_updates";
        private const string ReminderChannelId = "daily_reminders";

        private struct NotificationMessage
        {
            public string Title { get; }
            public string Body { get; }

            public NotificationMessage(string title, string body)
            {
                Title = title;
                Body = body;
            }
        }

        private static readonly NotificationMessage[] _energyFullMessages = new[]
        {
            new NotificationMessage("⚡ Energy Full!", "Your energy is fully recharged. Ready to play?"),
            new NotificationMessage("Fully Charged! 🔋", "Your energy is maxed out. Let's go!"),
            new NotificationMessage("Ding! Fully reloaded 🔋", "Grab your phone, All your energy is waiting for you!")
        };

        private static readonly NotificationMessage[] _dailyReminderMessages = new[]
        {
            new NotificationMessage("New High Score? 🏆", "Can you beat your best? Play a quick round!"),
            new NotificationMessage("Break time? ☕", "Kick back and come join the fun!"),
            new NotificationMessage("Game On! 🎮", "Take a quick break and set a new record today."),
            new NotificationMessage("Just dropping by! 👋", "Come play a quick round. We saved your spot!")
        };

        public NotificationService()
        {
            InitializeChannels();
        }

        private void InitializeChannels()
        {
#if UNITY_ANDROID
            var energyChannel = new AndroidNotificationChannel()
            {
                Id = EnergyChannelId,
                Name = "Energy Status",
                Importance = Importance.Default,
                Description = "Notifies you when your energy is fully charged."
            };
            AndroidNotificationCenter.RegisterNotificationChannel(energyChannel);

            var reminderChannel = new AndroidNotificationChannel()
            {
                Id = ReminderChannelId,
                Name = "Daily Reminders",
                Importance = Importance.Default,
                Description = "Friendly daily reminders to keep fitting blocks!"
            };
            AndroidNotificationCenter.RegisterNotificationChannel(reminderChannel);
#endif
        }
        
        private const int EnergyNotificationId = 1;
        private const string EnergyNotificationIdentifier = "energy_full";
        private const int ReminderNotificationId = 2;
        private const string ReminderNotificationIdentifier = "daily_reminder";

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
            // ถ้าพลังงานเต็มอยู่แล้ว ก็ไม่ต้องทำอะไรและลบตัวเก่าทิ้งค่ะ
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

            var message = _energyFullMessages[UnityEngine.Random.Range(0, _energyFullMessages.Length)];

            ScheduleNotification(
                message.Title,
                message.Body,
                fullChargeTime,
                EnergyChannelId,
                EnergyNotificationId,
                EnergyNotificationIdentifier
            );
        }

        public void ScheduleDailyReminder()
        {
            DateTime tomorrow = DateTime.Now.AddDays(1);

            var message = _dailyReminderMessages[UnityEngine.Random.Range(0, _dailyReminderMessages.Length)];

            ScheduleNotification(
                message.Title,
                message.Body,
                tomorrow,
                ReminderChannelId,
                ReminderNotificationId,
                ReminderNotificationIdentifier
            );
        }

        /// <summary>
        /// ยกเลิกการแจ้งเตือนที่ตั้งเวลาไว้ทั้งหมด (มีประโยชน์มากเวลาที่ผู้เล่นเปิดเกมขึ้นมาค่ะ)
        /// </summary>
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