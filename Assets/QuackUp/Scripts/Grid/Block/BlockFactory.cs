using System;
using QuackUp.Utils;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

namespace FitMe.Grid
{
    [Serializable]
    public class BlockFactory : IGameObjectFactory<BlockModel>
    {
        [SerializeField] private BlockView blockViewPrefab;
        [SerializeField] private Transform blocksParent;

        private readonly BlockConfig _blockConfig;
        private readonly GridManagerConfig _gridConfig;
        private readonly AtomFactory _atomFactory;

        [Inject]
        public BlockFactory(
            BlockConfig blockConfig,
            GridManagerConfig gridConfig,
            AtomFactory atomFactory)
        {
            _blockConfig = blockConfig;
            _gridConfig = gridConfig;
            _atomFactory = atomFactory;
        }
        
        public BlockModel Current { get; private set; }
        public BlockModel Create()
        {
            return Create(Vector3.zero, Quaternion.identity, out _);
        }

        public GameObject CurrentGameObject { get; private set; }

        public BlockModel Create(Vector3 position, Quaternion rotation, out GameObject gameObject,
            InstantiateParameters? instantiateParameters = null)
        {
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
            var viewModel = new BlockViewModel(model);
            view.Construct(_blockConfig, _gridConfig, viewModel);
            Current = model;
            CurrentGameObject = view.gameObject;
            gameObject = CurrentGameObject;
            return Current;
        }
    }
}