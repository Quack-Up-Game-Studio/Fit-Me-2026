using System.Threading;
using Cysharp.Threading.Tasks;
using QuackUp.Utils;
using PrimeTween;
using Sirenix.OdinInspector;
using UnityEngine;

namespace QuackUp.Core
{
    public class LoadSceneTransitionView : MonoBehaviour, ITransitionable
    {
        [Title("References")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform circleTransform;
        [Title("Tween")]
        [SerializeField] private TweenSettings<Vector2> tweenSettings;
        
        [Title("Debug")]
        [Button("Transition In")]
        private void TransitionInDebug() => TransitionIn().Forget();
        [Button("Transition Out")]
        private void TransitionOutDebug() => TransitionOut().Forget();
        
        private Sequence _transitionSequence;
        
        public async UniTask TransitionIn(CancellationToken cancellationToken = default)
        {
            canvasGroup.blocksRaycasts = true;
            cancellationToken.Register(CancelTransition);
            circleTransform.sizeDelta = tweenSettings.startValue;
            _transitionSequence = Sequence.Create()
                .Group(
                    Tween.UISizeDelta(circleTransform, tweenSettings.WithDirection(true)));
            await _transitionSequence.ToYieldInstruction().ToUniTask(cancellationToken: cancellationToken);
            canvasGroup.blocksRaycasts = false;
        }

        public async UniTask TransitionOut(CancellationToken cancellationToken = default)
        {
            DebugUtils.Log("Transition Out");
            canvasGroup.blocksRaycasts = true;
            cancellationToken.Register(CancelTransition);
            _transitionSequence = Sequence.Create()
                .Group(
                    Tween.UISizeDelta(circleTransform, tweenSettings.WithDirection(false)));
            await _transitionSequence.ToYieldInstruction().ToUniTask(cancellationToken: cancellationToken);
            canvasGroup.blocksRaycasts = false;
        }

        private void CancelTransition()
        {
            _transitionSequence.Complete();
            canvasGroup.blocksRaycasts = false;
        }
    }
}