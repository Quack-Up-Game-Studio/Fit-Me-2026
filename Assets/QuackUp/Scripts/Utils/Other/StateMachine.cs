using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using Sirenix.OdinInspector;
using UnityEngine;

namespace QuackUp.Utils
{
    public abstract class State
    {
        /// <summary>
        /// Call when entering the state.
        /// </summary>
        public virtual UniTask Enter() => UniTask.CompletedTask;
        /// <summary>
        /// Call every frame while in the state.
        /// </summary>
        public virtual void Update() { }
        /// <summary>
        /// Call when exiting the state.
        /// </summary>
        public virtual UniTask Exit() => UniTask.CompletedTask;
        /// <summary>
        /// Reset the state to its initial condition.
        /// </summary>
        public virtual UniTask Reset() => UniTask.CompletedTask;
    }
    
    public abstract class StateMachine
    {
        protected State CurrentState;

        /// <summary>
        /// Changes the current state of the state machine.
        /// </summary>
        /// <param name="newState">New state to change to.</param>
        protected async UniTask ChangeState(State newState)
        {
            if (CurrentState != null)
                await CurrentState.Exit();
            CurrentState = newState;
            await CurrentState.Enter();
        }

        public void Update()
        {
            CurrentState?.Update();
        }
    }

    /// <summary>
    /// An extended state machine that provides additional methods for navigating between states, such as moving to the next or previous state,
    /// or jumping to a specific state by key or offset.
    /// </summary>
    [Serializable]
    public class ExtendedStateMachine<TState> : StateMachine, IDisposable where TState : State
    {
        protected readonly OrderedDictionary<string, TState> states = new();
        [ShowInInspector] public IReadOnlyOrderedDictionary<string, TState> States => states;
        
        [ShowInInspector] public new TState CurrentState => (TState)base.CurrentState;
        [field: ShowInInspector] public string CurrentStateKey { get; protected set; }
        [field: ShowInInspector] public int CurrentStateIndex { get; protected set; } = 0;
        
        private TState _previousState;
        private Action _pendingTransition;
        private bool _isTransitioning;
        
        public virtual void AddState(string key, TState state)
        {
            states.Add(key, state);
        }
        
        public virtual void RemoveState(string key)
        {
            states.Remove(key);
        }

        protected virtual async UniTask ChangeStateInternal(TState targetState, int? index = null, [CanBeNull] string key = null)
        {
            _isTransitioning = true;
            var currentState = CurrentState;
            await ChangeState(targetState);
            _previousState = currentState;
            CurrentStateIndex = index ?? states.Values.ToList().IndexOf(targetState);
            CurrentStateKey = key ?? states.Keys.ElementAt(CurrentStateIndex);
            _isTransitioning = false;
        }
        
        /// <summary>
        /// Moves to the next state.
        /// </summary>
        public virtual async UniTask Next()
        {
            _pendingTransition = null;
            if (_isTransitioning)
            {
                _pendingTransition = () => Next().Forget();
                return;
            }
            if (CurrentStateIndex < states.Count - 1)
            {
                var nextState = states[CurrentStateIndex + 1];
                await ChangeStateInternal(nextState, CurrentStateIndex + 1);
            }
            _pendingTransition?.Invoke();
        }

        /// <summary>
        /// Moves to the state with the specified key.
        /// </summary>
        /// <param name="key">Key of the state to move to.</param>
        public virtual async UniTask NextTo(string key)
        {
            _pendingTransition = null;
            if (!states.ContainsKey(key))
            {
                DebugUtils.LogError($"Cannot change state because the state with key '{key}' does not exist.");
                return;
            }
            if (_isTransitioning)
            {
                _pendingTransition = () => NextTo(key).Forget();
                return;
            }
            var startIndex = CurrentStateIndex + 1;
            var targetIndex = states.Keys.ToList().IndexOf(key);
            for (var i = startIndex; i <= targetIndex; i++)
            {
                var nextState = states[targetIndex];
                await ChangeStateInternal(nextState, targetIndex, key);
            }
            _pendingTransition?.Invoke();
        }

        /// <summary>
        /// Moves to the state at the specified index.
        /// </summary>
        /// <param name="index"></param>
        public virtual async UniTask NextTo(int index)
        {
            _pendingTransition = null;
            if (_isTransitioning)
            {
                _pendingTransition = () => NextTo(index).Forget();
                return;
            }
            var startIndex = CurrentStateIndex + 1;
            var targetIndex = Mathf.Clamp(index, 0, states.Count - 1);
            for (var i = startIndex; i <= targetIndex; i++)
            {
                var nextState = states[i];
                await ChangeStateInternal(nextState, i);
            }
            _pendingTransition?.Invoke();
        }

        /// <summary>
        /// Moves to the state at the specified offset from the current state.
        /// </summary>
        /// <param name="offset">Offset from the current state to move forward to.</param>
        public virtual async UniTask NextBy(uint offset)
        {
            _pendingTransition = null;
            if (_isTransitioning)
            {
                _pendingTransition = () => NextBy(offset).Forget();
                return;
            }
            var startIndex = CurrentStateIndex + 1;
            var targetIndex = (int)(CurrentStateIndex + offset);
            targetIndex = Mathf.Clamp(targetIndex, 0, states.Count - 1);
            for (var i = startIndex; i <= targetIndex; i++)
            {
                var nextState = states[targetIndex];
                await ChangeStateInternal(nextState, i);
            }
            _pendingTransition?.Invoke();
        }

        /// <summary>
        /// Moves to the previous state.
        /// </summary>
        public virtual async UniTask Previous()
        {
            _pendingTransition = null;
            if (_isTransitioning)
            {
                _pendingTransition = () => Previous().Forget();
                return;
            }
            if (CurrentStateIndex > 0)
            {
                var previousState = states[CurrentStateIndex - 1];
                await ChangeStateInternal(previousState, CurrentStateIndex - 1);
            }
            _pendingTransition?.Invoke();
        }

        /// <summary>
        /// Moves to the state with the specified key.
        /// </summary>
        /// <param name="key">Key of the state to move to.</param>
        public virtual async UniTask PreviousTo(string key)
        {
            _pendingTransition = null;
            if (!states.ContainsKey(key))                
            {
                DebugUtils.LogError($"Cannot change state because the state with key '{key}' does not exist.");
                return;
            }
            if (_isTransitioning)
            {
                _pendingTransition = () => PreviousTo(key).Forget();
                return;
            }
            var startIndex = CurrentStateIndex - 1;
            var targetIndex = states.Keys.ToList().IndexOf(key);
            for (var i = startIndex; i >= targetIndex; i--)
            {
                var previousState = states[targetIndex];
                await ChangeStateInternal(previousState, i);
            }
            _pendingTransition?.Invoke();
        }
        
        /// <summary>
        /// Moves to the state at the specified index.
        /// </summary> <param name="index"></param>
        public virtual async UniTask PreviousTo(int index)
        {
            _pendingTransition = null;
            if (_isTransitioning)
            {
                _pendingTransition = () => PreviousTo(index).Forget();
                return;
            }
            var startIndex = CurrentStateIndex - 1;
            var targetIndex = Mathf.Clamp(index, 0, states.Count - 1);
            for (var i = startIndex; i >= index; i--)
            {
                var previousState = states[i];
                await ChangeStateInternal(previousState, i);
            }
            _pendingTransition?.Invoke();
        }

        /// <summary>
        /// Moves to the state at the specified offset from the current state.
        /// </summary>
        /// <param name="offset">Offset from the current state to move backward to.</param>
        public virtual async UniTask PreviousBy(uint offset)
        {
            _pendingTransition = null;
            if (_isTransitioning)
            {
                _pendingTransition = () => PreviousBy(offset).Forget();
                return;
            }
            var startIndex = CurrentStateIndex - 1;
            var targetIndex = (int)(CurrentStateIndex - offset);
            targetIndex = Mathf.Clamp(targetIndex, 0, states.Count - 1);
            for (var i = startIndex; i >= targetIndex; i--)
            {
                var previousState = states[targetIndex];
                await ChangeStateInternal(previousState, i);
            }
            _pendingTransition?.Invoke();
        }
        
        /// <summary>
        /// Jumps to the state with the specified key without passing through intermediate states.
        /// </summary>
        /// <param name="key"></param>
        public virtual async UniTask JumpTo(string key)
        {
            _pendingTransition = null;
            if (!states.TryGetValue(key, out var targetState)) 
            {
                DebugUtils.LogError($"Cannot change state because the state with key '{key}' does not exist.");
                return;
            }
            if (_isTransitioning)
            {
                _pendingTransition = () => JumpTo(key).Forget();
                return;
            }
            await ChangeStateInternal(targetState, key: key);
            _pendingTransition?.Invoke();
        }
        
        /// <summary>
        /// Jumps to the state at the specified index without passing through intermediate states.
        /// </summary>
        /// <param name="index"></param>
        public virtual async UniTask JumpTo(int index)
        {
            _pendingTransition = null;
            if (_isTransitioning)
            {
                _pendingTransition = () => JumpTo(index).Forget();
                return;
            }
            var targetIndex = Mathf.Clamp(index, 0, states.Count - 1);
            var targetState = states[targetIndex];
            await ChangeStateInternal(targetState, index);
            _pendingTransition?.Invoke();
        }

        /// <summary>
        /// Jumps to the state at the specified offset from the current state without passing through intermediate states.
        /// </summary>
        /// <param name="offset"></param>
        public virtual async UniTask JumpBy(int offset)
        {
            _pendingTransition = null;
            if (_isTransitioning)
            {
                _pendingTransition = () => JumpTo(offset).Forget();
                return;
            }
            var targetIndex = CurrentStateIndex + offset;
            targetIndex = Mathf.Clamp(targetIndex, 0, states.Count - 1);
            var targetState = states[targetIndex];
            await ChangeStateInternal(targetState, targetIndex);
            _pendingTransition?.Invoke();
        }

        /// <summary>
        /// Reverts to the previous state. If there is no previous state, it does nothing.
        /// </summary>
        public virtual async UniTask Revert()
        {
            _pendingTransition = null;
            if (_previousState == null)
            {
                DebugUtils.LogWarning("Cannot revert state because there is no previous state.");
                return;
            }
            if (_isTransitioning)
            {
                _pendingTransition = () => Revert().Forget();
                return;
            }
            await ChangeStateInternal(_previousState);
            _pendingTransition?.Invoke();
        }

        public void Dispose()
        {
            foreach (var state in states.Values)
            {
                if (state is IDisposable disposableState)
                {
                    disposableState.Dispose();
                }
            }
        }
    }
}
