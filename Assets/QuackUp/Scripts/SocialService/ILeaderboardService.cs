using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace QuackUp.SocialService
{
    public interface ILeaderboardService
    {
        UniTask<bool> ReportData(LeaderboardReportParameters parameters);
        UniTask<bool> ShowLeaderboardUI(string leaderboardId);
        UniTask<ILeaderboardDataRequestResults> RequestLeaderboardData(LeaderboardDataRequestParameters parameters);
        UniTask<ILeaderboardDataRequestResults> PreviousPage(LeaderboardPagingParameters parameters);
        UniTask<ILeaderboardDataRequestResults> NextPage(LeaderboardPagingParameters parameters);
    }
    
    public class MockLeaderboardService : ILeaderboardService
    {
        public UniTask<bool> ReportData(LeaderboardReportParameters parameters)
        {
            return UniTask.FromResult(true);
        }

        public UniTask<bool> ShowLeaderboardUI(string leaderboardId)
        {
            return UniTask.FromResult(true);
        }

        public UniTask<ILeaderboardDataRequestResults> RequestLeaderboardData(LeaderboardDataRequestParameters parameters)
        {
            return UniTask.FromResult<ILeaderboardDataRequestResults>(new CommonLeaderboardDataRequestResults
            {
                LeaderboardTitle = "Mock Leaderboard",
                Entries = new List<(IUserDataProvider UserData, ScoreData scoreData)>
                {
                    (new UserData { UserId = "1", DisplayName = "Player1" }, 
                        new ScoreData { RawValue = 1000, FormattedValue = "1,000", Rank = 1, Timestamp = DateTime.UtcNow }),
                    (new UserData { UserId = "2", DisplayName = "Player2" }, 
                        new ScoreData { RawValue = 800, FormattedValue = "800", Rank = 2, Timestamp = DateTime.UtcNow }),
                    (new UserData { UserId = "3", DisplayName = "Player3" }, 
                        new ScoreData { RawValue = 600, FormattedValue = "600", Rank = 3, Timestamp = DateTime.UtcNow }),
                }
            });
        }

        public UniTask<ILeaderboardDataRequestResults> PreviousPage(LeaderboardPagingParameters parameters)
        {
            return UniTask.FromResult<ILeaderboardDataRequestResults>(null);
        }

        public UniTask<ILeaderboardDataRequestResults> NextPage(LeaderboardPagingParameters parameters)
        {
            return UniTask.FromResult<ILeaderboardDataRequestResults>(null);
        }
    }
    
    #region Report Parameter implementations
    
    public class LeaderboardReportParameters
    {
        public string LeaderboardId { get; private set; }
        public long Data { get; private set; }
        public string Metadata { get; private set; }
        
        private LeaderboardReportParameters() { }
        
        public class Builder
        {
            private Builder() { }
            public static Builder CreateBuilder(string leaderboardId)
            {
                var builder = new Builder
                {
                    _parameters =
                    {
                        LeaderboardId = leaderboardId,
                    }
                };
                return builder;
            }
            private readonly LeaderboardReportParameters _parameters = new();

            public Builder WithData(long data)
            {
                _parameters.Data = data;
                return this;
            }

            public Builder WithMetadata(string metadata)
            {
                _parameters.Metadata = metadata;
                return this;
            }

            public LeaderboardReportParameters Build()
            {
                if (string.IsNullOrEmpty(_parameters.LeaderboardId))
                {
                    throw new InvalidOperationException("LeaderboardId is required.");
                }
                return _parameters;
            }
        }
    }
    #endregion

    #region Request Parameter implementations
    public class LeaderboardDataRequestParameters
    {
        #region GPGS-specific enums
        public enum LeaderboardStart
        {
            /// <summary>Start fetching scores from the top of the list.</summary>
            TopScores = 1,

            /// <summary>Start fetching relative to the player's score.</summary>
            PlayerCentered = 2,
        }
        
        /// <summary>Values specifying which leaderboard timespan to use.</summary>
        public enum LeaderboardTimeSpan
        {
            /// <summary>Daily scores.  The day resets at 11:59 PM PST.</summary>
            Daily = 1,

            /// <summary>Weekly scores.  The week resets at 11:59 PM PST on Sunday.</summary>
            Weekly = 2,

            /// <summary>All time scores.</summary>
            AllTime = 3,
        }

        /// <summary>Values specifying which leaderboard collection to use.</summary>
        public enum LeaderboardCollection
        {
            /// <summary>Public leaderboards contain the scores of players who are sharing their gameplay publicly.</summary>
            Public = 1,

            /// <summary>Social leaderboards contain the scores of players in the viewing player's circles.</summary>
            Social = 2,
        }
        #endregion
        
        public string LeaderboardId { get; private set; }
        public int RowCount { get; private set; }
        public LeaderboardStart Start { get; private set; }
        public LeaderboardCollection Collection { get; private set; }
        public LeaderboardTimeSpan TimeSpan { get; private set; }
        
        private LeaderboardDataRequestParameters() { }

        public class Builder
        {
            private Builder() { }
            public static Builder CreateBuilder(string leaderboardId, int rowCount)
            { 
                var builder = new Builder
                {
                    _parameters =
                    {
                        LeaderboardId = leaderboardId,
                        RowCount = rowCount,
                    }
                };
                return builder;
            }
            
            private readonly LeaderboardDataRequestParameters _parameters = new();
            
            public Builder WithStart(LeaderboardStart start)
            {
                _parameters.Start = start;
                return this;
            }
            
            public Builder WithCollection(LeaderboardCollection collection)
            {
                _parameters.Collection = collection;
                return this;
            }
            
            public Builder WithTimeSpan(LeaderboardTimeSpan timeSpan)
            {
                _parameters.TimeSpan = timeSpan;
                return this;
            }

            public LeaderboardDataRequestParameters Build()
            {
                if (string.IsNullOrEmpty(_parameters.LeaderboardId))
                {
                    throw new InvalidOperationException("LeaderboardId is required.");
                }
                if (_parameters.RowCount <= 0)
                {
                    throw new InvalidOperationException("RowCount must be greater than 0.");
                }
                return _parameters;
            }
        }
    }
    #endregion
    
    #region Paging Parameter implementations

    public class LeaderboardPagingParameters
    {
        /// <summary>
        /// Required, as it contains platform-specific data that is necessary for fetching the next or previous page of leaderboard results.
        /// </summary>
        public ILeaderboardDataRequestResults RawResults { get; set; }
        public int RowCount { get; private set; }
        
        private LeaderboardPagingParameters() { }
        
        public class Builder
        {
            private Builder() { }

            public static Builder CreateBuilder(ILeaderboardDataRequestResults rawResults, int rowCount)
            {
                var builder = new Builder
                {
                    _parameters =
                    {
                        RawResults = rawResults,
                        RowCount = rowCount
                    }
                };
                return builder;
            }
            private readonly LeaderboardPagingParameters _parameters = new();
            
            public LeaderboardPagingParameters Build()
            {
                if (_parameters.RawResults == null)
                {
                    throw new InvalidOperationException("RawResults is required.");
                }
                if (_parameters.RowCount <= 0)
                {
                    throw new InvalidOperationException("RowCount must be greater than 0.");
                }
                return _parameters;
            }
        }
        
    }
    #endregion

    #region Result implementations
    /// <summary>
    /// An interface for defining the results of a leaderboard data request.
    /// The specific data included in the results can vary based on the implementation and the requirements of the leaderboard system being used (e.g., Google Play Games, Apple Game Center, etc.).
    /// </summary>
    public interface ILeaderboardDataRequestResults{}

    public class ScoreData
    {
        public long RawValue { get; set; }
        public string FormattedValue { get; set; }
        public int Rank { get; set; }
        public DateTime Timestamp { get; set; }
    }
    
    public class CommonLeaderboardDataRequestResults : ILeaderboardDataRequestResults
    {
        public string LeaderboardTitle { get; set; }
        public IReadOnlyList<(IUserDataProvider UserData, ScoreData ScoreData)> Entries { get; set; }
        /// <summary>
        /// Raw results from the leaderboard data request, which may include platform-specific data that is not included in the common results.
        /// </summary>
        public ILeaderboardDataRequestResults RawResults { get; set; }
    }
    #endregion

}