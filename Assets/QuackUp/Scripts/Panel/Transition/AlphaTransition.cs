using System;
using System.Threading;
using PrimeTween;
using QuackUp.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace FitMe.Panel
{
    [Serializable]
    public class AlphaTransition : IUITransition
    {
        [SerializeField] private string objectKey = "PanelCanvasGroup";
        [SerializeField] private bool relative;
        [SerializeField] private TweenSettings<float> transitionSettings;

        private Sequence _transitionSequence;
        private Component _transitionObject;

        public void Initialize(ITransitionObjectProvider provider)
        {
            provider.TryGetTransitionObject(objectKey, out _transitionObject);
        }

        public Sequence? Transition(CancellationToken cancellationToken = default, CancelBehavior cancelBehavior = CancelBehavior.Stop)
        {
            if (!_transitionObject) return null;
            TweenSettings<float> settings;
            switch (_transitionObject)
            {
                case CanvasGroup canvasGroup:
                    settings = relative
                        ? transitionSettings.ToRelative(canvasGroup.alpha)
                        : transitionSettings;
                    _transitionSequence = Sequence.Create()
                        .Group(Tween.Alpha(canvasGroup, settings));
                    break;
                case Graphic image:
                    settings = relative
                        ? transitionSettings.ToRelative(image.color.a)
                        : transitionSettings;
                    _transitionSequence = Sequence.Create()
                        .Group(Tween.Alpha(image, settings));
                    break;
                case Shadow shadow:
                    settings = relative
                        ? transitionSettings.ToRelative(shadow.effectColor.a)
                        : transitionSettings;
                    _transitionSequence = Sequence.Create()
                        .Group(Tween.Alpha(shadow, settings));
                    break;
                case SpriteRenderer spriteRenderer:
                    settings = relative
                        ? transitionSettings.ToRelative(spriteRenderer.color.a)
                        : transitionSettings;
                    _transitionSequence = Sequence.Create()
                        .Group(Tween.Alpha(spriteRenderer, settings));
                    break;
                default:
                    Debug.LogWarning("AlphaTransition: Unsupported component type for alpha transition.");
                    break;
            }
            cancellationToken.Register(() => CancelTransition(cancelBehavior));
            return _transitionSequence;
        }

        private void CancelTransition(CancelBehavior cancelBehavior)
        {
            switch (cancelBehavior)
            {
                case CancelBehavior.Stop:
                    _transitionSequence.Stop();
                    break;
                case CancelBehavior.Complete:
                    _transitionSequence.Complete();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(cancelBehavior), cancelBehavior, null);
            }
        }
    }
}