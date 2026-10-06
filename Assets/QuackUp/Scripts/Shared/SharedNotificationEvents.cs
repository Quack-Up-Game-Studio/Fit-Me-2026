using System;
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using MessagePipe;
using QuackUp.Utils;
using R3;
using Sirenix.Serialization;
using UnityEngine;
using UnityEngine.SocialPlatforms;
using VContainer;

namespace FitMe.Shared
{
    public interface INotificationData { }
    
    public interface INotificationView
    {
        void Initialize();
        UniTask Show();
        UniTask PlayAnimation();
        UniTask Hide();
        void Cancel();
        void SetData<T>(T data) where T : INotificationData;
    }
    
    public enum NotificationType
    {
        General,
        Challenge
    }
    
    [Serializable]
    public struct NotificationDisplayEvent
    {
        public NotificationType notificationType;
        [OdinSerialize] public INotificationData data;
        public Promise<Unit> CompletionPromise;

        public NotificationDisplayEvent(NotificationType notificationType, INotificationData data, Promise<Unit> completionPromise = null)
        {
            this.notificationType = notificationType;
            this.data = data;
            this.CompletionPromise = completionPromise;
        }
    }
    
    [Serializable]
    public struct GeneralNotificationData : INotificationData
    {
        public string message;
        [CanBeNull] public Sprite icon;
    }
    
    public class NotificationMessageHub : MessageHub
    {
        public const string MessageHubKey = "NotificationMessageHub";

        [Inject]
        public NotificationMessageHub(
            IPublisher<NotificationDisplayEvent> notificationPublisher,
            ISubscriber<NotificationDisplayEvent> notificationSubscriber)
        {
            MessageWrappers[typeof(NotificationDisplayEvent)] =
                new MessageWrapper<NotificationDisplayEvent>(notificationPublisher, notificationSubscriber);
        }
    }
    
}