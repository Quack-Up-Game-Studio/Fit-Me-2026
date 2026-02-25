using System;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Shared
{
    public interface IUserDataProvider
    {
        Sprite Avatar { get; }
        string DisplayName { get; }
    }
    
    [Serializable]
    public class MockUserDataProviderInstaller : IInstaller
    {
        [ShowInInspector] private InspectorPlaceholder _title;
        
        public void Install(IContainerBuilder builder)
        {
            builder.Register<MockUserDataProvider>(Lifetime.Singleton)
                .As<IUserDataProvider>();
        }
    }
    
    public class MockUserDataProvider : IUserDataProvider
    {
        public Sprite Avatar => null;
        public string DisplayName => "Mock User";
    }
}