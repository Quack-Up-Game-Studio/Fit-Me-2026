using Cysharp.Threading.Tasks;
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using QuackUp.Utils;

namespace QuackUp.GPGS
{
    public class GPGSLeaderboard
    {
        public async UniTask<bool> ReportScore(string leaderboardId, long score, string metadata = null)
        {
            var tcs = new UniTaskCompletionSource<bool>();
            PlayGamesPlatform.Instance.ReportScore(score, leaderboardId, metadata, (success) =>
            {
                tcs.TrySetResult(success);
            });
            var result = await tcs.Task;
            DebugUtils.Log($"Report score: {score} to leaderboard: {leaderboardId} with metadata: {metadata}. Success: {result}");
            return result;
        }
        
        public void ShowLeaderboardUI(string leaderboardId)
        {
            PlayGamesPlatform.Instance.ShowLeaderboardUI(leaderboardId);
        }

        public async UniTask<LeaderboardScoreData> LoadLeaderboardData(string leaderboardId, LeaderboardStart start,
            int rowCount, LeaderboardCollection collection, LeaderboardTimeSpan timeSpan)
        {
            if (!PlayGamesPlatform.Instance.IsAuthenticated()) return null;
            
            var tcs = new UniTaskCompletionSource<LeaderboardScoreData>();
            
            if (collection is LeaderboardCollection.Social)
            {
                var loadFriendsTcs = new UniTaskCompletionSource<bool>();
                PlayGamesPlatform.Instance.LoadFriends(PlayGamesPlatform.Instance.localUser, b =>
                {
                    loadFriendsTcs.TrySetResult(b);
                });
                await loadFriendsTcs.Task;
                var status = PlayGamesPlatform.Instance.GetLastLoadFriendsStatus();
                DebugUtils.Log($"Load friends data status: {status}");
                if ((int)status < 0) // Negative value indicates an error occurred while loading friends data
                {
                    if (status is LoadFriendsStatus.ResolutionRequired)
                    {
                        var resolutionTcs = new UniTaskCompletionSource<UIStatus>();
                        PlayGamesPlatform.Instance.AskForLoadFriendsResolution((resolutionResult) =>
                        {
                            resolutionTcs.TrySetResult(resolutionResult);
                        });
                        var resolutionResult = await resolutionTcs.Task;
                        if (resolutionResult != UIStatus.Valid)
                        {
                            DebugUtils.Log("Failed to resolve friends data loading issue. Status: " + resolutionResult);
                            tcs.TrySetResult(null);
                            return await tcs.Task;
                        }
                    }
                    DebugUtils.LogWarning("Failed to load leaderboard data. Status: " + status);
                    tcs.TrySetResult(null);
                    return await tcs.Task;
                }
            }
            
            DebugUtils.Log($"Loading leaderboard data for leaderboardId: {leaderboardId}, start: {start}, rowCount: {rowCount}, collection: {collection}, timeSpan: {timeSpan}");
            PlayGamesPlatform.Instance.LoadScores(
                leaderboardId,
                start,
                rowCount,
                collection,
                timeSpan,
                (data) =>
                {
                    tcs.TrySetResult(data);
                });
            var result = await tcs.Task;
            DebugUtils.Log($"Leaderboard response status: {result.Status}");
            if (result.Status > 0) return result;
            DebugUtils.LogWarning($"Failed to load leaderboard data for leaderboardId: {leaderboardId}. Status: {result.Status}");
            return null;
        }

        public UniTask<LeaderboardScoreData> LoadMoreScores(ScorePageToken pageToken, int rowCount)
        {
            var tcs = new UniTaskCompletionSource<LeaderboardScoreData>();
            PlayGamesPlatform.Instance.LoadMoreScores(pageToken, rowCount,
                (results) =>
                {
                    tcs.TrySetResult(results);
                });
            return tcs.Task;
        }
    }
}