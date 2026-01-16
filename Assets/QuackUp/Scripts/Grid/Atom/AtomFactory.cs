using System;
using QuackUp.Utils;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

namespace FitMe.Grid
{
    [Serializable]
    public class AtomFactory : IGameObjectFactory<AtomModel>
    {
        [SerializeField] private AtomView atomViewPrefab;
        [SerializeField] private Transform atomParent;
        
        private readonly BlockConfig _blockConfig;
        
        [Inject]
        public AtomFactory(BlockConfig blockConfig)
        {
            _blockConfig = blockConfig;
        }
        
        public AtomModel Current { get; private set; }
        public AtomModel Create()
        {
            return Create(Vector3.zero, Quaternion.identity, out _);
        }

        public GameObject CurrentGameObject { get; private set; }

        public AtomModel Create(Vector3 position, Quaternion rotation, out GameObject gameObject,
            InstantiateParameters? instantiateParameters = null)
        {
            instantiateParameters ??= new InstantiateParameters
            {
                parent = atomParent
            };
            var view = Object.Instantiate(atomViewPrefab, position, rotation,
                instantiateParameters.Value);
            var model = new AtomModel
            {
                TransformData = new TransformData(view.transform)
            };
            var viewModel = new AtomViewModel(model);
            view.Construct(_blockConfig, viewModel);
            Current = model;
            CurrentGameObject = view.gameObject;
            gameObject = CurrentGameObject;
            return Current;
        }
    }
}