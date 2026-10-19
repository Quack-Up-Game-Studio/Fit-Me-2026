using System;
using System.Collections.Generic;
using QuackUp.Utils;
using R3;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FitMe.Panel
{
    public sealed class ShapeWorldSelectItemView : MonoBehaviour
    {
        [SerializeField, Required] private Button selectButton;
        [SerializeField, Required] private TMP_Text worldNameText;
        [SerializeField] private GameObject lockedIndicator;
        [SerializeField] private GameObject completedIndicator;
        private IDisposable _binding;
        public void Construct(ShapeWorldSelectItem item, Action<string> onSelected)
        {
            if (!selectButton || !worldNameText) { DebugUtils.LogError("World item requires button and name references."); return; }
            worldNameText.text = item.DisplayName;
            if (lockedIndicator) lockedIndicator.SetActive(!item.IsUnlocked);
            if (completedIndicator) completedIndicator.SetActive(item.IsComplete);
            selectButton.interactable = item.IsUnlocked;
            _binding?.Dispose();
            _binding = selectButton.OnClickAsObservable().Subscribe(_ => onSelected?.Invoke(item.WorldKey));
        }
        private void OnDestroy() => _binding?.Dispose();
    }
}
