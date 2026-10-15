using System;
using FitMe.Shared;
using QuackUp.Utils;
using R3;
using UnityEngine;
using VContainer;

namespace FitMe.Grid
{
    public class AtomView : MonoBehaviour, IDisposable
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private SpriteOutlineController spriteOutlineController;
        
        private BlockManagerConfig _blockManagerConfig;
        private AtomViewModel _viewModel;
        private IDisposable _bindings;
        private IDisposable _parentBlockSubscriptions;
        
        public Transform Transform => transform;
        
        [Inject]
        public void Construct(
            BlockManagerConfig blockManagerConfig,
            AtomViewModel viewModel)
        {
            _blockManagerConfig = blockManagerConfig;
            _viewModel = viewModel;
            spriteRenderer.enabled = false;
            Bind();
        }

        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            _viewModel.ParentBlock
                .Where(x => x != null)
                .Subscribe(OnParentBlockModelChanged)
                .AddTo(ref disposableBuilder);
            _viewModel.SetOutlineCommand
                .Subscribe(SetOutline)
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }

        private void OnParentBlockModelChanged(BlockInstance instance)
        {
            _parentBlockSubscriptions?.Dispose();
            var blockConfig = _viewModel.ParentBlock.CurrentValue.Model.Config;
            spriteRenderer.enabled = blockConfig.UseAtomSprite;
            if (!blockConfig.UseAtomSprite) return;
            var disposableBuilder = Disposable.CreateBuilder();
            instance.Model.BlockColor
                .Subscribe(_ => UpdateAtomSpriteColor(instance))
                .AddTo(ref disposableBuilder);
            instance.Model.BlockState
                .Subscribe(_ => UpdateAtomSpriteColor(instance))
                .AddTo(ref disposableBuilder);
            instance.ViewModel.SetSortingLayerCommand
                .Subscribe(OnSetSortingLayer)
                .AddTo(ref disposableBuilder);
            instance.ViewModel.SetSortingOrderCommand
                .Subscribe(OnSetSortingOrder)
                .AddTo(ref disposableBuilder);
            _parentBlockSubscriptions = disposableBuilder.Build();
        }
        
        private void OnSetSortingLayer(int layer)
        {
            spriteRenderer.sortingLayerID = layer;
        }
        
        private void OnSetSortingOrder(int order)
        {
            spriteRenderer.sortingOrder = order;
        }

        private void UpdateAtomSpriteColor(BlockInstance instance)
        {
            if (instance.Model.BlockState.CurrentValue is BlockState.Obstacle)
            {
                spriteRenderer.color = instance.Model.Config.ObstacleAtomSpriteColor;
                return;
            }

            var color = instance.Model.BlockColor.CurrentValue;
            if (!_blockManagerConfig.AtomColorDict.TryGetValue(color, out var spriteColor))
            {
                DebugUtils.LogError($"BlockType.CurrentValue {color} is not defined");
                return;
            }
            spriteRenderer.color = spriteColor;
        }

        private void SetOutline(SpriteOutlineSettings settings)
        {
            spriteOutlineController.UpdateOutline(settings);
        }

        private void OnDestroy()
        {
            Dispose();
        }

        public void Dispose()
        {
            _bindings?.Dispose();
            _parentBlockSubscriptions?.Dispose();
            _bindings = null;
            _parentBlockSubscriptions = null;
        }
    }
}
