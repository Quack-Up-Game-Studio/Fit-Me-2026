using System;
using FitMe.Panel;
using QuackUp.Utils;
using VContainer;
using VContainer.Unity;

namespace FitMe.Scene
{
    public class SplashScreenManagerInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            // Register Message Hub
            builder.Register<SplashScreenMessageHub>(Lifetime.Singleton)
                .AsSelf()
                .As<IMessageHub>();

            // Register Manager as a normal Singleton
            builder.Register<SplashScreenManager>(Lifetime.Singleton).AsSelf();

            // Resolve the manager at least once in the build callback to instantiate it
            builder.RegisterBuildCallback(container =>
            {
                DebugUtils.Log("SplashScreenManagerInstaller: Resolving SplashScreenManager to instantiate it.");
                container.Resolve<SplashScreenManager>();
            });
        }
    }
}