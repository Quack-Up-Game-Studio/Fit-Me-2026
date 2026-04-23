using System;
using System.Collections.Generic;
using FitMe.Shared;
using QuackUp.Audio;
using QuackUp.Input;
using QuackUp.Utils;
using R3;
using Sirenix.Serialization;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

namespace FitMe.Grid
{
    public class BlockInstance
    {
        public BlockModel Model { get; private set; }
        public BlockViewModel ViewModel { get; private set; }
        public BlockController Controller { get; private set; }
        public GameObject GameObject { get; private set; }
        
        public BlockInstance(BlockModel model, BlockViewModel viewModel, BlockController controller, GameObject gameObject)
        {
            Model = model;
            ViewModel = viewModel;
            Controller = controller;
            GameObject = gameObject;
        }
    }
    public class BlockFactory
    {
        private readonly BlockView _blockViewPrefab;
        private readonly BlockManagerConfig _blockManagerConfig;
        private readonly GridManagerConfig _gridConfig;
        private readonly AtomFactory _atomFactory;
        private readonly BlockControllerFactory _blockControllerFactory;

        [Inject]
        public BlockFactory(
            BlockView blockViewPrefab,
            BlockManagerConfig blockManagerConfig,
            GridManagerConfig gridConfig,
            AtomFactory atomFactory,
            BlockControllerFactory blockControllerFactory)
        {
            _blockViewPrefab = blockViewPrefab;
            _blockManagerConfig = blockManagerConfig;
            _gridConfig = gridConfig;
            _atomFactory = atomFactory;
            _blockControllerFactory = blockControllerFactory;
        }

        public BlockInstance Create(BlockShape blockShape, Vector3 position, Quaternion rotation,
            InstantiateParameters? instantiateParameters = null)
        {
            if (!_blockManagerConfig.BlockConfigDictionary.TryGetValue(blockShape, out var blockConfig))
            {
                throw new ArgumentException($"Block config '{blockShape}' not found in BlockConfigDictionary.");
            }
            if (!_blockManagerConfig.BlockPresetDictionary.TryGetValue(blockShape, out var blockPreset))
            {
                throw new ArgumentException($"Block face '{blockShape}' not found in BlockPresetDictionary.");
            }
            instantiateParameters ??= new InstantiateParameters
            {
                //parent = blocksParent
            };
            var view = Object.Instantiate(_blockViewPrefab, position, rotation,
                instantiateParameters.Value);
            var model = new BlockModel(blockConfig, _atomFactory);
            var viewModel = new BlockViewModel(model);
            var controller = _blockControllerFactory.Create();
            view.Construct(blockConfig, 
                _blockManagerConfig, 
                _gridConfig, 
                controller, 
                viewModel);
            var blockInstance = new BlockInstance(model, viewModel, controller, view.gameObject);
            controller.Initialize(blockInstance);
            model.GenerateAtom(blockShape, blockPreset, blockInstance);
            viewModel.SetSortingLayerCommand.Execute(_blockManagerConfig.SpawnSortingLayer);
            return blockInstance;
        }
    }

    public class BlockControllerFactory : IFactory<BlockController>
    {
        public BlockController Current { get; private set; }
        
        private readonly BlockManagerConfig _blockManagerConfig;
        private readonly GridManager _gridManager;
        private readonly IGameStateManager _gameStateManager;
        private readonly IAudioManager _audioManager;
        private readonly IPointerHandler _pointerHandler;

        [Inject]
        public BlockControllerFactory(
            BlockManagerConfig blockManagerConfig,
            GridManager gridManager,
            IGameStateManager gameStateManager,
            IAudioManager audioManager,
            IPointerHandler pointerHandler)
        {
            _blockManagerConfig = blockManagerConfig;
            _gridManager = gridManager;
            _gameStateManager = gameStateManager;
            _audioManager = audioManager;
            _pointerHandler = pointerHandler;
        }
        
        public BlockController Create()
        {
            var controller = new BlockController(
                _blockManagerConfig,
                _gridManager,
                _gameStateManager,
                _audioManager,
                _pointerHandler);
            Current = controller;
            return Current;
        }
    }
}