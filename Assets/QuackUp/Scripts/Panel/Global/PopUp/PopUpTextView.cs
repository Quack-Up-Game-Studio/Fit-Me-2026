using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using PrimeTween;
using QuackUp.Utils;
using R3;
using TMPro;
using UnityEngine;
using VContainer;

namespace FitMe.Panel
{
    public class PopUpTextView : MonoBehaviour, ITransitionable, IDisposable
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text popUpText;
        [SerializeField] private ParticleSystem particlesPrefab;
        [SerializeField] private TweenSettings<Vector3> scaleTweenSettings;
        [SerializeField] private TweenSettings<float> fadeTweenSettings;
        [SerializeField] private float stayDuration = 1f;
        
        private Sequence _scaleSequence;
        private ParticleSystem _particlesInstance;
        private PopUpTextViewModel _viewModel;
        private IDisposable _bindings;

        [Inject]
        public void Construct(PopUpTextViewModel viewModel)
        {
            gameObject.SetActive(true);
            transform.localScale = scaleTweenSettings.startValue;
            canvasGroup.alpha = fadeTweenSettings.startValue;
            _viewModel = viewModel;
            Bind();
        }

        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            _viewModel.ShowCommand
                .SubscribeAwait((x, ct) => OnShow(x.text, x.promise, ct), AwaitOperation.Switch)
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
        }

        private async UniTask OnShow(string text, Promise<Unit> promise, CancellationToken cancellationToken)
        {
            cancellationToken.Register(() => promise.TrySetResult(Unit.Default));
            popUpText.text = text;
            await TransitionIn(cancellationToken);
            await UniTask.WaitForSeconds(stayDuration, cancellationToken: cancellationToken);
            await TransitionOut(cancellationToken);
            promise.TrySetResult(Unit.Default);
        }

        public UniTask TransitionIn(CancellationToken cancellationToken = default)
        {
            if (particlesPrefab)
            {
                _particlesInstance = Instantiate(particlesPrefab, transform.position, Quaternion.identity, transform);
                _particlesInstance.Play();
            }
            return Transition(true, cancellationToken);
        }

        public UniTask TransitionOut(CancellationToken cancellationToken = default)
        { 
            return Transition(false, cancellationToken);
        }

        private UniTask Transition(bool direction, CancellationToken cancellationToken = default)
        {
            cancellationToken.Register(() =>
            {
                _scaleSequence.Stop();
                if (_particlesInstance)
                {
                    _particlesInstance.Stop();
                    Destroy(_particlesInstance.gameObject);
                    _particlesInstance = null;
                }
                transform.localScale = scaleTweenSettings.startValue;
                canvasGroup.alpha = fadeTweenSettings.startValue;
            });
            _scaleSequence = Sequence.Create()
                .Group(Tween.Alpha(canvasGroup, fadeTweenSettings.WithDirection(direction)))
                .Group(Tween.Scale(transform, scaleTweenSettings.WithDirection(direction)));
            return _scaleSequence.ToUniTask();
        }
    }
}