using System;
using R3;
using TMPro;
using UnityEngine;
using VContainer;

namespace FitMe.Scene.UI.Score
{
    public class ScoreView : MonoBehaviour, IDisposable
    {
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text fitText;

        private ScoreViewModel _viewModel;
        private IDisposable _bindings;

        [Inject]
        public void Construct(ScoreViewModel viewModel)
        {
            _viewModel = viewModel;
            Bind();
        }

        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            _viewModel.ScoreText
                .Subscribe(text => scoreText.text = text)
                .AddTo(ref disposableBuilder);
            _viewModel.FitText
                .Subscribe(text => fitText.text = text)
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }

        private void OnDestroy()
        {
            Dispose();
        }
        
        public void Dispose()
        {
            _bindings?.Dispose();
            _viewModel?.Dispose();
        }
    }
}
