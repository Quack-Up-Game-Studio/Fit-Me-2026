using System;
using FitMe.Shared;
using QuackUp.SocialService;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using VContainer;
using VContainer.Unity;

namespace FitMe.SocialService.Android
{
    [Serializable]
    public class GPGSAuthenticationHandlerInstaller : IInstaller
    {
        [ShowInInspector] private InspectorPlaceholder _title;
        
        public void Install(IContainerBuilder builder)
        {
            builder.Register<GPGSAuthenticationHandler>(Lifetime.Singleton)
                .As<IAuthenticationService>()
                .As<IUserDataProvider>();
        }
    }
}