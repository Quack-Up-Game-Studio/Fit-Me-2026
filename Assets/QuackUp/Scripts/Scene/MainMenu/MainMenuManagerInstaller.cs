using System;
using FitMe.Shared;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using VContainer;
using VContainer.Unity;

namespace FitMe.Scene.MainMenu
{
    [Serializable]
    public class MainMenuManagerInstaller : IInstaller
    {
        [ShowInInspector] private InspectorPlaceholder _title;
        
        public void Install(IContainerBuilder builder)
        {
            var gameStateManagerMock = new GameStateManagerMock(GameState.PlaceBlock);
            builder.RegisterInstance<IGameStateManager, GameStateManagerMock>(gameStateManagerMock);
            builder.Register<IMessageHub, MainMenuManagerMessageHub>(Lifetime.Singleton)
                .Keyed(MainMenuManagerMessageHub.MainMenuManagerMessageHubKey);
            builder.RegisterEntryPoint<MainMenuManager>().AsSelf();
        }
    }
}