using System;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace QuackUp.Utils
{
    public class FramerateLimiter : IPostInitializable
    {
        private readonly int _targetFramerate;
        
        public FramerateLimiter(int targetFramerate)
        {
            _targetFramerate = targetFramerate;
        }
        
        public void PostInitialize()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = _targetFramerate;
        }
    }

    [Serializable]
    public class FramerateLimiterInstaller : IInstaller
    {
        [Title("Framerate Limiter")]
        [SerializeField] private int targetFramerate = 60;
        
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint(_ => 
                new FramerateLimiter(targetFramerate), Lifetime.Singleton);
        }
    }
}