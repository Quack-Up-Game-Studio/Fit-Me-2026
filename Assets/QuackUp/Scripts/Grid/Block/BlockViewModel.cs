using System;
using System.Collections.Generic;
using QuackUp.Utils;
using R3;
using UnityEngine;
using VContainer;

namespace FitMe.Grid
{
    #region Command Data
    public struct ExplodeCommandData
    {
        public Promise<Unit> Promise;
        public FitType FitType;
        public bool Destroy;
        
        public ExplodeCommandData(Promise<Unit> promise, FitType fitType, bool destroy)
        {
            Promise = promise;
            FitType = fitType;
            Destroy = destroy;
        }
    }

    public struct ScaleInCommandData
    {
        public Promise<Unit> Promise;
        public Vector3 Scale;
        
        public ScaleInCommandData(Promise<Unit> promise, Vector3 scale)
        {
            Promise = promise;
            Scale = scale;
        }
    }

    public struct RotateCommandData
    {
        public Promise<Unit> Promise;
        public Quaternion Rotation;
        
        public RotateCommandData(Promise<Unit> promise, Quaternion rotation)
        {
            Promise = promise;
            Rotation = rotation;
        }
    }
    #endregion
    
    public class BlockViewModel : IDisposable
    {
        public ReactiveProperty<BlockInteractionState> BlockInteractionState { get; private set; } = new(Grid.BlockInteractionState.PlacedOnSpawn);
        public ReadOnlyReactiveProperty<BlockColor> BlockColor => _model.BlockColor;
        public ReactiveCommand<int> SetSortingLayerCommand { get; private set; } = new();
        public ReactiveCommand<int> SetSortingOrderCommand { get; private set; } = new();
        public ReactiveCommand<ExplodeCommandData> ExplodeCommand { get; private set; } = new();
        public ReactiveCommand<ScaleInCommandData> ScaleInCommand { get; private set; } = new();
        public ReactiveCommand<RotateCommandData> RotateCommand { get; private set; } = new();
        public ReactiveCommand<Unit> DestroyCommand { get; private set; } = new();
        
        private readonly BlockModel _model;
        private IDisposable _bindings;

        [Inject]
        public BlockViewModel(BlockModel model)
        {
            _model = model;
            Bind();
        }

        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            _bindings = disposableBuilder.Build();
        }
        
        public void Dispose()
        {
            _bindings?.Dispose();
        }
    }
}