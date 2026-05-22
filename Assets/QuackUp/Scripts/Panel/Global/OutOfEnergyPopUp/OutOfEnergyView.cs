using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using PrimeTween;
using QuackUp.Utils;
using R3;
using Redcode.Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace FitMe.Panel
{
    public class OutOfEnergyView : MonoBehaviour, IDisposable
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform panelTransform;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Button closeButton;
        [SerializeField] private UIButton3D shopButton;
        [SerializeField] private UIButton3D watchAdsButton;
        [SerializeField] private TMP_Text remainingAdCount;
        [SerializeField] private TMP_Text timeUntilNextAdText;
        [SerializeField] private TweenSettings<Vector3> scaleTweenSettings;
        [SerializeField] private TweenSettings<float> backgroundAlphaTweenSettings;

        private Sequence _transitionSequence;
        private IDisposable _bindings;
        private OutOfEnergyViewModel _viewModel;
        
        [Inject]
        public void Construct(OutOfEnergyViewModel viewModel)
        {
            _viewModel = viewModel;
            panelTransform.localScale = scaleTweenSettings.startValue;
            backgroundImage.color = backgroundImage.color.WithA(backgroundAlphaTweenSettings.startValue);
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            Bind();
        }

        private void Bind()
        {
            var builder = Disposable.CreateBuilder();
            _viewModel.TransitionInCommand
                .SubscribeAwait((x, ct) => TransitionIn(x, ct), AwaitOperation.Switch)
                .AddTo(ref builder);
            _viewModel.TransitionOutCommand
                .SubscribeAwait((x, ct) => TransitionOut(x, ct), AwaitOperation.Switch)
                .AddTo(ref builder);
            watchAdsButton.Button.OnClickAsObservable()
                .Subscribe(_ => _viewModel.WatchAdsCommand.Execute(Unit.Default))
                .AddTo(ref builder);
            shopButton.Button.OnClickAsObservable()
                .Subscribe(_ => _viewModel.ToShopCommand.Execute(Unit.Default))
                .AddTo(ref builder);
            closeButton.OnClickAsObservable()
                .SubscribeAwait((_, ct) => TransitionOut(new Promise<Unit>(), ct), AwaitOperation.Switch)
                .AddTo(ref builder);
            _viewModel.RemainingAdCount
                .Subscribe(OnRemainingAdCountChanged)
                .AddTo(ref builder);
            _viewModel.TimeUntilNextWatchAd
                .Subscribe(OnTimeUntilNextAdChanged)
                .AddTo(ref builder);
            _bindings = builder.Build();
        }

        private void OnDestroy()
        {
            Dispose();
        }

        public void Dispose()
        {
            _bindings?.Dispose();
        }

        private void OnRemainingAdCountChanged(int count)
        {
            if (_viewModel.RemainingAdCount.CurrentValue >= _viewModel.MaxAdCount)
            {
                timeUntilNextAdText.text = "Full";
            }
            watchAdsButton.Button.interactable = count > 0;
            watchAdsButton.ApplyTint(count > 0 ? ButtonSelectionState.Normal : ButtonSelectionState.Disabled);
            remainingAdCount.text = $"Remaining: {count}";
        }

        private void OnTimeUntilNextAdChanged(TimeSpan time)
        {
            if (_viewModel.RemainingAdCount.CurrentValue >= _viewModel.MaxAdCount)
            {
                timeUntilNextAdText.text = "Full";
                return;
            }
            //round up to the nearest second for display purposes
            var roundedTime = TimeSpan.FromSeconds(Mathf.Ceil((float)time.TotalSeconds));
            timeUntilNextAdText.text = $"{roundedTime:mm\\:ss}";
        }

        private async UniTask TransitionIn(Promise<Unit> promise, CancellationToken token)
        {
            canvasGroup.blocksRaycasts = true;
            await Transition(true, token);
            canvasGroup.interactable = true;
            promise.TrySetResult(Unit.Default);
        }
        
        private async UniTask TransitionOut(Promise<Unit> promise, CancellationToken token)
        {
            canvasGroup.interactable = false;
            await Transition(false, token);
            canvasGroup.blocksRaycasts = false;
            promise.TrySetResult(Unit.Default);
        }

        private UniTask Transition(bool direction, CancellationToken token)
        {
            token.Register(() => _transitionSequence.Complete());
            _transitionSequence.Complete();
            _transitionSequence = Sequence.Create()
                .Group(Tween.Alpha(backgroundImage, backgroundAlphaTweenSettings.WithDirection(direction)))
                .Group(Tween.Scale(panelTransform, scaleTweenSettings.WithDirection(direction)));
            return _transitionSequence.ToUniTask(cancellationToken: token);
        }
    }
}