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
        
        private BlockConfig _config;
        private AtomViewModel _viewModel;
        private IDisposable _bindings;
        private IDisposable _parentBlockSubscriptions;
        
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
            _viewModel.TransformData.OnChanged
                .Subscribe(OnTransformDataChanged)
                .AddTo(ref disposableBuilder);
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
            _parentBlockSubscriptions = disposableBuilder.Build();
        }

        private void OnBlockTypeChanged(BlockTypes type)
        {
            if (!_config.AtomColorDict.TryGetValue(type, out var color))
            {
                Debug.LogError($"BlockType.CurrentValue {type} is not defined");
            }
            spriteRenderer.color = color;
        }
        
        private void OnTransformDataChanged(TransformData transformData)
        {
            transform.position = transformData.Position.Value;
            transform.rotation = transformData.Rotation.Value;
            transform.localScale = transformData.LocalScale.Value;
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
