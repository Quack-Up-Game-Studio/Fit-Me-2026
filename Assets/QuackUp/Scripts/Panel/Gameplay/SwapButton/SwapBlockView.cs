using System;
using R3;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace FitMe.Panel
{
    public class SwapBlockView : MonoBehaviour, IDisposable
    {
        [SerializeField] private Button swapButton;
        
        private SwapBlockViewModel _viewModel;
        private IDisposable _bindings;
        
        [Inject]
        public void Construct(SwapBlockViewModel viewModel)
        {
            _viewModel = viewModel;
            Bind();
        }
        
        private void Bind()
        {
            var builder = Disposable.CreateBuilder();
            swapButton.OnClickAsObservable()
                .Subscribe(_ => _viewModel.SwapCommand.Execute(Unit.Default))
                .AddTo(ref builder);
            _bindings = builder.Build();
        }
        
        public void Dispose()
        {
            _bindings?.Dispose();
        }
    }
}