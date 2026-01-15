using System;
using QuackUp.Utils;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FitMe.Grid
{
    [Serializable]
    public class CellFactory : IGameObjectFactory<CellModel>
    {
        [SerializeField] private CellView cellViewPrefab;
        [SerializeField] private Transform cellParent;
        
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
                parent = cellParent
            };
            var view = Object.Instantiate(cellViewPrefab, position, rotation, instantiateParameters.Value);
            var model = new CellModel
            {
                TransformData = new TransformData(view.transform)
            };
            var viewModel = new CellViewModel(model);
            view.Construct(viewModel);
            Current = model;
            CurrentGameObject = view.gameObject;
            gameObject = CurrentGameObject;
            return Current;
        }
    }
}