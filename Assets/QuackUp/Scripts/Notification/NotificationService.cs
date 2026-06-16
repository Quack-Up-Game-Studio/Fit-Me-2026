using System;
using Unity.Notifications.Android;
using Unity.Notifications.iOS;
using UnityEngine;

namespace QuackUp.Notification
{
    public class NotificationService
    {
        private const string EnergyChannelId = "energy_updates";
        private const string ReminderChannelId = "daily_reminders";

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
        
        public void ScheduleNotification(string title, string text, DateTime fireTime, string channelId)
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
            AndroidNotificationCenter.SendNotification(notification, channelId);
#elif UNITY_IOS
            var timeTrigger = new iOSNotificationTimeIntervalTrigger
            {
                TimeInterval = fireTime - DateTime.Now,
                Repeats = false
            };

            var notification = new iOSNotification
            {
                Title = title,
                Body = text,
                ShowInForeground = true,
                Trigger = timeTrigger
            };
            iOSNotificationCenter.ScheduleNotification(notification);
#endif
        }

        public void ScheduleEnergyFullNotification(int currentEnergy, int maxEnergy, float secondsPerEnergy)
        {
            // ถ้าพลังงานเต็มอยู่แล้ว ก็ไม่ต้องทำอะไรค่ะ
            if (currentEnergy >= maxEnergy) return;

            int missingEnergy = maxEnergy - currentEnergy;
            float totalSecondsNeeded = missingEnergy * secondsPerEnergy;

            DateTime fullChargeTime = DateTime.Now.AddSeconds(totalSecondsNeeded);

            ScheduleNotification(
                "Energy Fully Charged! ⚡",
                "Your energy bar is full. Get back to fitting blocks and beat your high score!",
                fullChargeTime,
                EnergyChannelId
            );
        }

        public void ScheduleDailyReminder()
        {
            DateTime tomorrow = DateTime.Now.AddDays(1);

            ScheduleNotification(
                "Block-Fitting Time! 🧩",
                "It's time for your daily block-fitting puzzle. Come beat your high score!",
                tomorrow,
                ReminderChannelId
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