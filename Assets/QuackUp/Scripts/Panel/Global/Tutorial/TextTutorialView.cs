using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using PrimeTween;
using QuackUp.Utils;
using R3;
using Redcode.Extensions;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace FitMe.Panel.Tutorial
{
    public class TextTutorialView : MonoBehaviour, IDisposable
    {
        [SerializeField, Required] private CanvasGroup canvasGroup;
        [SerializeField, Required] private Image background;
        [SerializeField] private TMP_Text tutorialText;
        [SerializeField] private Image tutorialImage;
        [SerializeField] private RectTransform panelRect;
        [SerializeField] private Button nextButton;
        [SerializeField] private TweenSettings<float> backgroundAlphaTweenSettings;
        [SerializeField] private TweenSettings<Vector3> scaleTweenSettings;
        [SerializeField] private TweenSettings insetTweenSettings;
        
        private TextTutorialViewModel _viewModel;
        private CancellationTokenSource _cancellationTokenSource = new();
        private Sequence _backgroundSequence;
        private Sequence _showSequence;
        private Sequence _panelInsetSequence;
        private IDisposable _bindings;

        [Inject]
        public void Construct(TextTutorialViewModel viewModel)
        {
            _viewModel = viewModel;
            panelRect.localScale = Vector3.zero;
            tutorialImage.gameObject.SetActive(false);
            tutorialText.text = string.Empty;
            nextButton.gameObject.SetActive(false);
            Bind();
        }

        private void Bind()
        {
            var disposableBuilder = new DisposableBuilder();
            nextButton.OnClickAsObservable()
                .Subscribe(_ => OnNextButtonClicked())
                .AddTo(ref disposableBuilder);
            _viewModel.VisibilityState
                .Subscribe(OnVisibilityStateChanged)
                .AddTo(ref disposableBuilder);
            _viewModel.UIInputState
                .Subscribe(OnInputStateChanged)
                .AddTo(ref disposableBuilder);
            _viewModel.OnTransition
                .SubscribeAwait((x, _) => OnTransition(x.Direction, x.Promise), AwaitOperation.Switch)
                .AddTo(ref disposableBuilder);
            _viewModel.OnDisplayData
                .SubscribeAwait((x, _) => OnDisplayData(x), AwaitOperation.Switch)
                .AddTo(ref disposableBuilder);
            _viewModel.OnBlockInput
                .SubscribeAwait((x, _) => OnBlockInputChanged(x.BlockInput, x.Promise), AwaitOperation.Switch)
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }

        public void Dispose()
        {
            _bindings.Dispose();
        }

        private void OnDestroy()
        {
            Dispose();
        }
        
        private async UniTask OnBlockInputChanged(bool blockInput, Promise<bool> promise)
        {
            _cancellationTokenSource.Token.Register(() =>
            {
                _backgroundSequence.Complete();
                promise.TrySetResult(false);
            });
            if (blockInput)
            {
                background.raycastTarget = true;
            }
            _backgroundSequence = Sequence.Create()
                .Group(Tween.Alpha(background, backgroundAlphaTweenSettings.WithDirection(blockInput)));
            await _backgroundSequence.ToUniTask();
            if (!blockInput) background.raycastTarget = false;
            promise.TrySetResult(true);
        }
        
        private void OnInputStateChanged(InputState state)
        {
            canvasGroup.interactable = state is InputState.Active;
        }
        
        private void OnVisibilityStateChanged(VisibilityState state)
        {
            canvasGroup.alpha = state is VisibilityState.Visible ? 1 : 0;
        }

        private async UniTask OnTransition(bool direction, Promise<bool> promise)
        {
            ResetTokenSource(promise);
            await Transition(direction, _cancellationTokenSource.Token);
            promise.TrySetResult(true);
        }

        private async UniTask OnDisplayData(Promise<bool> promise)
        {
            if (!_viewModel.HasNextButton) nextButton.gameObject.SetActive(false);
            await SetInset(_cancellationTokenSource.Token);
            tutorialText.text = _viewModel.TutorialText;
            tutorialImage.sprite = _viewModel.TutorialImage;
            tutorialImage.gameObject.SetActive(_viewModel.TutorialImage);
            nextButton.gameObject.SetActive(_viewModel.HasNextButton);
            promise.TrySetResult(true);
        }
        
        private async UniTask Transition(bool direction, CancellationToken cancellationToken)
        {
            cancellationToken.Register(() =>
            {
                _showSequence.Stop();
            });
            _showSequence = Sequence.Create()
                .Group(Tween.Scale(panelRect.transform, scaleTweenSettings.WithDirection(direction)));
            await _showSequence.ToUniTask();
        }

        private void ResetTokenSource(Promise<bool> promise)
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
            _cancellationTokenSource.Token.Register(() => promise.TrySetResult(false));
        }
        
        private void OnNextButtonClicked()
        {
            _cancellationTokenSource.Cancel();
            _viewModel.OnNextCommand.Execute(Unit.Default);
        }
        
        private async UniTask SetInset(CancellationToken cancellationToken)
        {
            if (_viewModel.UsePreviousSize) return;
            cancellationToken.Register(() => _panelInsetSequence.Complete());
            var inset = _viewModel.PanelInset;
            _panelInsetSequence = Sequence.Create()
                .Group(Tween.UIOffsetMax(panelRect, new(inset.OffsetMax, insetTweenSettings)))
                .Group(Tween.UIOffsetMin(panelRect, new(inset.OffsetMin, insetTweenSettings)));
            await _panelInsetSequence.ToUniTask();
        }
    }
}