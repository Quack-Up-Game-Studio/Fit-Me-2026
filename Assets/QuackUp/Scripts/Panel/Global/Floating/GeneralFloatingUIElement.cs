using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using QuackUp.Utils;
using Sirenix.OdinInspector;
#if UNITY_EDITOR
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
#endif
using Sirenix.Serialization;
using UnityEngine;

namespace FitMe.Panel
{
    [ShowOdinSerializedPropertiesInInspector]
    public class AnimationGroup
    {
        [OdinSerialize] public ITransitionGroup transitionGroupIn;
        [OdinSerialize] public ITransitionGroup transitionGroupOut;
        [OdinSerialize] public ITransitionGroup animationGroup;
    }
    
    [ShowOdinSerializedPropertiesInInspector]
    public class GeneralFloatingUIElement : FloatingUIElement, ITransitionObjectProvider
    {
        [OdinSerialize, HideInInspector] private AnimationGroup animationGroup = new();
        [OdinSerialize] private Dictionary<string, Component> transitionObjects = new();
        
        private Vector3 _initialPosition;
        private Quaternion _initialRotation;
        private Vector3 _initialScale;
        
        private bool _initialized;
        private CancellationTokenSource _debugCts = new();
        
#if UNITY_EDITOR
        [Button("Edit Transition")]
        private void EditTransition()
        {
            animationGroup ??= new AnimationGroup();
            var window = OdinEditorWindow.CreateOdinEditorWindowInstanceForObject(animationGroup);
            window.titleContent = new GUIContent("Transition Editor", EditorIcons.ImageCollection.Active);
            window.minSize = new Vector2(1000, 500);
            window.Show();
        }

        [Title("Debug")]
        [Button("Transition In")]
        private void DebugTransitionIn()
        {
            if (!_initialized)
            {
                Initialize();
            }
            TransitionIn(_debugCts.Token).Forget();
        }
        
        [Button("Transition Out")]
        private void DebugTransitionOut()       
        {
            if (!_initialized)
            {
                Initialize();
            }
            TransitionOut(_debugCts.Token).Forget();
        }

        [Button("Animate")]
        private void DebugAnimate()
        {
            if (!_initialized)
            {
                Initialize();
            }
            Animate(_debugCts.Token).Forget();
        }
        
        [Button("Stop All Animations")]
        private void DebugStopAllAnimations()
        {
            _debugCts?.Cancel();
            _debugCts?.Dispose();
            _debugCts = new CancellationTokenSource();
        }

        [Button("Reset")]
        private void DebugReset()
        {
            if (!_initialized)
            {
                Initialize();
            }
            Reset();
        }
#endif

        public void Initialize()
        {
            _initialPosition = transform.position;
            _initialRotation = transform.rotation;
            _initialScale = transform.localScale;
            if (animationGroup.transitionGroupIn is BasicTransitionGroup basicTransitionGroupIn)
            {
                basicTransitionGroupIn.Initialize(this);
            }
            if (animationGroup.transitionGroupOut is BasicTransitionGroup basicTransitionGroupOut)
            {
                basicTransitionGroupOut.Initialize(this);
            }
            if (animationGroup.animationGroup is BasicTransitionGroup basicAnimationGroup)
            {
                basicAnimationGroup.Initialize(this);
            }
            _initialized = true;
        }

        public override UniTask Animate(CancellationToken cancellationToken = default)
        {
            if (!_initialized)
            {
                DebugUtils.LogError($"{GetType().Name}: Attempted to animate before initialization.");
                return UniTask.CompletedTask;
            }
            return animationGroup.animationGroup?.Transition(cancellationToken) ?? UniTask.CompletedTask;
        }

        public override UniTask TransitionIn(CancellationToken cancellationToken = default)
        {
            if (!_initialized)
            {
                DebugUtils.LogError($"{GetType().Name}: Attempted to transition in before initialization.");
                return UniTask.CompletedTask;
            }
            return animationGroup.transitionGroupIn?.Transition(cancellationToken) ?? UniTask.CompletedTask;
        }

        public override UniTask TransitionOut(CancellationToken cancellationToken = default)
        {
            if (!_initialized)
            {
                DebugUtils.LogError($"{GetType().Name}: Attempted to transition out before initialization.");
                return UniTask.CompletedTask;
            }
            return animationGroup.transitionGroupOut?.Transition(cancellationToken) ?? UniTask.CompletedTask;
        }

        public bool TryGetTransitionObject<T>(string key, out T component) where T : Component
        {
            if (transitionObjects.TryGetValue(key, out var obj) && obj is T typedObj)
            {
                component = typedObj;
                return true;
            }
            component = null;
            return false;
        }

        public override void Reset()
        {
            transform.position = _initialPosition;
            transform.rotation = _initialRotation;
            transform.localScale = _initialScale;
        }
    }
}