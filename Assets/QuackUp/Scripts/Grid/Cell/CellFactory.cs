using System;
using QuackUp.Utils;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

namespace FitMe.Grid
{
    public class CellFactory : IGameObjectFactory<CellModel>
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
        
        public CellModel Current { get; private set; }
        
        public CellModel Create()
        {
            return Create(Vector3.zero, Quaternion.identity, out _);
        }

        public GameObject CurrentGameObject { get; private set; }
        
        public CellModel Create(Vector3 position, Quaternion rotation, out GameObject gameObject, InstantiateParameters? instantiateParameters = null)
        {
            instantiateParameters ??= new InstantiateParameters
            {
                parent = _cellParent
            };
            var view = Object.Instantiate(_cellViewPrefab, position, rotation, instantiateParameters.Value);
            var model = new CellModel(view);
            var viewModel = new CellViewModel(model);
            view.Construct(_config, viewModel);
            Current = model;
            CurrentGameObject = view.gameObject;
            gameObject = CurrentGameObject;
            return Current;
        }
    }
}