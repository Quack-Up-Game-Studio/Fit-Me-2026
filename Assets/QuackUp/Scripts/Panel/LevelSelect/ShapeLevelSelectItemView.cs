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
            levelOrderText.text = item.Order.ToString();
            if (lockedIndicator) lockedIndicator.SetActive(!item.IsUnlocked);
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
