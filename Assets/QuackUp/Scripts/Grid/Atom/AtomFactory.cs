using System;
using QuackUp.Utils;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

namespace FitMe.Grid
{
    public class AtomFactory : IGameObjectFactory<AtomModel>
    {
        private readonly AtomView _atomViewPrefab;
        private readonly BlockConfig _blockConfig;
        
        [Inject]
        public AtomFactory(
            AtomView atomViewPrefab,
            BlockConfig blockConfig)
        {
            _atomViewPrefab = atomViewPrefab;
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
                //parent = atomParent
                worldSpace = false
            };
            var view = Object.Instantiate(_atomViewPrefab, position, rotation,
                instantiateParameters.Value);
            var model = new AtomModel(view);
            var viewModel = new AtomViewModel(model);
            view.Construct(_blockConfig, viewModel);
            Current = model;
            CurrentGameObject = view.gameObject;
            gameObject = CurrentGameObject;
            return Current;
        }
    }
}