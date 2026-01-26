using System;
using QuackUp.Utils;
using R3;
using UnityEngine;
using VContainer;

namespace FitMe.Grid
{
    public interface IAtomView : ITransformProvider
    {
        void SetOutline(SpriteOutlineSettings settings);
    }
    
    public class AtomView : MonoBehaviour, IAtomView, IDisposable
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private SpriteOutlineController spriteOutlineController;
        
        private BlockConfig _config;
        private AtomViewModel _viewModel;
        private IDisposable _bindings;
        private IDisposable _parentBlockSubscriptions;
        
        public Transform Transform => transform;
        
        [Inject]
        public void Construct(
            BlockConfig config,
            AtomViewModel viewModel)
        {
            _config = config;
            _viewModel = viewModel;
            spriteRenderer.enabled = false;
            Bind();
        }

        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            _viewModel.ParentBlockModel
                .Where(x => x != null)
                .Subscribe(OnParentBlockModelChanged)
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }

        private void OnParentBlockModelChanged(BlockModel model)
        {
            _parentBlockSubscriptions?.Dispose();
            if (!_config.UseAtomSprite) return;
            spriteRenderer.enabled = _config.UseAtomSprite;
            var disposableBuilder = Disposable.CreateBuilder();
            model.BlockType
                .Subscribe(OnBlockTypeChanged)
                .AddTo(ref disposableBuilder);
            model.SetSortingLayerCommand
                .Subscribe(OnSetSortingLayer)
                .AddTo(ref disposableBuilder);
            model.SetSortingOrderCommand
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
            if (!_config.AtomColorDict.TryGetValue(color, out var spriteColor))
            {
                DebugUtils.LogError($"BlockType.CurrentValue {color} is not defined");
            }
            spriteRenderer.color = spriteColor;
        }

        public void SetOutline(SpriteOutlineSettings settings)
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
