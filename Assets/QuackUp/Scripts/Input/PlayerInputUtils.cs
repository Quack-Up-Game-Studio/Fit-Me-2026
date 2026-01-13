using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Madduck.Scripts.Input;
using R3;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

namespace QuackUp.Input
{
    public interface IPlayerInputHandler
    {
        #region Values
        public SerializableReactiveProperty<bool> AnyButtonPressed { get; }
        public SerializableReactiveProperty<Vector2> JerkBaitDirection { get; }
        public SerializableReactiveProperty<Vector2> MovementInput { get; }
        public SerializableReactiveProperty<Vector2> MouseDelta { get; }
        public SerializableReactiveProperty<Vector2> MouseUnitCircle { get; }
        public SerializableReactiveProperty<Vector2> RightStickUnitCircle { get; }
        public SerializableReactiveProperty<Vector2> LeftStickUnitCircle { get; }
        public SerializableReactiveProperty<float> BaitSelectInput { get; }

        #endregion

        #region Buttons
        public InputButton InteractButton { get; }
        public InputButton JerkBaitButton { get; }
        public InputBinding[] JerkBindings { get; }
        public InputButton Action0Button { get; }
        public InputButton Action1Button { get; }
        public InputButton ReelingButton { get; }
        public InputButton PauseGameButton { get; }
        public InputButton SecretResetButton { get; }
        public InputButton BaitButton { get; }
        public InputButton ConfirmBaitButton { get; }
        #endregion
        
        public string CurrentControlScheme { get; }
        public event Action<string> OnControlSchemeChanged;
        
        void SetActiveInput(bool active);
    }

    public class PlayerInputHandlerMock : IPlayerInputHandler
    {
        public SerializableReactiveProperty<bool> AnyButtonPressed { get; set; }
        public SerializableReactiveProperty<Vector2> JerkBaitDirection { get; }
        public SerializableReactiveProperty<Vector2> MovementInput { get; set; }
        public SerializableReactiveProperty<Vector2> MouseDelta { get; set; }
        public SerializableReactiveProperty<Vector2> MouseUnitCircle { get; set; }
        public SerializableReactiveProperty<Vector2> RightStickUnitCircle { get; set; }
        public SerializableReactiveProperty<Vector2> LeftStickUnitCircle { get; set; }
        public SerializableReactiveProperty<float> BaitSelectInput { get; set; }
        public InputButton InteractButton { get; set; }
        public InputButton JerkBaitButton { get; set; }
        public InputBinding[] JerkBindings { get; set; }
        public InputButton Action0Button { get; set; }
        public InputButton Action1Button { get; set; }
        public InputButton ReelingButton { get; set; }
        public InputButton PauseGameButton { get; set; }
        public InputButton SecretResetButton { get; }
        public InputButton BaitButton { get; set; }
        public InputButton ConfirmBaitButton { get; set; }
        public event Action<string> OnControlSchemeChanged;
        public string CurrentControlScheme { get; set; }
        public void SetActiveInput(bool active){}
        
    }
    public enum InputType
    {
        UI = 0,
        NonUI = 1
    }
    
    #region Data Structures

        [Serializable]
        public record InputButton(InputAction InputAction)
        {
            public InputAction InputAction { get; internal set; } = InputAction;

            [ShowInInspector, DisplayAsString]
            public string ButtonName =>
                InputAction != null
                    ? InputAction.GetBindingDisplayString(UnityEngine.InputSystem.InputBinding.DisplayStringOptions.DontIncludeInteractions)
                    : string.Empty;
            public SerializableReactiveProperty<bool> IsDown { get; private set; } = new(false);
            public SerializableReactiveProperty<bool> IsUp { get; private set; } = new(false);
            public SerializableReactiveProperty<bool> IsHeld { get; private set; } = new(false);
            public SerializableReactiveProperty<bool> IsUpAfterHeld { get; private set; } = new(false);
            public InputBinding? InputBinding { get; private set; }
            private bool _heldLastTime;
            private CancellationTokenSource _cts = new();

            public void BindPressButton(InputAction.CallbackContext context)
            {
                InputBinding = context.action.GetBindingForControl(context.control);
                IsDown.Value = context.performed;
                IsUp.Value = context.canceled;
                IsHeld.Value = context.performed;
                IsUpAfterHeld.Value = context.canceled;
                _heldLastTime = context.performed;
                _cts = new();
                ButtonPressTask(_cts.Token).Forget();
            }
            
            public void BindPassThroughButton(InputAction.CallbackContext context)
            {
                InputBinding = context.action.GetBindingForControl(context.control);
                var down = context.ReadValueAsButton();
                IsDown.Value = down;
                IsUp.Value = !down;
                IsHeld.Value = down;
                IsUpAfterHeld.Value = !down;
                _heldLastTime = down;
                _cts = new();
                ButtonPressTask(_cts.Token).Forget();
            }
            
            public void BindPassThroughVector2(InputAction.CallbackContext context)
            {
                InputBinding = context.action.GetBindingForControl(context.control);
                var down = context.ReadValue<Vector2>() != Vector2.zero;
                IsDown.Value = down;
                IsUp.Value = !down;
                IsHeld.Value = down;
                IsUpAfterHeld.Value = !down;
                _heldLastTime = down;
                _cts = new();
                ButtonPressTask(_cts.Token).Forget();
            }

            public void BindHoldButton(InputAction.CallbackContext context)
            {
                InputBinding = context.action.GetBindingForControl(context.control);
                switch (context)
                {
                    case { started: true, performed: false }:
                        IsDown.Value = true;
                        IsHeld.Value = false;
                        IsUp.Value = false;
                        IsUpAfterHeld.Value = false;
                        _heldLastTime = false;
                        _cts = new();
                        ButtonPressTask(_cts.Token).Forget();
                        break;
                    case { performed: true }:
                        IsDown.Value = false;
                        IsHeld.Value = true;
                        IsUp.Value = false;
                        IsUpAfterHeld.Value = false;
                        _heldLastTime = true;
                        break;
                    case { canceled: true }:
                        IsDown.Value = false;
                        IsHeld.Value = false;
                        IsUp.Value = true;
                        IsUpAfterHeld.Value = _heldLastTime;
                        _cts = new();
                        ButtonPressTask(_cts.Token).Forget();
                        break;
                }
            }
            
            private async UniTaskVoid ButtonPressTask(CancellationToken token)
            {
                await UniTask.WaitForEndOfFrame(token);
                IsDown.Value = false;
                if (!IsHeld.Value)
                {
                    IsUp.Value = false;
                    IsUpAfterHeld.Value = false;
                }
            }
        }

        #endregion
}