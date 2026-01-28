using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using QuackUp.Utils;
using R3;
using UnityEngine.Serialization;
using VContainer;

namespace FitMe.Panel
{
    public interface IPanelViewModel
    {
        string PanelId { get; set; }
        ReactiveProperty<VisibilityState> VisibilityState { get; }
        ReactiveProperty<TransitionState> TransitionState { get; set; }
        ReactiveProperty<InputState> InputState { get; }
        /// <summary>
        /// Command view to transition in. Returns a <see cref="Promise{T}"/> that completes when the transition is done.
        /// </summary>
        ReactiveCommand<TransitionCommandData> TransitionInCommand { get; }
        /// <summary>
        /// Command view to transition out. Returns a <see cref="Promise{T}"/> that completes when the transition is done.
        /// </summary>
        ReactiveCommand<TransitionCommandData> TransitionOutCommand { get; }
        /// <summary>
        /// Call from view to request a crossfade to another panel.
        /// </summary>
        ReactiveCommand<CrossfadeCommandData> CrossfadeCommand { get; }
    }

    public enum VisibilityState
    {
        Hidden,
        Visible,
    }
    
    public enum InputState
    {
        Active,
        Inactive,
    }

    public enum TransitionState
    {
        None,
        In,
        Out,
    }

    public struct TransitionCommandData
    {
        public Promise<Unit> Promise { get; private set; }
        public string TransitionKey { get; private set; }
        
        public TransitionCommandData(Promise<Unit> promise, string transitionKey)
        {
            Promise = promise;
            TransitionKey = transitionKey;
        }
    }

    [Serializable]
    public struct CrossfadeCommandData
    {
        public string targetPanelKey;
        public CrossfadeSettings crossfadeSettings;
    }
    
    public abstract class PanelViewModelBase : IPanelViewModel, IDisposable
    {
        public string PanelId { get; set; }
        public ReactiveProperty<VisibilityState> VisibilityState { get; } = new(Panel.VisibilityState.Hidden);
        public ReactiveProperty<TransitionState> TransitionState { get; set; } = new(Panel.TransitionState.None);
        public ReactiveProperty<InputState> InputState { get; } = new(Panel.InputState.Inactive);
        public ReactiveCommand<TransitionCommandData> TransitionInCommand { get; } = new();
        public ReactiveCommand<TransitionCommandData> TransitionOutCommand { get; } = new();
        public ReactiveCommand<CrossfadeCommandData> CrossfadeCommand { get; } = new();
        
        private readonly PanelManager _panelManager;
        private IDisposable _bindings;

        [Inject]
        public PanelViewModelBase(PanelManager panelManager)
        {
            _panelManager = panelManager;
            BindBase();
        }

        private void BindBase()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            CrossfadeCommand
                .SubscribeAwait((x, ct) => OnCrossfadeRequested(x), AwaitOperation.Drop)
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }
        
        public void Dispose()
        {
            _bindings?.Dispose();
        }
        
        protected virtual async UniTask OnCrossfadeRequested(CrossfadeCommandData data)
        {
            await _panelManager.Crossfade(PanelId, data.targetPanelKey, data.crossfadeSettings);
        }
    }
}