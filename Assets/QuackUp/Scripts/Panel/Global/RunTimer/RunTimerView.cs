using System;
using R3;
using TMPro;
using UnityEngine;
using VContainer;

namespace FitMe.Panel
{
    public class RunTimerView : MonoBehaviour, IDisposable
    {
        [SerializeField] private TMP_Text timerText;
        
        private RunTimerViewModel _viewModel;
        private IDisposable _bindings;
        
        [Inject]
        public void Construct(RunTimerViewModel viewModel)
        {
            _viewModel = viewModel;
            Bind();
        }

        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            _viewModel.ElapsedTime
                .Subscribe(OnElapsedTimeChanged)
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }
        
        public void Dispose()
        {
            _bindings?.Dispose();
        }
        
        private void OnElapsedTimeChanged(TimeSpan elapsedTime)
        {
            timerText.text = elapsedTime.ToString(@"mm\:ss");
        }
    }
}