using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using FitMe.Shared;
using QuackUp.SocialService;
using QuackUp.Utils;
using R3;
using VContainer;

namespace FitMe.Panel
{
    public enum SortByMode
    {
        Score,
        Fit
    }

    public enum FocusOnMode
    {
        Top,
        Player
    }

    public enum LeaderboardStatus
    {
        Loading,
        Loaded,
        Error,
        Cancelled
    }

    public class LeaderboardEntryData
    {
        public int Rank { get; set; }
        public IUserDataProvider UserData { get; set; }
        public ScoreData Score { get; set; }
        public ScoreData Fit { get; set; }
        public DateTime Date { get; set; }
    }

    public class LeaderboardData
    {
        public string LeaderboardTitle { get; set; }
        public List<LeaderboardEntryData> Entries { get; set; } = new();
        public List<LeaderboardEntryData> TopThree { get; set; } = new();
        public List<LeaderboardEntryData> RemainingEntries { get; set; } = new();
        public LeaderboardEntryData PersonalEntry { get; set; } = new();
    }
    
    public class LeaderboardTabViewModel : PanelViewModel
    {
        public ReactiveProperty<SortByMode> SortBy { get; } = new();
        public ReactiveProperty<FocusOnMode> FocusOn { get; } = new();
        public ReadOnlyReactiveProperty<LeaderboardStatus> Status => _status.ToReadOnlyReactiveProperty();
        public ReactiveCommand LoadCommand { get; } = new();
        public ReadOnlyReactiveProperty<GameMode> CurrentGameMode => _panelViewModel.CurrentGameMode;
        public ILeaderboardService LeaderboardService { get; }
        private readonly ReactiveProperty<LeaderboardStatus> _status = new(LeaderboardStatus.Loading);
        private readonly LeaderboardPanelViewModel _panelViewModel;
        
        public Observable<LeaderboardData> OnDataLoaded => _onDataLoaded;
        private readonly Subject<LeaderboardData> _onDataLoaded = new();
        
        private CommonLeaderboardDataRequestResults _scoreLeaderboardDataRequestResults;
        
        private IDisposable _bindings;
        
        [Inject]
        public LeaderboardTabViewModel(
            [Key(PanelManagerInstaller.NestedPanelId)] PanelManager panelManager,
            LeaderboardPanelViewModel panelViewModel,
            ILeaderboardService leaderboardService) : base(panelManager)
        {
            LeaderboardService = leaderboardService;
            _panelViewModel = panelViewModel;
            Bind();
        }

        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            LoadCommand
                .SubscribeAwait((_, ct) => LoadLeaderboardData(ct), AwaitOperation.Switch)
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }

        public override void Dispose()
        {
            base.Dispose();
            _bindings?.Dispose();
        }
        
        private async UniTask LoadLeaderboardData(CancellationToken ct)
        {
            _status.Value = LeaderboardStatus.Loading;
            var scoreLeaderboardId = "Score";
            var fitLeaderboardId = "Fit";
            var focus = FocusOn.Value == FocusOnMode.Top
                ? LeaderboardDataRequestParameters.LeaderboardStart.TopScores
                : LeaderboardDataRequestParameters.LeaderboardStart.PlayerCentered;
            var scoreTask = LeaderboardService.RequestLeaderboardData(LeaderboardDataRequestParameters.Builder
                .CreateBuilder(scoreLeaderboardId, 30)
                .WithCollection(LeaderboardDataRequestParameters.LeaderboardCollection.Public)
                .WithStart(focus)
                .WithTimeSpan(LeaderboardDataRequestParameters.LeaderboardTimeSpan.AllTime)
                .Build());
            var fitTask = LeaderboardService.RequestLeaderboardData(LeaderboardDataRequestParameters.Builder
                .CreateBuilder(fitLeaderboardId, 30)
                .WithCollection(LeaderboardDataRequestParameters.LeaderboardCollection.Public)
                .WithStart(focus)
                .WithTimeSpan(LeaderboardDataRequestParameters.LeaderboardTimeSpan.AllTime)
                .Build());
            var personalScoreTask = LeaderboardService.RequestPersonalLeaderboardData(LeaderboardDataRequestParameters.Builder
                .CreateBuilder(scoreLeaderboardId, 30)
                .WithCollection(LeaderboardDataRequestParameters.LeaderboardCollection.Public)
                .WithStart(LeaderboardDataRequestParameters.LeaderboardStart.PlayerCentered)
                .WithTimeSpan(LeaderboardDataRequestParameters.LeaderboardTimeSpan.AllTime)
                .Build());
            var personalFitTask = LeaderboardService.RequestPersonalLeaderboardData(LeaderboardDataRequestParameters.Builder
                .CreateBuilder(fitLeaderboardId, 30)
                .WithCollection(LeaderboardDataRequestParameters.LeaderboardCollection.Public)
                .WithStart(LeaderboardDataRequestParameters.LeaderboardStart.PlayerCentered)
                .WithTimeSpan(LeaderboardDataRequestParameters.LeaderboardTimeSpan.AllTime)
                .Build());
            var scoreTopThreeTask = FocusOn.Value == FocusOnMode.Top
                ? UniTask.FromResult<ILeaderboardDataRequestResults>(null)
                : LeaderboardService.RequestLeaderboardData(LeaderboardDataRequestParameters.Builder
                    .CreateBuilder(scoreLeaderboardId, 30)
                    .WithCollection(LeaderboardDataRequestParameters.LeaderboardCollection.Public)
                    .WithStart(LeaderboardDataRequestParameters.LeaderboardStart.TopScores)
                    .WithTimeSpan(LeaderboardDataRequestParameters.LeaderboardTimeSpan.AllTime)
                    .Build());
            var fitTopThreeTask = FocusOn.Value == FocusOnMode.Top
                ? UniTask.FromResult<ILeaderboardDataRequestResults>(null)
                : LeaderboardService.RequestLeaderboardData(LeaderboardDataRequestParameters.Builder
                    .CreateBuilder(fitLeaderboardId, 30)
                    .WithCollection(LeaderboardDataRequestParameters.LeaderboardCollection.Public)
                    .WithStart(LeaderboardDataRequestParameters.LeaderboardStart.TopScores)
                    .WithTimeSpan(LeaderboardDataRequestParameters.LeaderboardTimeSpan.AllTime)
                    .Build());

            var results = await UniTask.WhenAll(scoreTask, fitTask);
            var personalResults = await UniTask.WhenAll(personalScoreTask, personalFitTask);
            var topThreeResults = FocusOn.Value == FocusOnMode.Top ? 
                results : 
                await UniTask.WhenAll(scoreTopThreeTask, fitTopThreeTask);

            if (ct.IsCancellationRequested)
            {
                _status.Value = LeaderboardStatus.Cancelled;
                return;
            }
            if (results.Item1 is not CommonLeaderboardDataRequestResults scoreResult ||
                results.Item2 is not CommonLeaderboardDataRequestResults fitResult)
            {
                _status.Value = LeaderboardStatus.Error;
                return;
            }
            DebugUtils.Log($"Score entries count: {scoreResult.Entries.Count}");
            DebugUtils.Log($"Fit entries count: {fitResult.Entries.Count}");
            var entries = HandleSorting(scoreResult, fitResult, SortBy.Value);
            DebugUtils.Log($"Entries count: {entries.Count}");

            List<LeaderboardEntryData> topThreeEntries = null;
            if (topThreeResults.Item1 is CommonLeaderboardDataRequestResults topThreeScoreResult &&
                topThreeResults.Item2 is CommonLeaderboardDataRequestResults topThreeFitResult)
            {
                topThreeEntries = HandleSorting(topThreeScoreResult, topThreeFitResult, SortBy.Value).Take(3).ToList();
            }

            var personalLeaderboardEntry = new LeaderboardEntryData
            {
                Rank = personalResults.Item1.scoreData.Rank,
                UserData = personalResults.Item1.UserData,
                Score = personalResults.Item1.scoreData,
                Fit = personalResults.Item2.scoreData,
                Date = personalResults.Item1.scoreData.Timestamp
            };
            var leaderboardData = new LeaderboardData
            {
                LeaderboardTitle = "Global",
                Entries = entries,
                TopThree = topThreeEntries,
                RemainingEntries = FocusOn.Value == FocusOnMode.Top ? entries.Skip(3).ToList() : entries,
                PersonalEntry = personalLeaderboardEntry
            };
            _onDataLoaded.OnNext(leaderboardData);
            _status.Value = LeaderboardStatus.Loaded;
        }

        private List<LeaderboardEntryData> HandleSorting(CommonLeaderboardDataRequestResults score,
            CommonLeaderboardDataRequestResults fit, SortByMode sortBy)
        {
            var entries = new List<LeaderboardEntryData>();
            var primary = sortBy == SortByMode.Score ? score : fit;
            var secondary = sortBy == SortByMode.Score ? fit : score;
            foreach (var primaryData in primary.Entries)
            {
                var secondaryData = secondary.Entries
                    .Select(x => new ValueTuple<IUserDataProvider, ScoreData>(x.UserData, x.ScoreData))
                    .Cast<ValueTuple<IUserDataProvider, ScoreData>?>()
                    .FirstOrDefault(s =>
                {
                    if (s == null) return false;
                    return s.Value.Item1.UserId == primaryData.UserData.UserId;
                }) ?? new ValueTuple<UserData, ScoreData>()
                {
                    Item1 = primaryData.UserData as UserData,
                    Item2 = new ScoreData
                    {
                        FormattedValue = "-",
                        RawValue = 0,
                        Rank = 0,
                        Timestamp = primaryData.ScoreData.Timestamp
                    }
                };
                entries.Add(new LeaderboardEntryData
                { 
                    Rank = primaryData.ScoreData.Rank,
                    UserData = primaryData.UserData,
                    Score = sortBy == SortByMode.Score ? primaryData.ScoreData : secondaryData.Item2,
                    Fit = sortBy == SortByMode.Score ? secondaryData.Item2 : primaryData.ScoreData,
                    Date = primaryData.ScoreData.Timestamp
                });
            }
            //Sort by primary then by timestamp
            entries = entries.OrderByDescending(e => sortBy == SortByMode.Score ? e.Score.RawValue : e.Fit.RawValue)
                .ThenByDescending(e => e.Date)
                .ToList();
            return entries;
        }
    }
}