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
    [Serializable]
    public class BlockInstance
    {
        [field: SerializeField] public BlockModel Model { get; private set; }
        public BlockViewModel ViewModel { get; private set; }
        public BlockController Controller { get; private set; }
        [field: SerializeField] public GameObject GameObject { get; private set; }
        
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
            InstantiateParameters? instantiateParameters = null, int[,] schemaOverride = null,
            BlockPreset presetOverride = null, BlockConfig blockConfigOverride = null)
        {
            var blockConfig = _blockManagerConfig.GetBlockConfig(blockShape, blockConfigOverride);

            BlockPreset blockPreset;
            if (presetOverride)
            {
                blockPreset = presetOverride;
            }
            else if (!_blockManagerConfig.BlockPresetDictionary.TryGetValue(blockShape, out blockPreset))
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
            model.GenerateAtom(blockShape, blockPreset, blockInstance, schemaOverride);
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