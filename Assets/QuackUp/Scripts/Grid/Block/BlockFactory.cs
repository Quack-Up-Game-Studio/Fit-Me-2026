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
        private readonly Dictionary<string, BlockView> _blockViewDictionary;
        private readonly BlockManagerConfig _blockManagerConfig;
        private readonly BlockConfig _blockConfig;
        private readonly GridManagerConfig _gridConfig;
        private readonly GridManager _gridManager;
        private readonly AtomFactory _atomFactory;
        private readonly IAudioManager _audioManager;
        private readonly IPointerHandler _pointerHandler;

        [Inject]
        public BlockFactory(
            Dictionary<string, BlockView> blockViewDictionary,
            BlockManagerConfig blockManagerConfig,
            BlockConfig blockConfig,
            GridManagerConfig gridConfig,
            GridManager gridManager,
            AtomFactory atomFactory,
            IAudioManager audioManager,
            IPointerHandler pointerHandler)
        {
            _blockViewDictionary = blockViewDictionary;
            _blockManagerConfig = blockManagerConfig;
            _blockConfig = blockConfig;
            _gridConfig = gridConfig;
            _gridManager = gridManager;
            _atomFactory = atomFactory;
            _audioManager = audioManager;
            _pointerHandler = pointerHandler;
        }

        public BlockModel Current { get; private set; }

        public GameObject CurrentGameObject { get; private set; }

        public BlockModel Create(string blockFace, Vector3 position, Quaternion rotation, out GameObject gameObject,
            InstantiateParameters? instantiateParameters = null)
        {
            if (!_blockViewDictionary.TryGetValue(blockFace, out var blockViewPrefab))
            {
                throw new ArgumentException($"Block face '{blockFace}' not found in BlockViewDictionary.");
            }
            if (!_blockManagerConfig.BlockPresetDictionary.TryGetValue(blockFace, out var blockPreset))
            {
                throw new ArgumentException($"Block face '{blockFace}' not found in BlockPresetDictionary.");
            }
            instantiateParameters ??= new InstantiateParameters
            {
                //parent = blocksParent
            };
            var view = Object.Instantiate(blockViewPrefab, position, rotation,
                instantiateParameters.Value);
            var model = new BlockModel(_blockConfig, _atomFactory, view);
            model.GenerateAtom(blockFace, blockPreset);
            var viewModel = new BlockViewModel(model);
            var controller = new BlockController(
                _blockConfig,
                _gridManager,
                model,
                _audioManager,
                _pointerHandler);
            view.Construct(_blockConfig, _gridConfig, controller, viewModel);
            Current = model;
            CurrentGameObject = view.gameObject;
            gameObject = CurrentGameObject;
            return Current;
        }
    }
}