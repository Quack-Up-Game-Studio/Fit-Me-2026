using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
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
    public class UIButton3D : MonoBehaviour, 
        IPointerDownHandler, 
        IPointerUpHandler, 
        IPointerClickHandler,
        IPointerEnterHandler,
        IPointerExitHandler,
        ISelectHandler,
        IDeselectHandler
    {
        [Title("References")] 
        [SerializeField] private GameObject up;
        [SerializeField] private GameObject down;

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
                if (_button == null)
                {
                    _button = GetComponent<Button>();
                }
                return _button;
            }
        }
        
        private Button _button;
        private IDisposable _bindings;

        private readonly ReactiveProperty<bool> _isPointerDown = new(false);
        private readonly ReactiveProperty<bool> _isPointerOver = new(false);
        private readonly ReactiveProperty<bool> _isSelected = new(false);
        private readonly ReactiveProperty<bool> _isInteractable = new(true);

        private void Awake()
        {
            _button = GetComponent<Button>();
            if (!_button)
            {
                Debug.LogError("UIButton3D requires a Button component to function properly.");
                return;
            }
            
            _button.transition = Selectable.Transition.None;
            
            up.SetActive(true);
            down.SetActive(false);
            
            InitializeImages();
        }

        private void OnEnable()
        {
            InitializeImages();
            Bind();
        }

        private void OnDisable()
        {
            _bindings?.Dispose();
            _bindings = null;
            _isPointerDown.Value = false;
            _isPointerOver.Value = false;
            _isSelected.Value = false;

            ApplyTint(ButtonSelectionState.Normal, instant: true);
        }

        private void InitializeImages()
        {
            if (childImages != null && childImages.Count > 0) return;

            childImages = new List<Image>();
            if (up != null)
            {
                childImages.AddRange(up.GetComponentsInChildren<Image>(true));
            }
            if (down != null)
            {
                childImages.AddRange(down.GetComponentsInChildren<Image>(true));
            }

            if (_button == null) _button = GetComponent<Button>();
            if (_button == null || _button.targetGraphic == null) return;

            if (_button.transition != Selectable.Transition.None)
            {
                childImages.RemoveAll(img => img == _button.targetGraphic);
                return;
            }

            if (_button.targetGraphic is Image targetImg && !childImages.Contains(targetImg))
            {
                childImages.Add(targetImg);
            }
        }

        private void Bind()
        {
            _bindings?.Dispose();
            var disposableBuilder = Disposable.CreateBuilder();

            if (_button == null) _button = GetComponent<Button>();
            if (_button != null)
            {
                Observable.EveryValueChanged(_button, b => IsSelectableInteractable(b))
                    .Subscribe(interactable => _isInteractable.Value = interactable)
                    .AddTo(ref disposableBuilder);
            }

            Observable.CombineLatest(
                _isInteractable,
                _isPointerDown,
                _isPointerOver,
                _isSelected,
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

            _bindings = disposableBuilder.Build();
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

        public void ApplyTint(ButtonSelectionState state, bool instant = false)
        {
            if (_button == null) _button = GetComponent<Button>();
            if (_button == null) return;

            ColorBlock colors = useCustomColors ? customColors : _button.colors;
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

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_button.interactable) return;
            if (eventData.button != PointerEventData.InputButton.Left) return;
            OnClick().Forget();
        }

        private async UniTaskVoid OnClick()
        {
            up.SetActive(false);
            down.SetActive(true);
            await UniTask.WaitForSeconds(0.05f, cancellationToken: destroyCancellationToken);
            up.SetActive(true);
            down.SetActive(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                _isPointerDown.Value = true;
            }

            if (!_button.interactable) return;
            if (eventData.button != PointerEventData.InputButton.Left) return;
            up.SetActive(false);
            down.SetActive(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                _isPointerDown.Value = false;
            }

            if (!_button.interactable) return;
            if (eventData.button != PointerEventData.InputButton.Left) return;
            up.SetActive(true);
            down.SetActive(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isPointerOver.Value = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isPointerOver.Value = false;
            _isPointerDown.Value = false;
        }

        public void OnSelect(BaseEventData eventData)
        {
            _isSelected.Value = true;
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _isSelected.Value = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_button == null)
            {
                _button = GetComponent<Button>();
            }

            if (_button != null)
            {
                _button.transition = Selectable.Transition.None;
            }
        }
#endif
    }
}