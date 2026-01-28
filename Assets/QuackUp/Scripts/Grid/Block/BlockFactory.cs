using System;
using System.Collections.Generic;
using QuackUp.Audio;
using QuackUp.Input;
using QuackUp.Utils;
using Sirenix.Serialization;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

namespace FitMe.Grid
{
    public class BlockFactory
    {
        private readonly BlockView _blockViewPrefab;
        private readonly BlockManagerConfig _blockManagerConfig;
        private readonly GridManagerConfig _gridConfig;
        private readonly GridManager _gridManager;
        private readonly AtomFactory _atomFactory;
        private readonly IAudioManager _audioManager;
        private readonly IPointerHandler _pointerHandler;

        [Inject]
        public BlockFactory(
            BlockView blockViewPrefab,
            BlockManagerConfig blockManagerConfig,
            GridManagerConfig gridConfig,
            GridManager gridManager,
            AtomFactory atomFactory,
            IAudioManager audioManager,
            IPointerHandler pointerHandler)
        {
            _blockViewPrefab = blockViewPrefab;
            _blockManagerConfig = blockManagerConfig;
            _gridConfig = gridConfig;
            _gridManager = gridManager;
            _atomFactory = atomFactory;
            _audioManager = audioManager;
            _pointerHandler = pointerHandler;
        }

        public BlockModel Current { get; private set; }

        public GameObject CurrentGameObject { get; private set; }

        public BlockModel Create(BlockShape blockShape, Vector3 position, Quaternion rotation, out GameObject gameObject,
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
            model.BlockView = view;
            model.GenerateAtom(blockShape, blockPreset);
            var viewModel = new BlockViewModel(model);
            var controller = new BlockController(
                _blockManagerConfig,
                _gridManager,
                model,
                _audioManager,
                _pointerHandler);
            model.BlockController = controller;
            view.Construct(blockConfig, 
                _blockManagerConfig, 
                _gridConfig, 
                controller, 
                viewModel);
            model.SetSortingLayerCommand.Execute(_blockManagerConfig.SpawnSortingLayer);
            Current = model;
            CurrentGameObject = view.gameObject;
            gameObject = CurrentGameObject;
            return Current;
        }
    }
}