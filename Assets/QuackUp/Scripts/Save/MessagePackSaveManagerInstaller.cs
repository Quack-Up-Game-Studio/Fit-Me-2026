using System;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace QuackUp.Save
{
    [Serializable]
    public record MessagePackDebugData : IDebugData
    {
        [field: SerializeField] public bool ConstantUpdate { get; private set; }
        [field: SerializeField] public bool AutoCloseWhenPlayModeEnds { get; private set; }
        [ShowInInspector] private MessagePackSaveManager _manager;
        
        public MessagePackDebugData(
            MessagePackSaveManager manager) 
        {
            ConstantUpdate = false;
            AutoCloseWhenPlayModeEnds = true;
            _manager = manager;
        }
    }
    
    [Serializable]
    public class MessagePackSaveManagerInstaller : IInstaller
    {
        [Title("MessagePack Save Manager")]
        [Required, InlineEditor, 
         SerializeField] private MessagePackSaveConfig config;
        
#if UNITY_EDITOR
        [Title("Debug")]
        [Button("Open Debug Window")]
        private void OpenDebugWindow()
        {
            DebugEditorWindow.Inspect(_messagePackDebugData, "MessagePack Save Manager Debug");
        }
        
        [HideInPlayMode, 
         Button("Create Debug Save Manager")]
        private void CreateDebugSaveManager()
        {
            var manager = new MessagePackSaveManager(config);
            _messagePackDebugData = new MessagePackDebugData(manager);
        }
        
        private MessagePackDebugData _messagePackDebugData;
#endif
        
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(config).AsSelf();
            builder.RegisterEntryPoint<MessagePackSaveManager>(Lifetime.Singleton)
                .AsSelf();
            builder.RegisterBuildCallback(x =>
            {
                var manager = x.Resolve<MessagePackSaveManager>();
#if UNITY_EDITOR
                _messagePackDebugData = new MessagePackDebugData(manager);
#endif
            });
        }
    }
}