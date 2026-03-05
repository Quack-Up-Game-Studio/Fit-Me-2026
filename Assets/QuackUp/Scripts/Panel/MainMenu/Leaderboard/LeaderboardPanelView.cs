using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using FitMe.Shared;
using QuackUp.SocialService;
using QuackUp.Utils;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FitMe.Panel
{
    public class LeaderboardPanelView : PanelView
    {
        [SerializeField] private RectTransform scrollContent;
        [SerializeField] private TMP_Text leaderboardTitleText;
        [SerializeField] private Button closeButton;
        [SerializeField] private LeaderboardBlock leaderboardBlockPrefab;
        [SerializeField] private RectTransform leaderboardBlockParent;
        
        private readonly List<LeaderboardBlock> _leaderboardBlocks = new();
        private CommonLeaderboardDataRequestResults _scoreLeaderboardDataRequestResults;
        
        private RectTransform ScrollContentRectTransform => scrollContent;
        private LeaderboardPanelViewModel ViewModel => (LeaderboardPanelViewModel)BaseViewModel;
        private IDisposable _bindings;
        
        public override void Construct(IPanelViewModel viewModel)
        {
            base.Construct(viewModel);
            Bind();
        }

        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            closeButton.OnClickAsObservable()
                .Subscribe(_ => OnCloseButtonClicked())
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }

        public override void Dispose()
        {
            base.Dispose();
            _bindings?.Dispose();
        }
        
        private void OnCloseButtonClicked()
        {
            if (!TryGetCrossfadeRule("MainMenu", out var rule)) return;
            ViewModel.CrossfadeCommand.Execute(new CrossfadeCommandData("MainMenu", rule.crossfadeSettings));
        }
        
        protected override void OnVisibilityStateChanged(VisibilityState state)
        {
            base.OnVisibilityStateChanged(state);
            if (state is not VisibilityState.Visible)
            {
                return;
            }
            SetScoreLeaderboard().Forget();
        }

        private async UniTaskVoid SetScoreLeaderboard()
        {
            _scoreLeaderboardDataRequestResults = await LoadLeaderboardData("Score");
            leaderboardTitleText.text = _scoreLeaderboardDataRequestResults.LeaderboardTitle;
            _leaderboardBlocks.ForEach(x => Destroy(x.gameObject));
            _leaderboardBlocks.Clear();
            foreach (var entry in _scoreLeaderboardDataRequestResults.Entries)
            {
                var block = Instantiate(leaderboardBlockPrefab, leaderboardBlockParent);
                var userData = entry.UserData;
                var scoreData = entry.ScoreData;
                block.SetData(userData, userData.DisplayName, scoreData.FormattedValue, scoreData.Timestamp, scoreData.Rank);
                _leaderboardBlocks.Add(block);
            }
            await ScrollContentRectTransform.ForceRebuildLayout();
        }

        private async UniTask<CommonLeaderboardDataRequestResults> LoadLeaderboardData(string leaderboardId)
        {
            return await ViewModel.LeaderboardService.RequestLeaderboardData(LeaderboardDataRequestParameters.Builder
                .CreateBuilder(leaderboardId, 10)
                .WithCollection(LeaderboardDataRequestParameters.LeaderboardCollection.Public)
                .WithStart(LeaderboardDataRequestParameters.LeaderboardStart.TopScores)
                .WithTimeSpan(LeaderboardDataRequestParameters.LeaderboardTimeSpan.AllTime)
                .Build()) as CommonLeaderboardDataRequestResults;
        }
    }
}