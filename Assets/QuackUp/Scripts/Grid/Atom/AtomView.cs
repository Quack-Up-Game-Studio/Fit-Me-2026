using System;
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
                .Subscribe(OnBlockTypeChanged)
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

        private void OnBlockTypeChanged(BlockColor color)
        {
            if (!_blockManagerConfig.AtomColorDict.TryGetValue(color, out var spriteColor))
            {
                DebugUtils.LogError($"BlockType.CurrentValue {color} is not defined");
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
        }
    }
}
