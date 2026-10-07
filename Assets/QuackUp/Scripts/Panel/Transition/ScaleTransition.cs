using System;
using System.Threading;
using PrimeTween;
using QuackUp.Utils;
using UnityEngine;

namespace FitMe.Panel
{
    [Serializable]
    public class ScaleTransition : IUITransition
    {
        [SerializeField] private string rectTransformKey = "PanelRectTransform";
        [SerializeField] private bool relative;
        [SerializeField] private TweenSettings<Vector3> transitionSettings;

        private RectTransform _transitionObject;

        public void Initialize(ITransitionObjectProvider provider)
        {
            provider.TryGetTransitionObject(rectTransformKey, out _transitionObject);
        }

        public Sequence? Transition(CancellationToken cancellationToken = default, CancelBehavior cancelBehavior = CancelBehavior.Stop)
        {
            if (!_transitionObject) return null;
            var settings = relative
                ? transitionSettings.ToRelative(_transitionObject.localScale)
                : transitionSettings;
            var sequence = Sequence.Create()
                .Group(Tween.Scale(_transitionObject, settings));
            CancellationTokenRegistration registration = default;
            registration = cancellationToken.Register(() =>
            {
                try { CancelTransition(sequence, cancelBehavior); }
                finally { registration.Dispose(); }
            });
            if (sequence.isAlive)
            {
                sequence.OnComplete(() => registration.Dispose());
            }
            else
            {
                registration.Dispose();
            }
            return sequence;
        }

        private void CancelTransition(Sequence sequence, CancelBehavior cancelBehavior)
        {
            switch (cancelBehavior)
            {
                case CancelBehavior.Stop:
                    sequence.Stop();
                    break;
                case CancelBehavior.Complete:
                    sequence.Complete();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(cancelBehavior), cancelBehavior, null);
            }
        }
    }
}