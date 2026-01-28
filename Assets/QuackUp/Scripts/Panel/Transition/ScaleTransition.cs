using System;
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

        private Sequence _transitionSequence;
        private RectTransform _transitionObject;

        public void Initialize(ITransitionObjectProvider provider)
        {
            provider.TryGetTransitionObject(rectTransformKey, out _transitionObject);
        }

        public Sequence? Transition()
        {
            if (!_transitionObject) return null;
            var settings = relative
                ? transitionSettings.ToRelative(_transitionObject.localScale)
                : transitionSettings;
            _transitionSequence = Sequence.Create()
                .Group(Tween.Scale(_transitionObject, settings));
            return _transitionSequence;
        }

        public void CancelTransition()
        {
            _transitionSequence.Stop();
        }
    }
}