using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using R3;
using Sirenix.OdinInspector;
using PrimeTween;

namespace QuackUp.Utils
{
    [Serializable]
    public class ToggleOption
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private GameObject backplate;
        [SerializeField] private RectTransform targetRect;

        public Button Button => button;
        public TMP_Text LabelText => labelText;
        public GameObject Backplate => backplate;
        
        public RectTransform TargetRect => targetRect != null 
            ? targetRect 
            : (button != null ? button.GetComponent<RectTransform>() : null);
    }

    public class UIToggleControl : MonoBehaviour
    {
        [Title("Options")]
        [SerializeField] private ToggleOption[] options;
        [SerializeField] private int defaultIndex = 0;

        [Title("Text Style Config")]
        [SerializeField] private Color selectedTextColor = Color.white;
        [SerializeField] private Color deselectedTextColor = Color.gray;
        [SerializeField] private bool changeFontStyle;
        
        [ShowIf(nameof(changeFontStyle))]
        [SerializeField] private FontStyles selectedFontStyle = FontStyles.Normal;
        [ShowIf(nameof(changeFontStyle))]
        [SerializeField] private FontStyles deselectedFontStyle = FontStyles.Normal;

        [Title("Backplate Config")]
        [SerializeField] private bool useSlidingBackplate;
        
        [ShowIf(nameof(useSlidingBackplate))]
        [SerializeField] private RectTransform slidingBackplate;

        [Title("Animation Settings")]
        [SerializeField] private float transitionDuration = 0.2f;
        [SerializeField] private Ease transitionEase = Ease.OutQuad;

        private readonly ReactiveProperty<int> _selectedIndex = new(0);
        public ReadOnlyReactiveProperty<int> SelectedIndex => _selectedIndex.ToReadOnlyReactiveProperty();

        private IDisposable _bindings;
        private Sequence _slideSequence;

        private void Awake()
        {
            _selectedIndex.Value = defaultIndex;
        }

        private void OnEnable()
        {
            Bind();
            SelectOption(_selectedIndex.Value, instant: true);
        }

        private void OnDisable()
        {
            _bindings?.Dispose();
            _bindings = null;
            
            _slideSequence.Stop();
        }

        private void Bind()
        {
            _bindings?.Dispose();
            var disposableBuilder = Disposable.CreateBuilder();

            if (options == null) return;

            for (int i = 0; i < options.Length; i++)
            {
                var index = i;
                var option = options[i];
                if (option == null || option.Button == null) continue;

                option.Button.onClick.AsObservable()
                    .Subscribe(_ => SelectOption(index))
                    .AddTo(ref disposableBuilder);
            }

            _bindings = disposableBuilder.Build();
        }

        public void SelectOption(int index, bool instant = false)
        {
            if (options == null || index < 0 || index >= options.Length)
            {
                return;
            }

            _selectedIndex.Value = index;

            for (int i = 0; i < options.Length; i++)
            {
                var option = options[i];
                if (option == null) continue;

                var isSelected = (i == index);

                // Update Button Interactable
                if (option.Button != null)
                {
                    option.Button.interactable = !isSelected;
                }

                // Update Discrete Backplate
                if (option.Backplate != null)
                {
                    option.Backplate.SetActive(isSelected);
                }

                // Update Text Style & Color
                if (option.LabelText != null)
                {
                    Color targetColor = isSelected ? selectedTextColor : deselectedTextColor;
                    
                    if (instant)
                    {
                        option.LabelText.color = targetColor;
                    }
                    else
                    {
                        Tween.Color(option.LabelText, targetColor, transitionDuration, transitionEase);
                    }

                    if (changeFontStyle)
                    {
                        option.LabelText.fontStyle = isSelected ? selectedFontStyle : deselectedFontStyle;
                    }
                }
            }

            // Update Sliding Backplate
            if (useSlidingBackplate && slidingBackplate != null)
            {
                var targetRect = options[index].TargetRect;
                if (targetRect != null)
                {
                    MoveSlidingBackplate(targetRect, instant);
                }
            }
        }

        private void MoveSlidingBackplate(RectTransform targetRect, bool instant)
        {
            if (slidingBackplate == null || targetRect == null) return;

            _slideSequence.Stop();

            if (instant)
            {
                slidingBackplate.anchorMin = targetRect.anchorMin;
                slidingBackplate.anchorMax = targetRect.anchorMax;
                slidingBackplate.pivot = targetRect.pivot;
                slidingBackplate.anchoredPosition = targetRect.anchoredPosition;
                slidingBackplate.sizeDelta = targetRect.sizeDelta;
            }
            else
            {
                // Calculate target local position based on current backplate pivot to ensure smooth alignment
                Vector3 targetLocalPos = targetRect.localPosition;
                if (slidingBackplate.pivot != targetRect.pivot)
                {
                    Vector2 pivotDiff = targetRect.pivot - slidingBackplate.pivot;
                    targetLocalPos.x -= pivotDiff.x * targetRect.rect.width;
                    targetLocalPos.y -= pivotDiff.y * targetRect.rect.height;
                }

                _slideSequence = Sequence.Create()
                    .Group(Tween.LocalPosition(slidingBackplate, targetLocalPos, transitionDuration, transitionEase))
                    .Group(Tween.UISizeDelta(slidingBackplate, targetRect.rect.size, transitionDuration, transitionEase))
                    .OnComplete(() =>
                    {
                        if (slidingBackplate == null || targetRect == null) return;

                        slidingBackplate.anchorMin = targetRect.anchorMin;
                        slidingBackplate.anchorMax = targetRect.anchorMax;
                        slidingBackplate.pivot = targetRect.pivot;
                        slidingBackplate.anchoredPosition = targetRect.anchoredPosition;
                        slidingBackplate.sizeDelta = targetRect.sizeDelta;
                    });
            }
        }
    }
}
