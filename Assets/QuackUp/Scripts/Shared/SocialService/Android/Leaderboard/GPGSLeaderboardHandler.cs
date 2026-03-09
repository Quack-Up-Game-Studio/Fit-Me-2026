#if UNITY_ANDROID
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using QuackUp.GPGS;
using QuackUp.SocialService;
using QuackUp.Utils;
using VContainer;

namespace FitMe.SocialService.Android
{
    public class GPGSLeaderboardDataRequestResults : ILeaderboardDataRequestResults
    {
        public LeaderboardScoreData LeaderBoardScoreData { get; set; }
    }
    
    public class GPGSLeaderboardHandler : ILeaderboardService
    {
        private readonly GPGSLeaderboard _leaderboard;
        private readonly IdMapping _leaderboardIdMapping;

        #region Enum Mapping
        private readonly Dictionary<LeaderboardDataRequestParameters.LeaderboardStart, LeaderboardStart> _leaderboardStartMapping = new()
        {
            { LeaderboardDataRequestParameters.LeaderboardStart.TopScores, LeaderboardStart.TopScores },
            { LeaderboardDataRequestParameters.LeaderboardStart.PlayerCentered, LeaderboardStart.PlayerCentered },
        };
        
        private readonly Dictionary<LeaderboardDataRequestParameters.LeaderboardTimeSpan, LeaderboardTimeSpan> _leaderboardTimeSpanMapping = new()
        {
            { LeaderboardDataRequestParameters.LeaderboardTimeSpan.AllTime, LeaderboardTimeSpan.AllTime },
            { LeaderboardDataRequestParameters.LeaderboardTimeSpan.Weekly, LeaderboardTimeSpan.Weekly },
            { LeaderboardDataRequestParameters.LeaderboardTimeSpan.Daily, LeaderboardTimeSpan.Daily },
        };
        
        private readonly Dictionary<LeaderboardDataRequestParameters.LeaderboardCollection, LeaderboardCollection> _leaderboardCollectionMapping = new()
        {
            { LeaderboardDataRequestParameters.LeaderboardCollection.Public, LeaderboardCollection.Public },
            { LeaderboardDataRequestParameters.LeaderboardCollection.Social, LeaderboardCollection.Social },
        };
        #endregion
        
        [Inject]
        public GPGSLeaderboardHandler(
            GPGSLeaderboard leaderboard,
            IdMapping leaderboardIdMapping)
        {
            _leaderboard = leaderboard;
            _leaderboardIdMapping = leaderboardIdMapping;
        }

        public UniTask<bool> ReportData(LeaderboardReportParameters parameters)
        {
            if (!_leaderboardIdMapping.Mapping.TryGetValue(parameters.LeaderboardId, out var id))
            {
                DebugUtils.LogError($"Leaderboard id '{parameters.LeaderboardId}' does not exist in the mapping.");
                return UniTask.FromResult(false);
            }
            return _leaderboard.ReportScore(id, parameters.Data, parameters.Metadata);
        }

        public UniTask<bool> ShowLeaderboardUI(string leaderboardId)
        {
            if (!_leaderboardIdMapping.Mapping.TryGetValue(leaderboardId, out var id))
            {
                DebugUtils.LogError($"Leaderboard id '{leaderboardId}' does not exist in the mapping.");
                return UniTask.FromResult(false);
            }
            _leaderboard.ShowLeaderboardUI(id);
            return UniTask.FromResult(true);
        }

        public async UniTask<ILeaderboardDataRequestResults> RequestLeaderboardData(
            LeaderboardDataRequestParameters parameters)
        {
            if (!_leaderboardIdMapping.Mapping.TryGetValue(parameters.LeaderboardId, out var leaderboardId))
            {
                DebugUtils.LogError($"Leaderboard id '{parameters.LeaderboardId}' does not exist in the mapping.");
                return null;
            }
            var result = await _leaderboard.LoadLeaderboardData(
                leaderboardId,
                _leaderboardStartMapping[parameters.Start],
                parameters.RowCount,
                _leaderboardCollectionMapping[parameters.Collection],
                _leaderboardTimeSpanMapping[parameters.TimeSpan]);

            if (result == null)
            {
                DebugUtils.LogError($"Failed to load leaderboard data for leaderboard id '{parameters.LeaderboardId}'. Result is null.");
                return null;
            }
            var commonResults = await ConvertToCommonResults(result);
            return commonResults;
        }

        public async UniTask<ILeaderboardDataRequestResults> PreviousPage(LeaderboardPagingParameters parameters)
        {
            if (parameters.RawResults is not GPGSLeaderboardDataRequestResults gpgsResults)
            {
                DebugUtils.LogError($"Invalid leaderboard data request results for paging. Expected {typeof(GPGSLeaderboardDataRequestResults)}.");
                return null;
            }
            var result = await _leaderboard.LoadMoreScores(gpgsResults.LeaderBoardScoreData.PrevPageToken, parameters.RowCount);
            var commonResults = await ConvertToCommonResults(result);
            return commonResults;
        }

        public async UniTask<ILeaderboardDataRequestResults> NextPage(LeaderboardPagingParameters parameters)
        {
            if (parameters.RawResults is not GPGSLeaderboardDataRequestResults gpgsResults)
            {
                DebugUtils.LogError($"Invalid leaderboard data request results for paging. Expected {typeof(GPGSLeaderboardDataRequestResults)}.");
                return null;
            }
            var result = await _leaderboard.LoadMoreScores(gpgsResults.LeaderBoardScoreData.NextPageToken, parameters.RowCount);
            var commonResults = await ConvertToCommonResults(result);
            return commonResults;
        }

        private async UniTask<CommonLeaderboardDataRequestResults> ConvertToCommonResults(LeaderboardScoreData leaderboardScoreData)
        {
            var allUsers = leaderboardScoreData.Scores.Select(x => x.userID).ToArray();
            var entries = new List<(IUserDataProvider UserData, ScoreData ScoreData)>();
            if (allUsers.Length > 0)
            {
                var profileTcs = new UniTaskCompletionSource<PlayGamesUserProfile[]>();
                DebugUtils.Log($"User ID count for leaderboard scores: {allUsers.Length}. Loading user profiles...");
                PlayGamesPlatform.Instance.LoadUsers(allUsers, (profiles) =>
                {
                    profileTcs.TrySetResult(profiles.Cast<PlayGamesUserProfile>().ToArray());
                });
                DebugUtils.Log("Loading user profiles for leaderboard scores...");
                var userProfiles = await profileTcs.Task;
                DebugUtils.Log("Load user profiles completed.");
                for (var i = 0; i < leaderboardScoreData.Scores.Length; i++)
                {
                    var user = userProfiles[i];
                    var score = leaderboardScoreData.Scores[i];
                    var userData = new UserData
                    {
                        DisplayName = user.userName,
                        UserId = user.id,
                        AvatarUrl = user.AvatarURL
                    };
                    var scoreData = new ScoreData
                    {
                        Rank = score.rank,
                        RawValue = score.value,
                        FormattedValue = score.formattedValue,
                        Timestamp = score.date
                    };
                    entries.Add((userData, scoreData));
                }
            }
            
            var requestResults = new CommonLeaderboardDataRequestResults
            {
                LeaderboardTitle = leaderboardScoreData.Title,
                Entries = entries,
                RawResults = new GPGSLeaderboardDataRequestResults
                {
                    LeaderBoardScoreData = leaderboardScoreData
                }
            };
            DebugUtils.Log("Successfully converted leaderboard score data to common results.");
            return requestResults;
        }
    }
}
#endif