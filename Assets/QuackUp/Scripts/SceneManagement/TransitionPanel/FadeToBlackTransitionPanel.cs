using System.Threading;
using Cysharp.Threading.Tasks;
using PrimeTween;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using UnityEngine;

namespace QuackUp.SceneManagement
{
    public class FadeToBlackTransitionPanel : MonoBehaviour, ITransitionable
    {
        [Title("References")]
        [SerializeField] private CanvasGroup canvasGroup;
        
        [Title("Tween")]
        [SerializeField] private TweenSettings<float> tweenSettings;
        
        [Title("Debug")]
        [Button("Transition In")]
        private void TransitionInDebug() => TransitionIn().Forget();
        [Button("Transition Out")]
        private void TransitionOutDebug() => TransitionOut().Forget();
        
        private Sequence _transitionSequence;
        
        public async UniTask TransitionIn(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = tweenSettings.startValue;
            _transitionSequence = Sequence.Create()
                .Group(Tween.Alpha(canvasGroup, tweenSettings.WithDirection(true)));
            var sequence = _transitionSequence;
            using var registration = cancellationToken.Register(() =>
            {
                sequence.Complete();
                canvasGroup.blocksRaycasts = false;
            });
            try { await sequence.ToYieldInstruction().ToUniTask(cancellationToken: cancellationToken); }
            finally { canvasGroup.blocksRaycasts = false; }
        }

        public async UniTask TransitionOut(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            canvasGroup.blocksRaycasts = true;
            _transitionSequence = Sequence.Create()
                .Group(Tween.Alpha(canvasGroup, tweenSettings.WithDirection(false)));
            var sequence = _transitionSequence;
            using var registration = cancellationToken.Register(() =>
            {
                sequence.Complete();
                canvasGroup.blocksRaycasts = false;
            });
            try { await sequence.ToYieldInstruction().ToUniTask(cancellationToken: cancellationToken); }
            finally { canvasGroup.blocksRaycasts = false; }
        }


    }
}