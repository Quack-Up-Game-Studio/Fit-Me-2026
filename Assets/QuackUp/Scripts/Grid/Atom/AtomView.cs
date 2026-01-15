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
        private AtomViewModel _viewModel;
        private IDisposable _bindings;
        
        [Inject]
        public void Construct(AtomViewModel viewModel)
        {
            _viewModel = viewModel;
            Bind();
        }

        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            _viewModel.TransformData.OnChanged
                .Subscribe(OnTransformDataChanged)
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }
        
        private void OnTransformDataChanged(TransformData transformData)
        {
            transform.position = transformData.Position.Value;
            transform.localPosition = transformData.LocalPosition.Value;
            transform.rotation = transformData.Rotation.Value;
            transform.localRotation = transformData.LocalRotation.Value;
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
