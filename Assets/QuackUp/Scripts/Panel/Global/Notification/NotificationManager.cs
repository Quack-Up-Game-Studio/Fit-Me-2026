using System;
using Debug = QuackUp.Utils.DebugUtils;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using FitMe.Shared;
using FMODUnity;
using QuackUp.Audio;
using QuackUp.Utils;
using R3;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

namespace FitMe.Panel
{
    [Serializable]
    public struct NotificationPrefabData
    {
        [OdinSerialize] public INotificationView notificationViewPrefab;
        public Vector2 initialPosition;
        [SerializeField] public EventReference soundEffect;
    }
    
    [Serializable]
    public class NotificationManager : IDisposable
    {
        [Title("Debug")]
        [Button("Test Notification")]
        private void TestNotification(NotificationDisplayEvent eventData)
        {
            EnqueueNotification(eventData);
        }
        
        public const string NotificationParentKey = "NotificationParentTransform";
        
        private readonly Transform _notificationParentTransform;
        private readonly NotificationManagerConfig _config;
        private readonly IAudioManager _audioManager;
        private readonly IMessageHub _notificationMessageHub;
        
        private readonly Queue<NotificationDisplayEvent> _notificationQueue = new();
        private bool _showingNotification;
        private IDisposable _notificationSubscription;
        
        [Inject]
        public NotificationManager(
            [Key(NotificationParentKey)] Transform notificationParentTransform,
            NotificationManagerConfig config,
            IAudioManager audioManager,
            [Key(NotificationMessageHub.MessageHubKey)] IMessageHub notificationMessageHub)
        {
            _notificationParentTransform = notificationParentTransform;
            _config = config;
            _audioManager = audioManager;
            _notificationMessageHub = notificationMessageHub;
            Subscribe();
        }
        
        private void Subscribe()
        {
            _notificationSubscription = _notificationMessageHub.Subscribe<NotificationDisplayEvent>(EnqueueNotification);
        }

        public void Dispose()
        {
            _notificationSubscription?.Dispose();
        }
        
        private void EnqueueNotification(NotificationDisplayEvent eventData)
        {
            _notificationQueue.Enqueue(eventData);
            if (!_showingNotification)
            {
                ShowNextNotification().Forget();
            }
        }

        private async UniTaskVoid ShowNextNotification()
        {
            _showingNotification = true;
            while (_notificationQueue.Count > 0)
            {
                var notificationEvent = _notificationQueue.Dequeue();
                if (!_config.NotificationPrefabDictionary.TryGetValue(notificationEvent.notificationType, out var prefabData))
                {
                    Debug.LogWarning($"No notification prefab found for type: {notificationEvent.notificationType}");
                    notificationEvent.CompletionPromise?.TrySetResult(Unit.Default);
                    continue;
                }

                var view = prefabData.notificationViewPrefab.InstantiateAsInterface(new InstantiateParameters
                {
                    parent = _notificationParentTransform,
                    worldSpace = false
                }, out var viewObject);
                var rectTransform = (RectTransform)viewObject.transform;
                rectTransform.anchoredPosition = prefabData.initialPosition;
                view.Initialize();
                view.SetData(notificationEvent.data);
                _audioManager.PlayAudioOneShot(prefabData.soundEffect, Vector3.zero);
                await view.Show();
                await UniTask.WhenAll(UniTask.WaitForSeconds(_config.NotificationStayDuration),
                    view.PlayAnimation());
                await view.Hide();
                notificationEvent.CompletionPromise?.TrySetResult(Unit.Default);
                Object.Destroy(viewObject);
                break;
            }

            _showingNotification = false;
            if (_notificationQueue.Count > 0)
            {
                ShowNextNotification().Forget();
            }
        }
    }
}