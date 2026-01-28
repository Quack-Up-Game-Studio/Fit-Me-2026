using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using QuackUp.Utils;
using R3;
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
            Initialize();
        }
        
        private void Initialize()
        {
            foreach (var kvp in _lifetimeScopes)
            {
                var panel = kvp.Value.CreatPanel();
                panel.PanelId = kvp.Key;
                _panels.Add(kvp.Key, panel);
            }
            Crossfade(null, _startupPanelId, new CrossfadeSettings
            {
                crossFadeType = CrossfadeType.InOnly,
                customOffset = 0f
            }).Forget();
        }

        private bool TryGetPanel(string panelId, out IPanelViewModel panel)
        {
            if (_panels.TryGetValue(panelId, out panel)) return true;
            DebugUtils.LogError($"Panel {panelId} not found");
            return false;
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
                    TransitionOut(fromPanel, cancellationToken: cancellationToken).Forget();
                    await UniTask.WaitForSeconds(crossfadeSettings.customOffset, cancellationToken: cancellationToken);
                    await TransitionIn(toPanel, cancellationToken: cancellationToken);
                    break;
                case CrossfadeType.InThenOut:
                    await TransitionIn(toPanel, cancellationToken: cancellationToken);
                    await UniTask.WaitForSeconds(crossfadeSettings.customOffset, cancellationToken: cancellationToken);
                    await TransitionOut(fromPanel, cancellationToken: cancellationToken);
                    break;
                case CrossfadeType.OutThenIn:
                    await TransitionOut(fromPanel, cancellationToken: cancellationToken);
                    await UniTask.WaitForSeconds(crossfadeSettings.customOffset, cancellationToken: cancellationToken);
                    await TransitionIn(toPanel, cancellationToken: cancellationToken);
                    break;
                case CrossfadeType.InOnly:
                    await TransitionOut(fromPanel, false, cancellationToken: cancellationToken);
                    await UniTask.WaitForSeconds(crossfadeSettings.customOffset, cancellationToken: cancellationToken);
                    await TransitionIn(toPanel, cancellationToken: cancellationToken);
                    break;
                case CrossfadeType.OutOnly:
                    await TransitionOut(fromPanel, cancellationToken: cancellationToken);
                    await UniTask.WaitForSeconds(crossfadeSettings.customOffset, cancellationToken: cancellationToken);
                    await TransitionIn(toPanel, false, cancellationToken: cancellationToken);
                    break;
                case CrossfadeType.None:
                    await TransitionOut(fromPanel, false, cancellationToken: cancellationToken);
                    await UniTask.WaitForSeconds(crossfadeSettings.customOffset, cancellationToken: cancellationToken);
                    await TransitionIn(toPanel, false, cancellationToken: cancellationToken);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        private async UniTask TransitionIn(IPanelViewModel panel, bool playTransition = true, CancellationToken cancellationToken = default)
        {
            if (panel == null) return;
            if (playTransition)
            {
                var transitionPromise = new Promise<Unit>();
                cancellationToken.Register(() => transitionPromise.Cancel());
                panel.TransitionInCommand.Execute(new TransitionCommandData(transitionPromise, panel.PanelId));
                await transitionPromise.Task;
            }
            panel.InputState.Value = InputState.Active;
            panel.VisibilityState.Value = VisibilityState.Visible;
        }
        
        private async UniTask TransitionOut(IPanelViewModel panel, bool playTransition = true, CancellationToken cancellationToken = default)
        {
            if (panel == null) return;
            panel.InputState.Value = InputState.Inactive;
            panel.VisibilityState.Value = VisibilityState.Hidden;
            if (playTransition)
            {
                var transitionPromise = new Promise<Unit>();
                cancellationToken.Register(() => transitionPromise.Cancel());
                panel.TransitionOutCommand.Execute(new TransitionCommandData(transitionPromise, panel.PanelId));
                await transitionPromise.Task;
            }
        }
    }
}