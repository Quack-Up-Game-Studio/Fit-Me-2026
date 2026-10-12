using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine.SocialPlatforms;
using QuackUp.SocialService;
using UnityEngine;

namespace QuackUp.iOSSocial
{
    public class iOSLeaderboardService : ILeaderboardService
    {
        public UniTask<bool> ReportData(LeaderboardReportParameters parameters)
        {
            Social.ReportScore(parameters.Data, parameters.LeaderboardId, success => { });
            return UniTask.FromResult(true);
        }

        public UniTask<bool> ShowLeaderboardUI(string leaderboardId)
        {
            Social.ShowLeaderboardUI();
            return UniTask.FromResult(true);
        }

        public UniTask<ILeaderboardDataRequestResults> RequestLeaderboardData(LeaderboardDataRequestParameters parameters)
        {
            return UniTask.FromResult<ILeaderboardDataRequestResults>(null);
        }

        public UniTask<ILeaderboardDataRequestResults> PreviousPage(LeaderboardPagingParameters parameters)
        {
            return UniTask.FromResult<ILeaderboardDataRequestResults>(null);
        }

        public UniTask<ILeaderboardDataRequestResults> NextPage(LeaderboardPagingParameters parameters)
        {
            return UniTask.FromResult<ILeaderboardDataRequestResults>(null);
        }

        public UniTask<(IUserDataProvider UserData, ScoreData scoreData)> RequestPersonalLeaderboardData(LeaderboardDataRequestParameters parameters)
        {
            return UniTask.FromResult<(IUserDataProvider UserData, ScoreData scoreData)>(
                (new UserData { UserId = Social.localUser.id, DisplayName = Social.localUser.userName },
                 new ScoreData { RawValue = 0, FormattedValue = "0", Rank = 0, Timestamp = DateTime.UtcNow }));
        }
    }
}
