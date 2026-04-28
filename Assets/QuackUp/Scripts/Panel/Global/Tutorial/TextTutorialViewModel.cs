using System;
using Cysharp.Threading.Tasks;
using QuackUp.Utils;
using R3;
using UnityEngine;

namespace FitMe.Panel.Tutorial
{
    public struct TextTutorialData
    {
        public string Text { get; set; }
        public Sprite Image { get; set; }
        public RectTransformInset? PanelInset { get; set; }
        public bool HasCharacter { get; set; }
        public Vector2? CharacterSize { get; set; }
        public Vector3? CharacterPosition { get; set; }
        public Vector3? CharacterRotation { get; set; }
        public bool HasCarveWindow { get; set; }
        public RectTransformInset? CarveWindowInset { get; set; }
        public bool HasNextButton { get; set; }
    }
    public class TextTutorialViewModel
    {
        public TextTutorialData TutorialData { get; private set; }
        public VisibilityState PreviousVisibilityState { get; private set; }
        public ReadOnlyReactiveProperty<VisibilityState> VisibilityState => _visibilityState;
        public ReadOnlyReactiveProperty<InputState> UIInputState => _uiInputState;
        public ReadOnlyReactiveProperty<bool> IsInputBlocked => _onBlockInput.Select(x => x.BlockInput).ToReadOnlyReactiveProperty();
        public Observable<(Promise<bool> Promise, bool Direction)> OnTransition => _onTransition;
        public Observable<Promise<bool>> OnDisplayData => _onDisplayData;
        public Observable<(Promise<bool> Promise, bool BlockInput)> OnBlockInput => _onBlockInput;
        public ReactiveCommand OnNextCommand { get; } = new();
        
        private readonly ReactiveProperty<VisibilityState> _visibilityState = new(Panel.VisibilityState.Hidden);
        private readonly ReactiveProperty<InputState> _uiInputState = new(InputState.Inactive);
        private readonly Subject<(Promise<bool> Promise, bool Direction)> _onTransition = new();
        private readonly Subject<Promise<bool>> _onDisplayData = new();
        private readonly Subject<(Promise<bool> Promise, bool BlockInput)> _onBlockInput = new();
        
        public void SetData(TextTutorialData data)
        {
            TutorialData = data;
        }

        public async UniTask Hide()
        {
            _uiInputState.Value = InputState.Inactive;
            var showPromise = new Promise<bool>();
            _onTransition?.OnNext((showPromise, false));
            var result = await showPromise.Task;
            if (!result)
            {
                _visibilityState.Value = Panel.VisibilityState.Visible;
                _uiInputState.Value = InputState.Active;
                return;
            }
            PreviousVisibilityState = _visibilityState.Value;
            _visibilityState.Value = Panel.VisibilityState.Hidden;
        }

        public async UniTask Show()
        {
            PreviousVisibilityState = _visibilityState.Value;
            _visibilityState.Value = Panel.VisibilityState.Visible;
            var showPromise = new Promise<bool>();
            _onTransition?.OnNext((showPromise, true));
            var result = await showPromise.Task;
            if (!result)
            {
                _visibilityState.Value = Panel.VisibilityState.Hidden;
                _uiInputState.Value = InputState.Inactive;
                return;
            }
            _uiInputState.Value = InputState.Active;
        }

        public async UniTask DisplayData()
        {
            var promise = new Promise<bool>();
            _onDisplayData?.OnNext(promise);
            await promise.Task;
        }

        public async UniTask ChangeInputBlockState(bool block)
        {
            var promise = new Promise<bool>();
            _onBlockInput?.OnNext((promise, block));
            await promise.Task;
        }
    }
}