using System;
using System.Collections.Generic;
using QuackUp.Utils;
using Sirenix.Serialization;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

namespace FitMe.Grid
{
    [Serializable]
    public class BlockFactory
    {
        //[SerializeField] private BlockView blockViewPrefab;
        [field: OdinSerialize] public Dictionary<string, BlockView> BlockViewDictionary { get; private set; } = new();
        [SerializeField] private Transform blocksParent;

        private readonly BlockManagerConfig _blockManagerConfig;
        private readonly BlockConfig _blockConfig;
        private readonly GridManagerConfig _gridConfig;
        private readonly AtomFactory _atomFactory;

        [Inject]
        public BlockFactory(
            BlockManagerConfig blockManagerConfig,
            BlockConfig blockConfig,
            GridManagerConfig gridConfig,
            AtomFactory atomFactory)
        {
            _blockManagerConfig = blockManagerConfig;
            _blockConfig = blockConfig;
            _gridConfig = gridConfig;
            _atomFactory = atomFactory;
        }

        public BlockModel Current { get; private set; }

        public GameObject CurrentGameObject { get; private set; }

        public BlockModel Create(string blockFace, Vector3 position, Quaternion rotation, out GameObject gameObject,
            InstantiateParameters? instantiateParameters = null)
        {
            if (!BlockViewDictionary.TryGetValue(blockFace, out var blockViewPrefab))
            {
                throw new ArgumentException($"Block face '{blockFace}' not found in BlockViewDictionary.");
            }
            if (!_blockManagerConfig.BlockPresetDictionary.TryGetValue(blockFace, out var blockPreset))
            {
                throw new ArgumentException($"Block face '{blockFace}' not found in BlockPresetDictionary.");
            }
            instantiateParameters ??= new InstantiateParameters
            {
                parent = blocksParent
            };
            var view = Object.Instantiate(blockViewPrefab, position, rotation,
                instantiateParameters.Value);
            var model = new BlockModel(_blockConfig, _atomFactory, view)
            {
                TransformData = new TransformData(view.transform)
            };
            model.GenerateAtom(blockFace, blockPreset);
            var viewModel = new BlockViewModel(model);
            view.Construct(_blockConfig, _gridConfig, viewModel);
            Current = model;
            CurrentGameObject = view.gameObject;
            gameObject = CurrentGameObject;
            return Current;
        }
        
        
    }
}