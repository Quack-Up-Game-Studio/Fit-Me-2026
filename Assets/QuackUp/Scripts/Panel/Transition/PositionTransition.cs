using System;
using System.Threading;
using PrimeTween;
using QuackUp.Utils;
using UnityEngine;

namespace FitMe.Panel
{
    [Serializable]
    public class PositionTransition : IUITransition
    {
        private enum PositionType
        {
            World,
            Local,
            AnchoredUI
        }
        [SerializeField] private string rectTransformKey = "PanelRectTransform";
        [SerializeField] private PositionType positionType = PositionType.AnchoredUI;
        [SerializeField] private bool relative;
        [SerializeField] private TweenSettings<Vector3> transitionSettings;

        private Sequence _transitionSequence;
        private RectTransform _transitionObject;

        public void Initialize(ITransitionObjectProvider provider)
        {
            provider.TryGetTransitionObject(rectTransformKey, out _transitionObject);
        }

        public Sequence? Transition(CancellationToken cancellationToken = default, CancelBehavior cancelBehavior = CancelBehavior.Stop)
        {
            if (!_transitionObject) return null;
            TweenSettings<Vector3> settings;
            switch (positionType)
            {
                case PositionType.World: 
                    settings = relative
                        ? transitionSettings.ToRelative(_transitionObject.position)
                        : transitionSettings;
                    _transitionSequence = Sequence.Create()
                        .Group(Tween.Position(_transitionObject, settings));
                    break;
                case PositionType.Local:
                    settings = relative
                        ? transitionSettings.ToRelative(_transitionObject.localPosition)
                        : transitionSettings;
                    _transitionSequence = Sequence.Create()
                        .Group(Tween.LocalPosition(_transitionObject, settings));
                    break;
                case PositionType.AnchoredUI:
                    settings = relative
                        ? transitionSettings.ToRelative(_transitionObject.anchoredPosition)
                        : transitionSettings;
                    _transitionSequence = Sequence.Create()
                        .Group(Tween.UIAnchoredPosition(_transitionObject, settings.ToVector2()));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            var sequence = _transitionSequence;
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