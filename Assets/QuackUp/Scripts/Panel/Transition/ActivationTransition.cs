using System;
using PrimeTween;
using UnityEngine;

namespace FitMe.Panel
{
    [Serializable]
    public class ActivationTransition : IUITransition
    {
        [SerializeField] private string objectKey = "PanelCanvasGroup";
        [SerializeField] private bool active = true;
        
        private Component _transitionObject;
        public void Initialize(ITransitionObjectProvider provider)
        {
            provider.TryGetTransitionObject(objectKey, out _transitionObject);
        }

        public Sequence? Transition()
        {
            if (!_transitionObject) return null;
            _transitionObject.gameObject.SetActive(active);
            return null;
        }

        public void CancelTransition()
        {
            // No ongoing transition to cancel since this is an instant activation/deactivation.
        }
    }
}