using System;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace QuackUp.Utils
{
    public class VsyncAndFramerateController : IPostInitializable
    {
        private readonly int _targetFramerate;
        private readonly int _targetVsyncCount;
        private readonly bool _syncFramerateToRefreshRate;
        
        public VsyncAndFramerateController(
            int targetFramerate,
            int targetVsyncCount,
            bool syncFramerateToRefreshRate)
        {
            _targetFramerate = targetFramerate;
            _targetVsyncCount = targetVsyncCount;
            _syncFramerateToRefreshRate = syncFramerateToRefreshRate;
        }
        
        public void PostInitialize()
        {
            QualitySettings.vSyncCount = _targetVsyncCount;
            if (_syncFramerateToRefreshRate)
            {
                Application.targetFrameRate = (int)Screen.currentResolution.refreshRateRatio.value;
            }
            else
            {
                Application.targetFrameRate = _targetFramerate;
            }
        }
    }

    [Serializable]
    public class VsyncAndFramerateControllerInstaller : IInstaller
    {
        [Title("Vsync and Framerate Settings")]
        [SerializeField, Range(0, 4)] private int vsyncCount;
        [SerializeField] private bool syncFramerateToRefreshRate;
        [SerializeField, HideIf(nameof(syncFramerateToRefreshRate))] private int targetFramerate = 60;
        
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint(_ =>
                new VsyncAndFramerateController(
                    targetFramerate,
                    vsyncCount,
                    syncFramerateToRefreshRate), 
                Lifetime.Singleton);
        }
    }
}