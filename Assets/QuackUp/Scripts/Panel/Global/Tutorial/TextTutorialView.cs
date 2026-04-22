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
        [SerializeField] private Image character;
        [SerializeField] private TMP_Text tutorialText;
        [SerializeField] private Image tutorialImage;
        [SerializeField] private RectTransform panelRect;
        [SerializeField] private RectTransform carveWindowRect;
        [SerializeField] private Button nextButton;
        [SerializeField] private TMP_Text touchAnywhereText;
        [SerializeField] private TweenSettings<float> backgroundAlphaTweenSettings;
        [SerializeField] private TweenSettings<Vector3> scaleTweenSettings;
        [SerializeField] private TweenSettings insetTweenSettings;
        [SerializeField] private TweenSettings carveWindowTweenSettings;
        [SerializeField] private TweenSettings characterSizeTweenSettings;
        [SerializeField] private TweenSettings characterRotationTweenSettings;
        [SerializeField] private TweenSettings characterPositionTweenSettings;
        
        private TextTutorialViewModel _viewModel;
        private CancellationTokenSource _cancellationTokenSource = new();
        private Sequence _backgroundSequence;
        private Sequence _showSequence;
        private Sequence _carveWindowSequence;
        private Sequence _panelInsetSequence;
        private Sequence _characterSequence;
        private IDisposable _bindings;

        [Inject]
        public void Construct(TextTutorialViewModel viewModel)
        {
            _viewModel = viewModel;
            panelRect.localScale = Vector3.zero;
            carveWindowRect.offsetMax = Vector2.zero;
            carveWindowRect.offsetMin = Vector2.zero;
            carveWindowRect.gameObject.SetActive(false);
            tutorialImage.gameObject.SetActive(false);
            tutorialText.text = string.Empty;
            nextButton.gameObject.SetActive(false);
            touchAnywhereText.gameObject.SetActive(false);
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
            nextButton.gameObject.SetActive(_viewModel.TutorialData.HasNextButton);
            carveWindowRect.gameObject.SetActive(_viewModel.TutorialData.HasCarveWindow);
            touchAnywhereText.gameObject.SetActive(_viewModel.TutorialData.HasNextButton);
            await UniTask.WhenAll(
                TweenPanelInset(_cancellationTokenSource.Token), 
                TweenCharacter(_cancellationTokenSource.Token),
                TweenCarveWindowInset(_cancellationTokenSource.Token));
            tutorialText.text = _viewModel.TutorialData.Text;
            tutorialImage.sprite = _viewModel.TutorialData.Image;
            tutorialImage.gameObject.SetActive(_viewModel.TutorialData.Image);
            nextButton.gameObject.SetActive(_viewModel.TutorialData.HasNextButton);
            promise.TrySetResult(true);
        }
        
        private async UniTask Transition(bool direction, CancellationToken cancellationToken)
        {
            if (direction && 
                _viewModel.PreviousVisibilityState is VisibilityState.Hidden)
            {
                var inset = _viewModel.TutorialData.PanelInset;
                if (inset != null)
                {
                    panelRect.offsetMax = inset.Value.OffsetMax;
                    panelRect.offsetMin = inset.Value.OffsetMin;
                }
                var characterRectTransform = (RectTransform)character.transform;
                var characterSize = _viewModel.TutorialData.CharacterSize;
                if (characterSize != null)
                {
                    characterRectTransform.sizeDelta = characterSize.Value;
                }
                var characterPosition = _viewModel.TutorialData.CharacterPosition;
                if (characterPosition != null)
                {
                    characterRectTransform.anchoredPosition = characterPosition.Value;
                }
                var characterRotation = _viewModel.TutorialData.CharacterRotation;
                if (characterRotation != null)
                {
                    characterRectTransform.localEulerAngles = characterRotation.Value;
                }
                carveWindowRect.offsetMax = Vector2.zero;
                carveWindowRect.offsetMin = Vector2.zero;
            }
            carveWindowRect.gameObject.SetActive(_viewModel.TutorialData.HasCarveWindow);
            touchAnywhereText.gameObject.SetActive(_viewModel.TutorialData.HasNextButton);
            cancellationToken.Register(() =>
            {
                _showSequence.Stop();
            });
            _showSequence = Sequence.Create()
                .Group(Tween.Scale(panelRect.transform, scaleTweenSettings.WithDirection(direction)))
                .Group(Tween.Scale(character.transform, scaleTweenSettings.WithDirection(direction)));
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
        
        private async UniTask TweenPanelInset(CancellationToken cancellationToken)
        {
            if (_viewModel.TutorialData.PanelInset == null) return;
            cancellationToken.Register(() => _panelInsetSequence.Complete());
            var inset = _viewModel.TutorialData.PanelInset;
            _panelInsetSequence = Sequence.Create()
                .Group(Tween.UIOffsetMax(panelRect, new(inset.Value.OffsetMax, insetTweenSettings)))
                .Group(Tween.UIOffsetMin(panelRect, new(inset.Value.OffsetMin, insetTweenSettings)));
            await _panelInsetSequence.ToUniTask();
        }

        private async UniTask TweenCarveWindowInset(CancellationToken cancellationToken)
        {
            if (!_viewModel.TutorialData.HasCarveWindow || _viewModel.TutorialData.CarveWindowInset == null) return;
            cancellationToken.Register(() => _carveWindowSequence.Complete());
            var inset = _viewModel.TutorialData.CarveWindowInset;
            _carveWindowSequence = Sequence.Create()
                .Group(Tween.UIOffsetMax(carveWindowRect, new(inset.Value.OffsetMax, carveWindowTweenSettings)))
                .Group(Tween.UIOffsetMin(carveWindowRect, new(inset.Value.OffsetMin, carveWindowTweenSettings)));
            await _carveWindowSequence.ToUniTask();
        }

        private async UniTask TweenCharacter(CancellationToken cancellationToken)
        {
            var size = _viewModel.TutorialData.CharacterSize;
            var position = _viewModel.TutorialData.CharacterPosition;
            var rotation = _viewModel.TutorialData.CharacterRotation;
            if (size == null && 
                position == null &&
                rotation == null) return;
            var sequence = Sequence.Create();
            cancellationToken.Register(() => _characterSequence.Complete());
            var rectTransform = (RectTransform)character.transform;
            if (size != null)
            {
                _ = sequence.Group(Tween.UISizeDelta(rectTransform, new TweenSettings<Vector2>(size.Value, characterSizeTweenSettings)));
            }
            if (position != null)
            {
                _ = sequence.Group(Tween.UIAnchoredPosition(rectTransform, new TweenSettings<Vector2>(position.Value, characterPositionTweenSettings)));
            }
            if (rotation != null)
            {
                _ = sequence.Group(Tween.Rotation(rectTransform, new TweenSettings<Vector3>(rotation.Value, characterRotationTweenSettings)));
            }
            _characterSequence = sequence;
            await sequence.ToUniTask();
        }
    }
}