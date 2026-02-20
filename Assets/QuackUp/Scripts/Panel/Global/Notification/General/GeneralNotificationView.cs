using System;
using Cysharp.Threading.Tasks;
using FitMe.Shared;
using JetBrains.Annotations;
using PrimeTween;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FitMe.Panel
{
    [Serializable]
    public struct GeneralNotificationData : INotificationData
    {
        public string message;
        [CanBeNull] public Sprite icon;
    }
    
    public class GeneralNotificationView : MonoBehaviour, INotificationView
    {
        [Title("Inspectors")]
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text messageText;
        
        [Title("Settings")]
        [SerializeField] private Sprite defaultIcon;

        [Title("Tween")] 
        [SerializeField] private TweenSettings<Vector2> showingRelativePositionTweenSettings;
        [SerializeField] private ShakeSettings scaleTweenSettings;

        private TweenSettings<Vector2> _relativePositionSettings;
        private Sequence _visibilitySequence;
        private Sequence _animationSequence;
        
        public void SetData<T>(T data) where T : INotificationData
        {
            if (data is GeneralNotificationData generalData)
            {
                messageText.text = generalData.message;
                icon.sprite = generalData.icon ? generalData.icon : defaultIcon;
            }
            else
            {
                Debug.LogWarning($"[GeneralNotificationView] Invalid data type: {typeof(T)}");
            }
        }

        public void Initialize()
        {
            messageText.text = string.Empty;
            icon.sprite = null;
            _relativePositionSettings = showingRelativePositionTweenSettings.ToRelative(((RectTransform)transform).anchoredPosition);
        }
        
        public async UniTask Show()
        {
            _visibilitySequence = Sequence.Create()
                .Group(Tween.UIAnchoredPosition((RectTransform)transform, _relativePositionSettings));
            await _visibilitySequence.ToUniTask();
        }

        public async UniTask PlayAnimation()
        {
            _animationSequence = Sequence.Create()
                .Group(Tween.PunchScale(transform, scaleTweenSettings));
            await _animationSequence.ToUniTask();
        }

        public async UniTask Hide()
        {
            _visibilitySequence = Sequence.Create()
                .Group(Tween.UIAnchoredPosition((RectTransform)transform, _relativePositionSettings.WithDirection(false)));
            await _visibilitySequence.ToUniTask();
        }

        public void Cancel()
        {
            _visibilitySequence.Complete();
            _animationSequence.Complete();
        }
    }
}