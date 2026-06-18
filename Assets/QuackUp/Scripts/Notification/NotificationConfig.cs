using System;
using UnityEngine;

namespace FitMe.Notification
{
    [Serializable]
    public struct NotificationMessage
    {
        [SerializeField] public string title;
        [SerializeField] public string body;
    }

    [Serializable]
    public struct ChannelConfig
    {
        public string channelId;
        public string channelName;
        public string channelDescription;
    }

    [CreateAssetMenu(fileName = "NotificationConfig", menuName = "FitMe/Notification/NotificationConfig", order = 0)]
    public class NotificationConfig : ScriptableObject
    {
        [Header("Android Channels")]
        public ChannelConfig energyChannel = new()
        {
            channelId = "energy_updates",
            channelName = "Energy Status",
            channelDescription = "Notifies you when your energy is fully charged."
        };

        public ChannelConfig reminderChannel = new()
        {
            channelId = "daily_reminders",
            channelName = "Daily Reminders",
            channelDescription = "Friendly daily reminders to keep fitting blocks!"
        };

        [Header("Daily Reminder Settings")]
        [Tooltip("Delay in hours for the daily reminder. Default is 24 hours. Set to a small value (e.g., 0.0166 for 1 minute) for testing.")]
        public float dailyReminderDelayHours = 24f;

        [Header("Energy Notification Messages")]
        public NotificationMessage[] energyFullMessages = new[]
        {
            new NotificationMessage { title = "⚡ Energy Full!", body = "Your energy is fully recharged. Ready to play?" },
            new NotificationMessage { title = "Fully Charged! 🔋", body = "Your energy is maxed out. Let's go!" },
            new NotificationMessage { title = "Ding! Fully reloaded 🔋", body = "Grab your phone, All your energy is waiting for you!" }
        };

        [Header("Daily Reminder Messages")]
        public NotificationMessage[] dailyReminderMessages = new[]
        {
            new NotificationMessage { title = "New High Score? 🏆", body = "Can you beat your best? Play a quick round!" },
            new NotificationMessage { title = "Break time? ☕", body = "Kick back and come join the fun!" },
            new NotificationMessage { title = "Game On! 🎮", body = "Take a quick break and set a new record today." },
            new NotificationMessage { title = "Just dropping by! 👋", body = "Come play a quick round. We saved your spot!" }
        };
    }
}
