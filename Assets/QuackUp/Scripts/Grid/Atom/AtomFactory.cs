using System;
using QuackUp.Utils;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

namespace FitMe.Grid
{
    public class AtomInstance
    {
        public AtomModel Model { get; }
        public AtomViewModel ViewModel { get; }
        public GameObject GameObject { get; }

        public AtomInstance(AtomModel model, AtomViewModel viewModel, GameObject gameObject)
        {
            Model = model;
            ViewModel = viewModel;
            GameObject = gameObject;
        }
    }
    public class AtomFactory : IFactory<AtomInstance>
    {
        private readonly AtomView _atomViewPrefab;
        private readonly BlockManagerConfig _config;
        
        [Inject]
        public AtomFactory(
            BlockManagerConfig blockManagerConfig,
            AtomView atomViewPrefab)
        {
            _atomViewPrefab = atomViewPrefab;
            _config = blockManagerConfig;
        }
        
        public AtomInstance Current { get; private set; }
        public AtomInstance Create()
        {
            return Create(Vector3.zero, Quaternion.identity);
        }

        public AtomInstance Create(Vector3 position, Quaternion rotation, InstantiateParameters? instantiateParameters = null)
        {
            instantiateParameters ??= new InstantiateParameters
            {
                //parent = atomParent
                worldSpace = false
            };
            var view = Object.Instantiate(_atomViewPrefab, position, rotation,
                instantiateParameters.Value);
            var model = new AtomModel();
            var viewModel = new AtomViewModel(model);
            view.Construct(_config, viewModel);
            Current = new AtomInstance(model, viewModel, view.gameObject);
            return Current;
        }
    }
}