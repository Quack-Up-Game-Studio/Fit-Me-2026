using FitMe.Achievement;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FitMe.Panel
{
    public class ChallengeBlock : MonoBehaviour
    {
        [Title("References")]
        [SerializeField] private TMP_Text challengeNameText;
        [SerializeField] private TMP_Text challengeDescriptionText;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private Slider progressSlider;
        [SerializeField] private Image challengeIcon;
        [SerializeField] private Image completedIconOverlay;
        [SerializeField] private Image completedOverlay;

        public void SetData(IAchievement achievement)
        {
            challengeNameText.text = achievement.BasePreset.AchievementName;
            challengeDescriptionText.text = achievement.BasePreset.AchievementDescription;
            var progress = achievement.GetProgress();
            progress.x = Mathf.Clamp(progress.x, progress.x, progress.y);
            var isInt = progress.x % 1 == 0 && progress.y % 1 == 0;
            var format = isInt ? "N0" : "N2";
            progressText.text = $"{progress.x.ToString(format)} / {progress.y.ToString(format)}";
            progressSlider.maxValue = progress.y;
            progressSlider.value = progress.x;
            challengeIcon.sprite = achievement.BasePreset.AchievementIcon;
            completedIconOverlay.gameObject.SetActive(achievement.IsCompleted);
            completedOverlay.gameObject.SetActive(achievement.IsCompleted);
        }
    }
}