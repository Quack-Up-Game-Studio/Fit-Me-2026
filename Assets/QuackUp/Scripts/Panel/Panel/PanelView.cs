using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using QuackUp.Utils;
using R3;
#if UNITY_EDITOR
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;   
#endif
using Sirenix.OdinInspector;    
using Sirenix.Serialization;
using UnityEngine;
using VContainer;

namespace FitMe.Panel
{
    public interface IPanelView
    {
        void Construct(IPanelViewModel viewModel);
    }

    [Serializable]
    [ShowOdinSerializedPropertiesInInspector]
    public record CrossfadeRule
    {
        [SerializeField] public CrossfadeSettings crossfadeSettings;
        [HideInInspector, OdinSerialize] public TransitionPair transitionPair = new();
#if UNITY_EDITOR
        [Button("Edit Transition")]
        private void EditTransition()
        {
            transitionPair ??= new TransitionPair();
            var window = OdinEditorWindow.CreateOdinEditorWindowInstanceForObject(transitionPair);
            window.titleContent = new GUIContent("Transition Editor", EditorIcons.ImageCollection.Active);
            window.minSize = new Vector2(1000, 500);
            window.Show();
        }
#endif
    }

    [Serializable]
    [ShowOdinSerializedPropertiesInInspector]
    public record TransitionPair
    {
        [OdinSerialize] public ITransitionGroup transitionGroupIn;
        [OdinSerialize] public ITransitionGroup transitionGroupOut;
    }

    [ShowOdinSerializedPropertiesInInspector]
    public abstract class PanelView : SerializedMonoBehaviour, IPanelView, ITransitionObjectProvider, IDisposable
    {
        [SerializeField, Required] protected CanvasGroup canvasGroup;
        [OdinSerialize] protected Dictionary<string, Component> transitionObjects = new();
        [OdinSerialize] protected Dictionary<string, CrossfadeRule> crossfadeRules = new();
        public CanvasGroup CanvasGroup => canvasGroup;
        public RectTransform RectTransform => (RectTransform)transform;
        
        protected IPanelViewModel BaseViewModel;
        protected IDisposable BaseBinding;
        
        [Inject]
        public virtual void Construct(IPanelViewModel viewModel)
        {
            BaseViewModel = viewModel;
            BindBase();
            Initialize();
        }

        protected virtual void Initialize()
        {
            foreach (var rule in crossfadeRules.Values)
            {
                if (rule.transitionPair.transitionGroupIn is BasicTransitionGroup transitionGroupIn)
                {
                    transitionGroupIn.Initialize(this);
                }
                if (rule.transitionPair.transitionGroupOut is BasicTransitionGroup transitionGroupOut)
                {
                    transitionGroupOut.Initialize(this);
                }
            }
        }

        private void BindBase()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            BaseViewModel.InputState
                .Subscribe(OnInputStateChanged)
                .AddTo(ref disposableBuilder);
            BaseViewModel.VisibilityState
                .Subscribe(OnVisibilityStateChanged)
                .AddTo(ref disposableBuilder);
            BaseViewModel.TransitionInCommand
                .Subscribe(OnTransitionIn)
                .AddTo(ref disposableBuilder);
            BaseViewModel.TransitionOutCommand
                .Subscribe(OnTransitionOut)
                .AddTo(ref disposableBuilder);
            BaseBinding = disposableBuilder.Build();
        }
        
        public virtual void Dispose()
        {
            BaseBinding?.Dispose();
        }

        private void OnDestroy()
        {
            Dispose();
        }

        protected virtual void OnInputStateChanged(InputState state)
        {
            canvasGroup.interactable = state == InputState.Active;
            canvasGroup.blocksRaycasts = state == InputState.Active;
        }

        protected virtual void OnVisibilityStateChanged(VisibilityState state)
        {
            canvasGroup.alpha = state == VisibilityState.Visible ? 1f : 0f;
        }

        protected virtual void OnTransitionIn(TransitionCommandData transitionCommandData)
        {
            BaseViewModel.TransitionState.Value = TransitionState.In;
            Transition(transitionCommandData.TransitionKey, true, transitionCommandData.Promise.CancellationToken).ContinueWith(() =>
            {
                BaseViewModel.TransitionState.Value = TransitionState.None;
                transitionCommandData.Promise.TrySetResult(Unit.Default);
            });
        }
        
        protected virtual void OnTransitionOut(TransitionCommandData transitionCommandData)
        {
            BaseViewModel.TransitionState.Value = TransitionState.Out;
            Transition(transitionCommandData.TransitionKey, false, transitionCommandData.Promise.CancellationToken).ContinueWith(() =>
            {
                BaseViewModel.TransitionState.Value = TransitionState.None;
                transitionCommandData.Promise.TrySetResult(Unit.Default);
            });
        }

        protected virtual async UniTask Transition(string transitionKey, bool direction, CancellationToken cancellationToken = default)
        {   
            if (!TryGetCrossfadeRule(transitionKey, out var rule))
            {
                await UniTask.CompletedTask;
                return;
            }
            if (rule == null)
            {
                await UniTask.CompletedTask;
                return;
            }
            var transitionGroup = direction ? rule.transitionPair.transitionGroupIn : rule.transitionPair.transitionGroupOut;
            if (transitionGroup == null)
            {
                DebugUtils.LogWarning($"Transition group for key '{transitionKey}' is null.");
                await UniTask.CompletedTask;
                return;
            }
            await transitionGroup.Transition(cancellationToken);
        }

        public virtual bool TryGetTransitionObject<T>(string key, out T component) where T : Component
        {
            if (transitionObjects.TryGetValue(key, out var obj) && obj is T typedObj)
            {
                component = typedObj;
                return true;
            }
            component = null;
            return false;
        }
        
        protected bool TryGetCrossfadeRule(string key, out CrossfadeRule rule)
        {
            if (crossfadeRules.TryGetValue(key, out var foundRule))
            {
                rule = foundRule;
                return true;
            }
            rule = null;
            DebugUtils.LogWarning($"Transition key '{key}' not found.");
            return false;
        }
    }
}