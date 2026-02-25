using System;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using VContainer;
using VContainer.Unity;

namespace FitMe.Shared
{
    [Serializable]
    public class MockSocialServiceInstaller : IInstaller
    {
        [ShowInInspector] private InspectorPlaceholder _title;
        
        public void Install(IContainerBuilder builder)
        {
            builder.Register<MockAuthenticationService>(Lifetime.Singleton)
                .As<IAuthenticationService>();
            builder.Register<MockUserDataProvider>(Lifetime.Singleton)
                .As<IUserDataProvider>();
            builder.Register<MockCloudSaveService>(Lifetime.Singleton)
                .As<ICloudSaveService>();
        }
    }
}