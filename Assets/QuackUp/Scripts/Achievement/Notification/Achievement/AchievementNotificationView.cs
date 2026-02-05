using System;
using Cysharp.Threading.Tasks;
using FitMe.Shared;
using QuackUp.Utils;
using PrimeTween;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FitMe.Achievement
{
    [Serializable]
    public struct AchievementNotificationData : INotificationData
    {
        [OdinSerialize] public IAchievement achievement;
        
        public AchievementNotificationData(IAchievement achievement)
        {
            this.achievement = achievement;
        }
    }
    
    public class AchievementNotificationView : MonoBehaviour, INotificationView
    {
        [Title("Inspectors")]
        [SerializeField] private TMP_Text achievementNameText;
        [SerializeField] private TMP_Text achievementDescriptionText;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private Slider progressSlider;
        [SerializeField] private Image achievementIcon;
        
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
            if (data is AchievementNotificationData generalData)
            {
                var achievement = generalData.achievement;
                achievementNameText.text = achievement.BasePreset.AchievementName;
                achievementDescriptionText.text = achievement.BasePreset.AchievementDescription;
                var progress = achievement.GetProgress();
                progress.x = Mathf.Clamp(progress.x, progress.x, progress.y);
                var isInt = progress.x % 1 == 0 && progress.y % 1 == 0;
                var format = isInt ? "N0" : "N2";
                progressText.text = $"{progress.x.ToString(format)} / {progress.y.ToString(format)}";
                progressSlider.maxValue = progress.y;
                progressSlider.value = progress.x;
                achievementIcon.sprite = achievement.BasePreset.AchievementIcon ? achievement.BasePreset.AchievementIcon : defaultIcon;
            }
            else
            {
                Debug.LogWarning($"[AchievementNotificationView] Invalid data type: {typeof(T)}");
            }
        }

        public void Initialize()
        {
            achievementNameText.text = string.Empty;
            achievementDescriptionText.text = string.Empty;
            progressText.text = string.Empty;
            progressSlider.maxValue = 1;
            progressSlider.value = 0;
            achievementIcon.sprite = defaultIcon;
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