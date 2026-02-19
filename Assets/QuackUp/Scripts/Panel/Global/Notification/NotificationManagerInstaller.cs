using System;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Panel
{
    [Serializable]
    public class NotificationManagerDebugData : DebugDataBase
    {
        [SerializeField] private NotificationManager notificationManager;
        public NotificationManagerDebugData(NotificationManager notificationManager)
        {
            this.notificationManager = notificationManager;
        }
    }
    
    [Serializable]
    public class NotificationManagerInstaller : IInstaller
    {
        [ShowInInspector] private InspectorPlaceholder _title;
        [SerializeField] private NotificationManagerConfig config;
        [SerializeField] private Transform notificationParentTransform;
        
        private NotificationManagerDebugData _debugData;
        
        #if UNITY_EDITOR
        [Title("Debug")]
        [HideInEditorMode]
        [Button("Open Debug Window")]
        private void OpenDebugWindow()
        {
            DebugEditorWindow.Inspect(_debugData, nameof(NotificationManager));
        }
        #endif
        
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(config);
            builder.RegisterInstance(notificationParentTransform).Keyed(NotificationManager.NotificationParentKey);
            builder.Register<NotificationManager>(Lifetime.Singleton);
            builder.RegisterBuildCallback(x =>
            {
                var manager = x.Resolve<NotificationManager>();
                _debugData = new NotificationManagerDebugData(manager);
            });
        }
    }
}