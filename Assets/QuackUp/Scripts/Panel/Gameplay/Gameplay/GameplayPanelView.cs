using System;
using System.Collections.Generic;
using FitMe.Shared;
using TMPro;
using R3;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace FitMe.Panel
{
    public class GameplayPanelView : PanelView
    {
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button hintButton;
        [SerializeField] private Button retryButton;
        [SerializeField] private TMP_Text hintCountText;
        [SerializeField] private string pausePanelId = "Pause";

        [Header("Shape Mode")]
        [Tooltip("Legacy UI to hide only in Shape Mode. Leaving Shape Mode restores each target's previous active state; unlisted objects are untouched.")]
        [SerializeField] private GameObject[] hideInShapeMode = Array.Empty<GameObject>();

        private readonly Dictionary<GameObject, bool> _activeStatesBeforeShapeMode = new();

        private void OnGameModeChanged(GameMode mode)
        {
            if (hintCountText != null)
                hintCountText.gameObject.SetActive(mode == GameMode.LevelShape);

            if (mode == GameMode.LevelShape)
            {
                if (hideInShapeMode != null)
                {
                    foreach (var target in hideInShapeMode)
                    {
                        if (!target) continue;
                        if (!_activeStatesBeforeShapeMode.ContainsKey(target))
                            _activeStatesBeforeShapeMode.Add(target, target.activeSelf);
                        target.SetActive(false);
                    }
                }
                return;
            }

            foreach (var state in _activeStatesBeforeShapeMode)
                if (state.Key) state.Key.SetActive(state.Value);
            _activeStatesBeforeShapeMode.Clear();
        }

        private GameplayPanelViewModel ViewModel => (GameplayPanelViewModel)BaseViewModel;
        private IDisposable _bindings;
        private Color _retryNormalColor;

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
            if (hintButton != null)
            {
                if (hintCountText == null)
                    hintCountText = hintButton.GetComponentInChildren<TMP_Text>(true);
                if (hintCountText != null)
                {
                    var countRect = hintCountText.rectTransform;
                    countRect.anchorMin = Vector2.one;
                    countRect.anchorMax = Vector2.one;
                    countRect.pivot = Vector2.one;
                    countRect.sizeDelta = new Vector2(36f, 36f);
                    countRect.anchoredPosition = new Vector2(-4f, -4f);
                    hintCountText.alignment = TextAlignmentOptions.Center;
                    hintCountText.fontSize = 28f;
                    ViewModel.RemainingHints
                        .Subscribe(count => hintCountText.text = count.ToString())
                        .AddTo(ref disposableBuilder);
                }
                hintButton.colors = GameplayHintButtonPolicy.WithUnavailableTint(hintButton.colors);
                hintButton.onClick.AsObservable()
                    .Subscribe(_ => ViewModel.HintCommand.Execute(Unit.Default))
                    .AddTo(ref disposableBuilder);
                ViewModel.IsHintVisible
                    .Subscribe(isVisible => hintButton.gameObject.SetActive(isVisible))
                    .AddTo(ref disposableBuilder);
                ViewModel.CanUseHint
                    .Subscribe(canUse => hintButton.interactable = canUse)
                    .AddTo(ref disposableBuilder);
            }
            if (retryButton != null)
            {
                retryButton.gameObject.SetActive(false);
                var retryGraphic = retryButton.targetGraphic;
                _retryNormalColor = retryGraphic != null ? retryGraphic.color : Color.white;
                retryButton.onClick.AsObservable()
                    .Subscribe(_ => ViewModel.RetryCommand.Execute(Unit.Default))
                    .AddTo(ref disposableBuilder);
                ViewModel.IsRetryVisible
                    .Subscribe(isVisible => retryButton.gameObject.SetActive(isVisible))
                    .AddTo(ref disposableBuilder);
                ViewModel.RetryFlashVisible
                    .Subscribe(isFlashing =>
                    {
                        if (retryGraphic != null)
                            retryGraphic.color = isFlashing
                                ? Color.Lerp(_retryNormalColor, Color.yellow, 0.7f)
                                : _retryNormalColor;
                    })
                    .AddTo(ref disposableBuilder);
            }
            ViewModel.CurrentGameMode
                .Subscribe(OnGameModeChanged)
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }

        public override void Dispose()
        {
            base.Dispose();
            _bindings?.Dispose();
            if (retryButton != null && retryButton.targetGraphic != null)
                retryButton.targetGraphic.color = _retryNormalColor;
        }

        private void OnPauseButtonClicked()
        {
            ViewModel.PauseCommand.Execute(Unit.Default);
            if (!TryGetCrossfadeRule(pausePanelId, out var rule)) return;
            ViewModel.CrossfadeCommand.Execute(new CrossfadeCommandData(pausePanelId, rule.crossfadeSettings));
        }
    }
}