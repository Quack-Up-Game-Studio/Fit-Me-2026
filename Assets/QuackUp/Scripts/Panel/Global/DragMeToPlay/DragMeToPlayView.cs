using System;
using Cysharp.Threading.Tasks;
using PrimeTween;
using QuackUp.Utils;
using R3;
using UnityEngine;
using VContainer;

namespace FitMe.Panel
{
    public class DragMeToPlayView : MonoBehaviour, IDisposable
    {
        [SerializeField] private SpriteRenderer dragMeToPlaySpriteRenderer;
        [SerializeField] private SpriteRenderer glowSpriteRenderer;
        
        [SerializeField] private TweenSettings<float> alphaTweenSettings;
        
        private DragMeToPlayViewModel _viewModel;
        private Sequence _currentTransition;
        private IDisposable _bindings;
        
        [Inject]
        public void Construct(DragMeToPlayViewModel viewModel)
        {
            _viewModel = viewModel;
            Bind();
        }

        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            _viewModel.TransitionInCommand
                .SubscribeAwait((promise, _) => TransitionIn(promise), AwaitOperation.Drop)
                .AddTo(ref disposableBuilder);
            _viewModel.TransitionOutCommand
                .SubscribeAwait((promise, _) => TransitionOut(promise), AwaitOperation.Drop)
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }
        
        public void Dispose()
        {
            _bindings?.Dispose();
        }

        private void OnDestroy()
        {
            Dispose();
        }

        private async UniTask TransitionIn(Promise<Unit> promise)
        {
            promise.CancellationToken.Register(CancelTransition);
            await Transition(true);
            promise.TrySetResult(Unit.Default);
        }
        
        private async UniTask TransitionOut(Promise<Unit> promise)
        {
            promise.CancellationToken.Register(CancelTransition);
            await Transition(false);
            promise.TrySetResult(Unit.Default);
        }

        private UniTask Transition(bool direction)
        {
            _currentTransition = Sequence.Create()
                .Group(Tween.Alpha(dragMeToPlaySpriteRenderer, alphaTweenSettings.WithDirection(direction))
                .Group(Tween.Alpha(glowSpriteRenderer, alphaTweenSettings.WithDirection(direction))));
            return _currentTransition.ToUniTask();
        }

        private void CancelTransition()
        {
            _currentTransition.Complete();
        }
    }
}