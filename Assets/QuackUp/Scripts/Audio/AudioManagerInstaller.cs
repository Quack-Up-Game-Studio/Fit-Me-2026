using System;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace QuackUp.Audio
{
    [Serializable]
    public class AudioManagerInstaller : IInstaller
    {
        [Title("Audio")]
        [SerializeField] private AudioManagerConfig audioManagerConfig;
        
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(audioManagerConfig).AsSelf();
            builder.RegisterEntryPoint<AudioManager>()
                .As<IAudioManager>()
                .As<IAudioBusManager>()
                .AsSelf();
        }
    }
}