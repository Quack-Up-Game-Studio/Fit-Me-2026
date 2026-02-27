using System;
using FitMe.Shared;
using QuackUp.Save;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.SocialService.Android
{
    [Serializable]
    public class GPGSSavedGameHandlerInstaller : IInstaller
    {
        [ShowInInspector] private InspectorPlaceholder _title;
        [SerializeField] private RemoteSaveResolverConfig remoteSaveResolverConfig;
        
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(remoteSaveResolverConfig);
            builder.Register<RemoteSaveResolver>(Lifetime.Singleton).AsSelf();
            builder.Register<GPGSSavedGamesHandler>(Lifetime.Singleton)
                .As<ICloudSaveService>();
        }
    }
}