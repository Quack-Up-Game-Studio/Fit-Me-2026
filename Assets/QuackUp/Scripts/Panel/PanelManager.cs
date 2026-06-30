using System;
using Debug = QuackUp.Utils.DebugUtils;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using QuackUp.Utils;
using R3;
using UnityEngine;
using VContainer;

namespace FitMe.Panel
{
    public enum CrossfadeType
    {
        Parallel,
        InThenOut,
        OutThenIn,
        InOnly,
        OutOnly,
        None
    }
    
    public class PanelManager
    {
        public Observable<Unit> OnFinishedInitialize => _onFinishedInitialize;
        private readonly Subject<Unit> _onFinishedInitialize = new Subject<Unit>();
        private readonly IReadOnlyDictionary<string, PanelLifetimeScope> _lifetimeScopes;
        private readonly Dictionary<string, IPanelViewModel> _panels = new();
        private readonly string _startupPanelId;
        
        [Inject]
        public PanelManager(
            IReadOnlyDictionary<string, PanelLifetimeScope> lifetimeScopes,
            string startupPanelId)
        {
            _lifetimeScopes = lifetimeScopes;
            _startupPanelId = startupPanelId;
        }
        
        public void Initialize()
        {
            foreach (var kvp in _lifetimeScopes)
            {
                var panel = kvp.Value.CreatPanel();
                panel.PanelId = kvp.Key;
                _panels.Add(kvp.Key, panel);
            }
            Debug.Log($"PanelCount: {_panels.Count}");
            Crossfade(_startupPanelId, _startupPanelId, new CrossfadeSettings
            {
                crossFadeType = CrossfadeType.InOnly,
                customOffset = 0f
            }).Forget();
            _onFinishedInitialize.OnNext(Unit.Default);
        }

        public bool TryGetPanel(string panelId, out IPanelViewModel panel)
        {
            if (_panels.TryGetValue(panelId, out panel)) return true;
            DebugUtils.LogError($"Panel {panelId} not found");
            return false;
        }
        
        public bool TryGetPanel<T>(string panelId, out T panelOfType) where T : IPanelViewModel
        {
            panelOfType = default;
            if (_panels.TryGetValue(panelId, out var panel))
            {
                if (panel is T typedPanel)
                {
                    panelOfType = typedPanel;
                    return true;
                }
                DebugUtils.LogError($"Panel {panelId} is not of type {typeof(T).Name}");
                panelOfType = default;
                return false;
            }
            DebugUtils.LogError($"Panel {panelId} not found");
            return false;
        }

        public bool GetFirstPanelOfType<T>(out T panel) where T : class, IPanelViewModel
        {
            foreach (var p in _panels.Values)
            {
                if (p is not T typedPanel) continue;
                panel = typedPanel;
                return true;
            }
            DebugUtils.LogError($"No panel of type {typeof(T).Name} found");
            panel = null;
            return false;
        }

        public List<T> GetPanelsOfType<T>() where T : IPanelViewModel
        { 
            var panelsOfType = new List<T>();
            foreach (var panel in _panels.Values)
            {
                if (panel is T typedPanel)
                {
                    panelsOfType.Add(typedPanel);
                }
            }
            return panelsOfType;
        }
        
        public async UniTask Crossfade([CanBeNull] string fromPanelId, string toPanelId, CrossfadeSettings crossfadeSettings,
            CancellationToken cancellationToken = default)
        {
            IPanelViewModel fromPanel = null;
            if (fromPanelId != null)
            {
                if (!TryGetPanel(fromPanelId, out fromPanel)) return;
            }
            if (!TryGetPanel(toPanelId, out var toPanel)) return;
            switch (crossfadeSettings.crossFadeType)
            {
                case CrossfadeType.Parallel:
                    TransitionOut(fromPanel, toPanelId, cancellationToken: cancellationToken).Forget();
                    await UniTask.WaitForSeconds(crossfadeSettings.customOffset, cancellationToken: cancellationToken);
                    await TransitionIn(toPanel, fromPanelId, cancellationToken: cancellationToken);
                    break;
                case CrossfadeType.InThenOut:
                    await TransitionIn(toPanel, fromPanelId, cancellationToken: cancellationToken);
                    await UniTask.WaitForSeconds(crossfadeSettings.customOffset, cancellationToken: cancellationToken);
                    await TransitionOut(fromPanel, toPanelId, cancellationToken: cancellationToken);
                    break;
                case CrossfadeType.OutThenIn:
                    await TransitionOut(fromPanel, toPanelId, cancellationToken: cancellationToken);
                    await UniTask.WaitForSeconds(crossfadeSettings.customOffset, cancellationToken: cancellationToken);
                    await TransitionIn(toPanel, fromPanelId, cancellationToken: cancellationToken);
                    break;
                case CrossfadeType.InOnly:
                    if (crossfadeSettings.hidePreviousPanel)
                    {
                        await TransitionOut(fromPanel, toPanelId, false, cancellationToken: cancellationToken);
                    }
                    else
                    {
                        if (fromPanel != null) 
                            fromPanel.InputState.Value = InputState.Inactive;
                    }
                    await UniTask.WaitForSeconds(crossfadeSettings.customOffset, cancellationToken: cancellationToken);
                    await TransitionIn(toPanel, fromPanelId, cancellationToken: cancellationToken);
                    break;
                case CrossfadeType.OutOnly:
                    await TransitionOut(fromPanel, toPanelId, cancellationToken: cancellationToken);
                    await UniTask.WaitForSeconds(crossfadeSettings.customOffset, cancellationToken: cancellationToken);
                    await TransitionIn(toPanel, fromPanelId, false, cancellationToken: cancellationToken);
                    break;
                case CrossfadeType.None:
                    await TransitionOut(fromPanel, toPanelId, false, cancellationToken: cancellationToken);
                    await UniTask.WaitForSeconds(crossfadeSettings.customOffset, cancellationToken: cancellationToken);
                    await TransitionIn(toPanel, fromPanelId,false, cancellationToken: cancellationToken);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        private async UniTask TransitionIn(IPanelViewModel toPanel, string fromPanelId, bool playTransition = true, 
            CancellationToken cancellationToken = default)
        {
            if (toPanel == null) return;
            toPanel.VisibilityState.Value = VisibilityState.Visible;
            if (playTransition)
            {
                var transitionPromise = new Promise<Unit>();
                cancellationToken.Register(() => transitionPromise.Cancel());
                toPanel.TransitionInCommand.Execute(new TransitionCommandData(transitionPromise, fromPanelId));
                await transitionPromise.Task;
            }
            toPanel.InputState.Value = InputState.Active;
        }
        
        private async UniTask TransitionOut(IPanelViewModel fromPanel, string toPanelId, bool playTransition = true, 
            CancellationToken cancellationToken = default)
        {
            if (fromPanel == null) return;
            fromPanel.InputState.Value = InputState.Inactive;
            if (playTransition)
            {
                var transitionPromise = new Promise<Unit>();
                cancellationToken.Register(() => transitionPromise.Cancel());
                fromPanel.TransitionOutCommand.Execute(new TransitionCommandData(transitionPromise, toPanelId));
                await transitionPromise.Task;
            }
            fromPanel.VisibilityState.Value = VisibilityState.Hidden;
        }
    }
}