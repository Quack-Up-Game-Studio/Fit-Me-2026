using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using FitMe.Shared;
using QuackUp.SocialService;
using QuackUp.Utils;
using R3;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FitMe.Panel
{
    [ShowOdinSerializedPropertiesInInspector]
    public class LeaderboardTabView : PanelView
    {
        [SerializeField] private GameMode gameMode;
        [SerializeField] private RectTransform scrollContent;
        [SerializeField] private TMP_Text leaderboardTitleText;
        [SerializeField] private LeaderboardBlock leaderboardBlockPrefab;
        [SerializeField] private RectTransform leaderboardBlockParent;
        [SerializeField] private TMP_Dropdown sortByDropdown;
        [SerializeField] private TMP_Dropdown focusOnDropdown;
        [OdinSerialize] private Dictionary<GameMode, string> panelIdMapping = new();
        
        private readonly List<LeaderboardBlock> _leaderboardBlocks = new();
        
        private RectTransform ScrollContentRectTransform => scrollContent;
        private LeaderboardTabViewModel ViewModel => (LeaderboardTabViewModel)BaseViewModel;
        private IDisposable _bindings;
        
        public override void Construct(IPanelViewModel viewModel)
        {
            base.Construct(viewModel);
            sortByDropdown.options = new List<TMP_Dropdown.OptionData>
            {
                new(nameof(SortByMode.Score)),
                new(nameof(SortByMode.Fit))
            };
            focusOnDropdown.options = new List<TMP_Dropdown.OptionData>
            {
                new(nameof(FocusOnMode.Top)),
                new(nameof(FocusOnMode.Player))
            };
            sortByDropdown.value = (int)ViewModel.SortBy.Value;
            focusOnDropdown.value = (int)ViewModel.FocusOn.Value;
            Bind();
        }

        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            ViewModel.CurrentGameMode
                .IgnoreFirstValueWhenSubscribe()
                .Subscribe(OnGameModeChanged)
                .AddTo(ref disposableBuilder);
            sortByDropdown.onValueChanged.AsObservable()
                .Subscribe(OnSortByChanged)
                .AddTo(ref disposableBuilder);
            focusOnDropdown.onValueChanged.AsObservable()
                .Subscribe(OnFocusOnChanged)
                .AddTo(ref disposableBuilder);
            ViewModel.OnDataLoaded
                .Subscribe(SetLeaderboard)
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }

        public override void Dispose()
        {
            base.Dispose();
            _bindings?.Dispose();
        }
        
        private void OnGameModeChanged(GameMode gameMode)
        {
            if (gameMode == this.gameMode) return;
            if (!panelIdMapping.TryGetValue(gameMode, out var panelId)) return;
            if (!TryGetCrossfadeRule(panelId, out var rule)) return;
            ViewModel.CrossfadeCommand.Execute(new CrossfadeCommandData(panelId, rule.crossfadeSettings));
        }

        private void OnSortByChanged(int sortBy)
        {
            ViewModel.SortBy.Value = (SortByMode)sortBy;
            ViewModel.LoadCommand.Execute(Unit.Default);
        }
        
        private void OnFocusOnChanged(int focusOn)
        {
            ViewModel.FocusOn.Value = (FocusOnMode)focusOn;
            ViewModel.LoadCommand.Execute(Unit.Default);
        }
        
        protected override void OnVisibilityStateChanged(VisibilityState state)
        {
            base.OnVisibilityStateChanged(state);
            if (state is not VisibilityState.Visible)
            {
                return;
            }
            ViewModel.LoadCommand.Execute(Unit.Default);
        }

        private void SetLeaderboard(LeaderboardData data)
        {
            leaderboardTitleText.text = data.LeaderboardTitle;
            _leaderboardBlocks.ForEach(x => Destroy(x.gameObject));
            _leaderboardBlocks.Clear();
            foreach (var entry in data.Entries)
            {
                var block = Instantiate(leaderboardBlockPrefab, leaderboardBlockParent);
                var userData = entry.UserData;
                var scoreData = entry.Score;
                var fitData = entry.Fit;
                block.SetData(userData, userData.DisplayName, scoreData.FormattedValue, fitData.FormattedValue, scoreData.Timestamp, scoreData.Rank);
                _leaderboardBlocks.Add(block);
            }
            ScrollContentRectTransform.ForceRebuildLayout().Forget();
        }
    }
}