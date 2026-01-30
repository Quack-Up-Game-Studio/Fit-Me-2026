using System;
using QuackUp.Utils;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

namespace FitMe.Grid
{
    public class CellInstance
    {
        public CellModel Model { get; }
        public CellViewModel ViewModel { get; }
        public GameObject GameObject { get; }

        public CellInstance(CellModel model, CellViewModel viewModel, GameObject gameObject)
        {
            Model = model;
            ViewModel = viewModel;
            GameObject = gameObject;
        }
    }
    public class CellFactory : IFactory<CellInstance>
    {
        private readonly CellConfig _config;
        private readonly CellView _cellViewPrefab;
        private readonly Transform _cellParent;
        
        public const string CellParentKey = "CellParent";
        
        [Inject]
        public CellFactory(
            CellConfig config,
            CellView cellViewPrefab,
            [Key(CellParentKey)] Transform cellParent)
        {
            _config = config;
            _cellViewPrefab = cellViewPrefab;
            _cellParent = cellParent;
        }
        
        public CellInstance Current { get; private set; }
        
        public CellInstance Create()
        {
            return Create(Vector3.zero, Quaternion.identity);
        }
        
        public CellInstance Create(Vector3 position, Quaternion rotation, InstantiateParameters? instantiateParameters = null)
        {
            instantiateParameters ??= new InstantiateParameters
            {
                parent = _cellParent
            };
            var view = Object.Instantiate(_cellViewPrefab, position, rotation, instantiateParameters.Value);
            var model = new CellModel();
            var viewModel = new CellViewModel(model);
            view.Construct(_config, viewModel);
            var cellInstance = new CellInstance(model, viewModel, view.gameObject);
            Current = cellInstance;
            return Current;
        }
    }
}