using System;
using System.Collections.Generic;
using FitMe.Shared;
using QuackUp.Utils;
using R3;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using TMPro;
using UnityEngine;

namespace FitMe.Panel
{
    [ShowOdinSerializedPropertiesInInspector]
    public class LeaderboardTabView : PanelView
    {
        [SerializeField] private GameMode gameMode;
        [SerializeField] private LeaderboardBlock[] topLeaderboardBlocks;
        [SerializeField] private LeaderboardBlock personalLeaderboardBlock;
        [SerializeField] private TMP_Text leaderboardTitleText;
        [SerializeField] private TMP_Text focusHeaderText;
        [SerializeField] private LeaderboardBlock leaderboardBlockPrefab;
        [SerializeField] private RectTransform leaderboardBlockParent;
        [SerializeField] private UIToggleControl sortByToggleControl;
        [SerializeField] private UIToggleControl focusOnToggleControl;
        [OdinSerialize] private Dictionary<GameMode, string> panelIdMapping = new();
        
        private readonly List<LeaderboardBlock> _leaderboardBlocks = new();
        private LeaderboardTabViewModel ViewModel => (LeaderboardTabViewModel)BaseViewModel;
        private IDisposable _bindings;
        
        public override void Construct(IPanelViewModel viewModel)
        {
            base.Construct(viewModel);
            sortByToggleControl.SelectOption((int)ViewModel.SortBy.Value, instant: true);
            focusOnToggleControl.SelectOption((int)ViewModel.FocusOn.Value, instant: true);
            Bind();
        }

        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            ViewModel.CurrentGameMode
                .IgnoreFirstValueWhenSubscribe()
                .Subscribe(OnGameModeChanged)
                .AddTo(ref disposableBuilder);
            sortByToggleControl.SelectedIndex
                .IgnoreFirstValueWhenSubscribe()
                .Subscribe(OnSortByChanged)
                .AddTo(ref disposableBuilder);
            focusOnToggleControl.SelectedIndex
                .Do(x =>
                {
                     switch ((FocusOnMode)x)
                    {
                        case FocusOnMode.Top:
                            focusHeaderText.text = "Global Ranks";
                            break;
                        case FocusOnMode.Player:
                            focusHeaderText.text = "Nearby Ranks";
                            break;
                    }
                })
                .IgnoreFirstValueWhenSubscribe()
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
                block.SetData(userData, userData.DisplayName, scoreData.RawValue, fitData.RawValue, scoreData.Timestamp, scoreData.Rank);
                _leaderboardBlocks.Add(block);
            }

            for (var i = 0; i < topLeaderboardBlocks.Length; i++)
            {
                var block = topLeaderboardBlocks[i];
                var hasTopThree = data.TopThree != null && i < data.TopThree.Count;
                block.gameObject.SetActive(hasTopThree);
                if (!hasTopThree) continue;

                var top = data.TopThree[i];
                var userData = top.UserData;
                var scoreData = top.Score;
                var fitData = top.Fit;
                block.SetData(userData, userData.DisplayName, scoreData.RawValue, fitData.RawValue, scoreData.Timestamp, scoreData.Rank);
            }
            
            personalLeaderboardBlock.SetData(
                data.PersonalEntry.UserData, 
                data.PersonalEntry.UserData.DisplayName, 
                data.PersonalEntry.Score.RawValue, 
                data.PersonalEntry.Fit.RawValue, 
                data.PersonalEntry.Date, 
                data.PersonalEntry.Rank);
        }
    }
}