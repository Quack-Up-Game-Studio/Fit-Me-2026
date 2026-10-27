using System;
using QuackUp.Utils;
using R3;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FitMe.Panel
{
    public sealed class ShapeLevelSelectItemView : MonoBehaviour
    {
        [Title("References")]
        [SerializeField, Required] private Button selectButton;
        [SerializeField, Required] private TMP_Text levelNameText;
        [SerializeField, Required] private TMP_Text levelOrderText;
        [SerializeField] private Image buttonImage;
        [SerializeField] private Sprite unlockedSprite;
        [SerializeField] private Sprite lockedSprite;
        [SerializeField] private GameObject lockedIndicator;
        [SerializeField] private GameObject invalidIndicator;

        private IDisposable _binding;

        public void Construct(ShapeLevelSelectItem item, Action<string> onSelected)
        {
            if (!selectButton || !levelNameText || !levelOrderText)
            {
                DebugUtils.LogError("Shape Level Select item requires its button, name, and order references.");
                return;
            }

            levelNameText.text = item.DisplayName;
            levelNameText.gameObject.SetActive(false);
            levelOrderText.text = item.Order.ToString();
            levelOrderText.gameObject.SetActive(item.IsUnlocked);
            if (buttonImage)
            {
                var sprite = item.IsUnlocked ? unlockedSprite : lockedSprite;
                if (sprite) buttonImage.sprite = sprite;
                buttonImage.preserveAspect = true;
            }
            // Complete locked artwork includes its own padlock.
            if (lockedIndicator) lockedIndicator.SetActive(!item.IsUnlocked && (!buttonImage || !lockedSprite));
            if (invalidIndicator) invalidIndicator.SetActive(!item.IsValid);
            selectButton.interactable = item.IsSelectable;

            _binding?.Dispose();
            _binding = selectButton.OnClickAsObservable()
                .Subscribe(_ => onSelected?.Invoke(item.LevelId));
        }

        private void OnDestroy()
        {
            _binding?.Dispose();
        }
    }
}
