using System;
using FitMe.Grid;
using FitMe.Shared;
using QuackUp.Utils;
using R3;
using Redcode.Extensions;
using VContainer;
using VContainer.Unity;

namespace FitMe.Scene.MainMenu
{
    public class MainMenuManager : IStartable, IDisposable
    {
        private readonly BlockManagerConfig _blockManagerConfig;
        private readonly IMessageHub _messageHub;
        
        private IDisposable _subscriptions;
        
        [Inject]
        public MainMenuManager(
            BlockManagerConfig blockManagerConfig,
            [Key(MainMenuManagerMessageHub.MainMenuManagerMessageHubKey)] IMessageHub messageHub)
        {
            _blockManagerConfig = blockManagerConfig;
            _messageHub = messageHub;
            Subscribe();
        }
        
        private void Subscribe()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            _subscriptions = disposableBuilder.Build();
        }
        
        public void Start()
        {
            var randomPreset = _blockManagerConfig.BlockPresetDictionary.Values.GetRandomElement();
            _messageHub.Publish(new StartSpawnEvent(randomPreset));
        }

        public void Dispose()
        {
            _subscriptions?.Dispose();
        }
    }
}