using System;
using PrimeTween;
using R3;
using TMPro;
using UnityEngine;
using VContainer;

namespace FitMe.Scene.UI.Score
{
    [Serializable]
    public class PopUpScoreView : MonoBehaviour
    {
        [SerializeField] private TMP_Text popUpScoreText;

        [Header("Animation Settings")] 
        [SerializeField] private float duration = 1.0f;
        [SerializeField] private Ease easeType = Ease.OutQuad;

        private Vector3 _endPoint;
        private PopUpScoreViewModel _viewModel;
        private IDisposable _bindings;

        [Inject]
        public void Construct(PopUpScoreViewModel viewModel, 
            Vector3 endPoint)
        {
            _viewModel = viewModel;
            _endPoint = endPoint;
            Bind();

            PlayPopUpAnimation();
        }

        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            _viewModel.PopUpScoreText
                .Subscribe(text => popUpScoreText.text = text)
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

        #region Animation Methods

        private void PlayPopUpAnimation()
        {
            Vector3 endPosition = _endPoint;
            Tween.Position(transform, endPosition, duration, easeType);
            Tween.Alpha(popUpScoreText, 0f, duration, easeType);
            Tween.Delay(duration, () => 
            {
                Destroy(gameObject); 
            });
        }

        #endregion
    }
}
