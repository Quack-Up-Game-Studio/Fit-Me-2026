using System;
using System.Collections.Generic;
using FitMe.Shared;
using R3;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace FitMe.Panel
{
    public class GameplayPanelView : PanelView
    {
        [SerializeField] private Button pauseButton;
        [SerializeField] private string pausePanelId = "Pause";

        [Header("Shape Mode")]
        [Tooltip("Legacy UI to hide only in Shape Mode. Leaving Shape Mode restores each target's previous active state; unlisted objects are untouched.")]
        [SerializeField] private GameObject[] hideInShapeMode = Array.Empty<GameObject>();

        private readonly Dictionary<GameObject, bool> _activeStatesBeforeShapeMode = new();

        private void OnGameModeChanged(GameMode mode)
        {
            if (mode == GameMode.LevelShape)
            {
                if (hideInShapeMode == null) return;
                foreach (var target in hideInShapeMode)
                {
                    if (!target) continue;
                    if (!_activeStatesBeforeShapeMode.ContainsKey(target))
                        _activeStatesBeforeShapeMode.Add(target, target.activeSelf);
                    target.SetActive(false);
                }
                return;
            }

            foreach (var state in _activeStatesBeforeShapeMode)
                if (state.Key) state.Key.SetActive(state.Value);
            _activeStatesBeforeShapeMode.Clear();
        }

        private GameplayPanelViewModel ViewModel => (GameplayPanelViewModel)BaseViewModel;
        private IDisposable _bindings;

        [Inject]
        public override void Construct(IPanelViewModel viewModel)
        {
            base.Construct(viewModel);
            Bind();
        }

        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            pauseButton.onClick.AsObservable()
                .Subscribe(_ => OnPauseButtonClicked())
                .AddTo(ref disposableBuilder);
            ViewModel.CurrentGameMode
                .Subscribe(OnGameModeChanged)
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }

        public override void Dispose()
        {
            base.Dispose();
            _bindings?.Dispose();
        }

        private void OnPauseButtonClicked()
        {
            ViewModel.PauseCommand.Execute(Unit.Default);
            if (!TryGetCrossfadeRule(pausePanelId, out var rule)) return;
            ViewModel.CrossfadeCommand.Execute(new CrossfadeCommandData(pausePanelId, rule.crossfadeSettings));
        }
    }
}