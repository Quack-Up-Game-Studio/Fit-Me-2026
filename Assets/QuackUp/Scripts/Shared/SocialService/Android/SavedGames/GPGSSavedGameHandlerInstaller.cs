using System;
using FitMe.Shared;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using VContainer;
using VContainer.Unity;

namespace FitMe.SocialService.Android
{
    [Serializable]
    public class GPGSSavedGameHandlerInstaller : IInstaller
    {
        [ShowInInspector] private InspectorPlaceholder _title;
        
        public void Install(IContainerBuilder builder)
        {
            builder.Register<GPGSSavedGamesHandler>(Lifetime.Singleton)
                .As<ICloudSaveService>();
        }
    }
}