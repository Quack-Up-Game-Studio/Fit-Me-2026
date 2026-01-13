using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Madduck.Scripts.Input;
using R3;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using Observable = R3.Observable;

namespace QuackUp.Input
{
    /// <summary>
    /// Handle player inputs.
    /// </summary>
    [Serializable]
    public class PlayerInputHandler : 
        MonoBehaviour, 
        PlayerInputAction.IPlayerActions, 
        PlayerInputAction.IUIActions,
        IPlayerInputHandler
    {
        #region Inspector

        #region Values
        [field: ReadOnly, 
                ShowInInspector] public SerializableReactiveProperty<bool> AnyButtonPressed { get; private set; } = new();
        [field: ReadOnly, 
                ShowInInspector] public SerializableReactiveProperty<Vector2> JerkBaitDirection { get; private set; } = new();
        [field: ReadOnly, 
                ShowInInspector] public SerializableReactiveProperty<Vector2> MovementInput { get; private set; } = new();
        [field: ReadOnly, 
                ShowInInspector] public SerializableReactiveProperty<Vector2> MouseDelta { get; private set; } = new();
        [field: ReadOnly, 
                ShowInInspector] public SerializableReactiveProperty<Vector2> MouseUnitCircle { get; private set; } = new();
        [field: ReadOnly, 
                ShowInInspector] public SerializableReactiveProperty<Vector2> RightStickUnitCircle { get; private set; } = new();
        [field: ReadOnly, 
                ShowInInspector] public SerializableReactiveProperty<Vector2> LeftStickUnitCircle { get; private set; } = new();
        [field: ReadOnly, 
                ShowInInspector] public SerializableReactiveProperty<float> BaitSelectInput { get; private set; } = new();
        #endregion

        #region Buttons

        [field: ReadOnly, 
                ShowInInspector] public InputButton InteractButton { get; private set; } = new(null);
        [field: ReadOnly, 
                ShowInInspector] public InputButton JerkBaitButton { get; private set; } = new(null);
        
        public InputBinding[] JerkBindings
        {
            get
            {
                var action = _playerInputAction.Player.JerkBait;
                var bindings = action.bindings;
                var composites = bindings.Where(x => x.isPartOfComposite && x.groups
                    .Split(';').Any(s => s == _currentControlScheme.CurrentValue)).ToList();
                return composites.ToArray();
            }
        }
        
        [field: ReadOnly, 
                ShowInInspector] public InputButton Action0Button { get; private set; } = new(null);
        [field: ReadOnly, 
                ShowInInspector] public InputButton Action1Button { get; private set; } = new(null);
        [field: ReadOnly, 
                ShowInInspector] public InputButton ReelingButton { get; private set; } = new(null);
        [field: ReadOnly, 
                ShowInInspector]public InputButton BaitButton { get; private set; } = new(null);
        [field: ReadOnly, 
                ShowInInspector]public InputButton ConfirmBaitButton { get; private set; } = new(null);
        [field: ReadOnly, 
                ShowInInspector] public InputButton PauseGameButton { get; private set; } = new(null);
        [field: ReadOnly, 
                ShowInInspector] public InputButton SecretResetButton { get; private set; } = new(null);

        #endregion
        
        public event Action<string> OnControlSchemeChanged;
        public string CurrentControlScheme => _currentControlScheme.Value;

        #endregion

        #region Fields

        private PlayerInputAction _playerInputAction;
        private ReactiveProperty<string> _currentControlScheme = new("Mouse & Keyboard");
        private string _beforeDeactivateControlScheme = "Mouse & Keyboard";
        private bool _activationJustChanged;
        private List<string> _schemeRequest = new();
        private Vector2 _lastLeftStickValue;
        private IDisposable _currentControlSchemeSubscription;
        private IDisposable _anyButtonPressListener;

        #endregion

        #region Life Cycle

        private void OnEnable()
        {
            _anyButtonPressListener = InputSystem.onAnyButtonPress.Call(x => OnAnyButton(x).Forget());
            _currentControlSchemeSubscription = _currentControlScheme
                .DistinctUntilChanged()
                .Subscribe(scheme => OnControlSchemeChanged?.Invoke(scheme));
            Subscribe();
            RegisterInputAction();
        }

        private void OnDisable()
        {
            Unsubscribe();
            _currentControlSchemeSubscription?.Dispose();
        }

        private void RegisterInputAction()
        {
            InteractButton.InputAction = _playerInputAction.Player.Interact;
            JerkBaitButton.InputAction = _playerInputAction.Player.JerkBait;
            Action0Button.InputAction = _playerInputAction.Player.Action0;
            Action1Button.InputAction = _playerInputAction.Player.Action1;
            ReelingButton.InputAction = _playerInputAction.Player.Reeling;
            PauseGameButton.InputAction = _playerInputAction.UI.PauseGame;
            SecretResetButton.InputAction = _playerInputAction.Player.SecretReset;
            BaitButton.InputAction = _playerInputAction.Player.ToggleBait;
            ConfirmBaitButton.InputAction = _playerInputAction.Player.ConfirmBait;
        }

        #endregion

        #region Subscriptions

        private void Subscribe()
        {
            if (_playerInputAction == null)
            {
                _playerInputAction = new PlayerInputAction();
                _playerInputAction.Player.SetCallbacks(this);
                _playerInputAction.UI.SetCallbacks(this);
            }
            
            _playerInputAction.Player.Enable();
            _playerInputAction.UI.Enable();
        }

        private void Unsubscribe()
        {
            _playerInputAction.Player.Disable();
            //_anyButtonPressListener?.Dispose();
        }

        private void OnDestroy()
        {
            _playerInputAction.Player.Disable();
            _playerInputAction.UI.Disable();
            _playerInputAction?.Dispose();
            _anyButtonPressListener?.Dispose();
        }

        #endregion

        #region Event Handlers

        private async UniTaskVoid OnAnyButton(InputControl inputControl)
        {
            var scheme = _currentControlScheme.Value;
            switch (inputControl.device)
            {
                case Mouse:
                case Keyboard:
                    scheme = "Mouse & Keyboard";
                    break;
                case Gamepad:
                    scheme  = "Gamepad";
                    break;
                case Touchscreen:
                    scheme  = "Touchscreen";
                    break;
                default:
                    Debug.LogWarning("Unknown control scheme detected. Using current scheme.");
                    break;
            }
            _beforeDeactivateControlScheme = scheme;
            _currentControlScheme.Value = scheme;
            AnyButtonPressed.Value = true;
            await UniTask.WaitForEndOfFrame();
            AnyButtonPressed.Value = false;
        }

        public void OnMovement(InputAction.CallbackContext context)
        {
            MovementInput.Value = context.ReadValue<Vector2>();
        }

        public void OnPauseGame(InputAction.CallbackContext context)
        {
            PauseGameButton.BindPressButton(context);
        }

        public void OnSecretReset(InputAction.CallbackContext context)
        {
            SecretResetButton.BindPressButton(context);
        }

        public void OnToggleBait(InputAction.CallbackContext context)
        {
            BaitButton.BindHoldButton(context);
        }

        public void OnSelectBait(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                float input = context.ReadValue<float>();
                BaitSelectInput.Value = input;
            }
            else if (context.canceled)
            {
                BaitSelectInput.Value = 0f;
            }
        }
        public void OnConfirmBait(InputAction.CallbackContext context)
        {
            ConfirmBaitButton.BindPressButton(context);
        }
        public void OnInteract(InputAction.CallbackContext context)
        {
            InteractButton.BindPressButton(context);
        }

        public void OnJerkBait(InputAction.CallbackContext context)
        {
            JerkBaitDirection.Value = context.ReadValue<Vector2>();
            JerkBaitButton.BindPassThroughVector2(context);
        }

        public void OnAction0(InputAction.CallbackContext context)
        {
            Action0Button.BindHoldButton(context);
        }

        public void OnAction1(InputAction.CallbackContext context)
        {
            Action1Button.BindPressButton(context);
        }

        public void OnReeling(InputAction.CallbackContext context)
        {
            ReelingButton.BindHoldButton(context);
        }

        public void OnMouseDelta(InputAction.CallbackContext context)
        {
            HandleSchemeSwitch("Mouse & Keyboard");
            MouseDelta.Value = context.ReadValue<Vector2>();
        }

        public void OnMouseUnitCircle(InputAction.CallbackContext context)
        {
            HandleSchemeSwitch("Mouse & Keyboard");
            var position = context.ReadValue<Vector2>();
            Vector2 screenCenter = new(Screen.currentResolution.width / 2f, Screen.currentResolution.height / 2f);
            var delta = position - screenCenter;
            MouseUnitCircle.Value = delta.normalized;
        }

        public void OnRightStickDelta(InputAction.CallbackContext context)
        {
            HandleSchemeSwitch("Gamepad");
            RightStickUnitCircle.Value = context.ReadValue<Vector2>();
        }
        public void OnLeftStickDelta(InputAction.CallbackContext context)
        {
            HandleSchemeSwitch("Gamepad");
            LeftStickUnitCircle.Value = context.ReadValue<Vector2>();
        }
        #endregion
        
        public void SetActiveInput(bool active)
        {
            _activationJustChanged = true;
            if (active)
            {
                Subscribe();
            }
            else
            {
                _beforeDeactivateControlScheme = _currentControlScheme.Value;
                Unsubscribe();
            }
        }

        private void HandleSchemeSwitch(string scheme)
        {
            _schemeRequest.Add(scheme);
            if (_schemeRequest.Count == 1)
                ResolveScheme().Forget();
        }

        private async UniTaskVoid ResolveScheme()
        {
            await UniTask.WaitForEndOfFrame();
            if (_activationJustChanged)
            {
                _currentControlScheme.Value = _beforeDeactivateControlScheme;
                _activationJustChanged = false;
                _schemeRequest.Clear();
                return;
            }
            var stack = new Stack<string>(_schemeRequest);
            var resolvedScheme = stack.Pop();
            _schemeRequest.Clear();
            _currentControlScheme.Value = resolvedScheme;
        }
    }
}