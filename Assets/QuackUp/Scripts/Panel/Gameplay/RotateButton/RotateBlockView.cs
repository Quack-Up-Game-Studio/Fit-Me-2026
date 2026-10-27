using System;
using R3;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace FitMe.Panel
{
    public sealed class RotateBlockView : MonoBehaviour, IDisposable
    {
        [SerializeField] private Button rotateButton;

        private RotateBlockViewModel _viewModel;
        private IDisposable _bindings;

        [Inject]
        public void Construct(RotateBlockViewModel viewModel)
        {
            _viewModel = viewModel;
            var builder = Disposable.CreateBuilder();
            rotateButton.interactable = _viewModel.CanRotate.CurrentValue;
            _viewModel.CanRotate
                .Subscribe(canRotate => rotateButton.interactable = canRotate)
                .AddTo(ref builder);
            rotateButton.OnClickAsObservable()
                .Subscribe(_ => _viewModel.RotateCommand.Execute(Unit.Default))
                .AddTo(ref builder);
            _bindings = builder.Build();
        }

        public void Dispose()
        {
            _bindings?.Dispose();
        }
    }
}
