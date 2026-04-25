using System;
using FitMe.Shared;
using QuackUp.Audio;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FitMe.Scene.MainMenu
{
    [Serializable]
    public class MainMenuManagerInstaller : IInstaller
    {
        [ShowInInspector] private InspectorPlaceholder _title;
        [SerializeField] private MainMenuManagerConfig mainMenuManagerConfig;
        
        public void Install(IContainerBuilder builder)
        {
            var gameStateManagerMock = new LevelManagerMock(GameState.PlaceBlock);
            builder.RegisterInstance(mainMenuManagerConfig).AsSelf();
            builder.RegisterInstance(gameStateManagerMock).AsSelf()
                .As<ILevelManager>()
                .As<IGameStateManager>()
                .As<IScoreManager>();
            builder.Register<IMessageHub, MainMenuManagerMessageHub>(Lifetime.Singleton)
                .Keyed(MainMenuManagerMessageHub.MainMenuManagerMessageHubKey);
            builder.RegisterEntryPoint<MainMenuManager>().AsSelf();
        }
    }
}