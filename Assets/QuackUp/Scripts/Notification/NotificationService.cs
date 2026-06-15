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