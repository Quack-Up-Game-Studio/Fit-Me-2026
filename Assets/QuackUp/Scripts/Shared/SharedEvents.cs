using System;
using Cysharp.Threading.Tasks;
using Sirenix.Serialization;
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

        public NotificationDisplayEvent(NotificationType notificationType, INotificationData data)
        {
            this.notificationType = notificationType;
            this.data = data;
        }
    }
}