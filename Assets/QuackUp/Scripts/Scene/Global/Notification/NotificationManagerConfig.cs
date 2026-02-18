using System.Collections.Generic;
using FitMe.Shared;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace FitMe.Scene
{
    [CreateAssetMenu(fileName = "NotificationManagerConfig", menuName = "FitMe/Scene/Global/NotificationManagerConfig")]
    [ShowOdinSerializedPropertiesInInspector]
    public class NotificationManagerConfig : SerializedScriptableObject
    {
        [Title("References")]
        [OdinSerialize] private Dictionary<NotificationType, NotificationPrefabData> notificationPrefabDictionary = new();
        public IReadOnlyDictionary<NotificationType, NotificationPrefabData> NotificationPrefabDictionary => notificationPrefabDictionary;
        
        [Title("Settings")]
        [field: SerializeField] public float NotificationStayDuration { get; set; } = 2f;
    }
}