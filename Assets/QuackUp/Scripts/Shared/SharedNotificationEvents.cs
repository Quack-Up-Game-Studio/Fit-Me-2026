using System;
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using QuackUp.Utils;
using R3;
using Sirenix.Serialization;
using UnityEngine;
using UnityEngine.SocialPlatforms;

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
    
    
}