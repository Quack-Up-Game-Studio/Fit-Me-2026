using System;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FitMe.Panel
{
    public class ResultPanelView : PanelView
    {
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private Button retryButton;
        
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text fitText;

        private ResultPanelViewModel ViewModel => (ResultPanelViewModel)BaseViewModel;
        private IDisposable _bindings;
        
        public override void Construct(IPanelViewModel viewModel)
        {
            base.Construct(viewModel);
            Bind();
        }

        private void Bind()
        {
            var  disposableBuilder = Disposable.CreateBuilder();
            
            mainMenuButton.OnClickAsObservable()
                .Subscribe(_ => OnMainMenu())
                .AddTo(ref disposableBuilder);
            
            retryButton.OnClickAsObservable()
                .Subscribe(_ => OnRetry())
                .AddTo(ref disposableBuilder);
            
            ViewModel.ScoreText
                .Subscribe(text => scoreText.text = text)
                .AddTo(ref disposableBuilder);
            
            ViewModel.FitText
                .Subscribe(text => fitText.text = text)
                .AddTo(ref disposableBuilder);
            
            _bindings = disposableBuilder.Build();
        }
        
        public override void Dispose()
        {
            base.Dispose();
            _bindings?.Dispose();
        }

        private void OnMainMenu()
        {
            ViewModel.ToMainMenuCommand.Execute(Unit.Default);
        }
        
        private void OnRetry()
        {
            ViewModel.ToRetryCommand.Execute(Unit.Default);
        }
    }
}
