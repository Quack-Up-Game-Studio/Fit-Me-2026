using System;
using QuackUp.Core;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace QuackUp.Core
{
    [Serializable]
    public record LoadSceneManagerDebugData : IDebugData
    {
        [field: SerializeField] public bool ConstantUpdate { get; private set; }
        [field: SerializeField] public bool AutoCloseWhenPlayModeEnds { get; private set; }
        [ShowInInspector] private LoadSceneManager _manager;
        
        public LoadSceneManagerDebugData(
            LoadSceneManager manager) 
        {
            ConstantUpdate = false;
            AutoCloseWhenPlayModeEnds = true;
            _manager = manager;
        }
    }
    
    [Serializable]
    public class LoadSceneManagerInstaller : IInstaller
    {
        [Title("Scene Management")]
        [Required, InlineEditor,
         SerializeField] private LoadSceneManagerConfig config;
        [Required, 
         SerializeField] private LoadSceneTransitionView transitionScreen;
        
#if UNITY_EDITOR
        [Title("Debug")]
        [HideInEditorMode]
        [Button("Open Debug Window")]
        private void OpenDebugWindow()
        {
            DebugEditorWindow.Inspect(_loadSceneManagerDebugData, "Load Scene Manager Debug");
        }
        
        private LoadSceneManagerDebugData _loadSceneManagerDebugData;
#endif
        
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(config).AsSelf();
            builder.RegisterComponent(transitionScreen)
                .As<ITransitionable>();
            builder.RegisterEntryPoint<LoadSceneManager>().AsSelf();
            builder.RegisterBuildCallback(x =>
            {
#if UNITY_EDITOR
                var manager = x.Resolve<LoadSceneManager>();
                _loadSceneManagerDebugData = new LoadSceneManagerDebugData(manager);
#endif
            });
        }
    }
}