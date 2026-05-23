using System;
using System.Collections.Generic;
using R3;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace QuackUp.Utils
{
    public enum ButtonSelectionState
    {
        Normal,
        Highlighted,
        Pressed,
        Selected,
        Disabled
    }

    [RequireComponent(typeof(Button))]
    public class CustomTintButton : MonoBehaviour, 
        IPointerDownHandler, 
        IPointerUpHandler, 
        IPointerClickHandler,
        IPointerEnterHandler,
        IPointerExitHandler,
        ISelectHandler,
        IDeselectHandler
    {
        [Title("Target Button")]
        [SerializeField] private Button button;

        [Title("Child Color Tint Config")]
        [SerializeField] private List<Image> childImages = new();
        [SerializeField] private bool useCustomColors;
        
        [ShowIf(nameof(useCustomColors))]
        [SerializeField] private ColorBlock customColors = ColorBlock.defaultColorBlock;
        [SerializeField] private bool autoTintOnDisabled = true;

        public Button Button
        {
            get
            {
                if (_resolvedButton == null)
                {
                    _resolvedButton = button != null ? button : GetComponent<Button>();
                }
                return _resolvedButton;
            }
        }

        private Button _resolvedButton;
        protected IDisposable Bindings;

        protected readonly ReactiveProperty<bool> IsPointerDown = new(false);
        protected readonly ReactiveProperty<bool> IsPointerOver = new(false);
        protected readonly ReactiveProperty<bool> IsSelected = new(false);
        protected readonly ReactiveProperty<bool> IsInteractable = new(true);

        protected virtual void Awake()
        {
            if (_resolvedButton == null)
            {
                _resolvedButton = button != null ? button : GetComponent<Button>();
            }
            if (_resolvedButton != null)
            {
                _resolvedButton.transition = Selectable.Transition.None;
            }
            InitializeImages();
        }

        protected virtual void OnEnable()
        {
            InitializeImages();
            Bind();
        }

        protected virtual void OnDisable()
        {
            Bindings?.Dispose();
            Bindings = null;
            IsPointerDown.Value = false;
            IsPointerOver.Value = false;
            IsSelected.Value = false;

            ApplyTint(ButtonSelectionState.Normal, instant: true);
        }

        protected virtual void InitializeImages()
        {
            if (childImages != null && childImages.Count > 0) return;

            childImages = new List<Image>();
            var targetBtn = Button;
            if (targetBtn != null)
            {
                childImages.AddRange(targetBtn.GetComponentsInChildren<Image>(true));
                if (targetBtn.targetGraphic is Image targetImg && !childImages.Contains(targetImg))
                {
                    childImages.Add(targetImg);
                }
            }
        }

        protected virtual void Bind()
        {
            Bindings?.Dispose();
            var disposableBuilder = Disposable.CreateBuilder();

            var targetBtn = Button;
            if (targetBtn != null)
            {
                Observable.EveryValueChanged(targetBtn, b => IsSelectableInteractable(b))
                    .Subscribe(interactable => IsInteractable.Value = interactable)
                    .AddTo(ref disposableBuilder);
            }

            Observable.CombineLatest(
                IsInteractable,
                IsPointerDown,
                IsPointerOver,
                IsSelected,
                (interactable, pointerDown, pointerOver, selected) =>
                {
                    if (!interactable)
                    {
                        return autoTintOnDisabled ? ButtonSelectionState.Disabled : ButtonSelectionState.Normal;
                    }
                    if (pointerDown) return ButtonSelectionState.Pressed;
                    if (pointerOver) return ButtonSelectionState.Highlighted;
                    if (selected) return ButtonSelectionState.Selected;
                    return ButtonSelectionState.Normal;
                })
                .DistinctUntilChanged()
                .Subscribe(state => ApplyTint(state, instant: false))
                .AddTo(ref disposableBuilder);

            Bindings = disposableBuilder.Build();
        }

        private bool IsSelectableInteractable(Selectable selectable)
        {
            if (selectable == null) return false;
            if (!selectable.interactable) return false;

            var canvasGroups = selectable.GetComponentsInParent<CanvasGroup>();
            foreach (var cg in canvasGroups)
            {
                if (!cg.interactable) return false;
                if (cg.ignoreParentGroups) return true;
            }
            return true;
        }

        public virtual void ApplyTint(ButtonSelectionState state, bool instant = false)
        {
            var targetBtn = Button;
            if (targetBtn == null) return;

            ColorBlock colors = useCustomColors ? customColors : targetBtn.colors;
            Color targetColor;

            switch (state)
            {
                case ButtonSelectionState.Normal:
                    targetColor = colors.normalColor;
                    break;
                case ButtonSelectionState.Highlighted:
                    targetColor = colors.highlightedColor;
                    break;
                case ButtonSelectionState.Pressed:
                    targetColor = colors.pressedColor;
                    break;
                case ButtonSelectionState.Selected:
                    targetColor = colors.selectedColor;
                    break;
                case ButtonSelectionState.Disabled:
                    targetColor = colors.disabledColor;
                    break;
                default:
                    targetColor = colors.normalColor;
                    break;
            }

            targetColor *= colors.colorMultiplier;
            float duration = instant ? 0f : colors.fadeDuration;

            if (childImages == null) return;

            foreach (var img in childImages)
            {
                if (img == null) continue;
                img.CrossFadeColor(targetColor, duration, ignoreTimeScale: true, useAlpha: true);
            }
        }

        public virtual void OnPointerClick(PointerEventData eventData) { }

        public virtual void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                IsPointerDown.Value = true;
            }
        }

        public virtual void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                IsPointerDown.Value = false;
            }
        }

        public virtual void OnPointerEnter(PointerEventData eventData)
        {
            IsPointerOver.Value = true;
        }

        public virtual void OnPointerExit(PointerEventData eventData)
        {
            IsPointerOver.Value = false;
            IsPointerDown.Value = false;
        }

        public virtual void OnSelect(BaseEventData eventData)
        {
            IsSelected.Value = true;
        }

        public virtual void OnDeselect(BaseEventData eventData)
        {
            IsSelected.Value = false;
        }

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }
            if (button != null)
            {
                button.transition = Selectable.Transition.None;
            }
        }
#endif
    }
}
