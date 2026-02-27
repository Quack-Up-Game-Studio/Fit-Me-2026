using System;
using Cysharp.Threading.Tasks;
using QuackUp.Save;
using QuackUp.Utils;
using Semver;
using Sirenix.OdinInspector;
using UnityEngine;

namespace FitMe.GameData
{
    [Serializable]
    public class PlaytimeConflictSolutionProvider : ISaveConflictSolutionProvider
    {
        public enum PlaytimeConflictSolution
        {
            UseLongest,
            UseShortest,
        }

        [SerializeField] private PlaytimeConflictSolution playtimeConflictSolution;
        
        public UniTask<ConflictSolution> GetConflictSolutionAsync(DeserializedSaveData local, DeserializedSaveData remote)
        {
            var localNull = local == null;
            var remoteNull = remote == null;
            if (localNull || remoteNull)
            {
                DebugUtils.LogError($"Deserialized local save is null: {localNull}. Deserialized remote save is null: {remoteNull}. Passing through.");
                return UniTask.FromResult(ConflictSolution.PassThrough);
            }

            var localPlayerNull = !local.TryGetFirstSaveDataOfType<PlayerRecordSaveData>(out var localPlayerData);
            var remotePlayerNull = !remote.TryGetFirstSaveDataOfType<PlayerRecordSaveData>(out var remotePlayerData);
            if (localPlayerNull || remotePlayerNull)
            {
                DebugUtils.LogError($"Local player data is null: {localPlayerNull}. Remote player data is null: {remotePlayerNull}. Passing through.");
                return UniTask.FromResult(ConflictSolution.PassThrough);
            }
            
            var localPlaytime = localPlayerData.TotalPlayTime;
            var remotePlaytime = remotePlayerData.TotalPlayTime;
            DebugUtils.Log($"Local playtime: {localPlaytime:g}. Remote playtime: {remotePlaytime:g}.");
            if (localPlaytime == remotePlaytime) return UniTask.FromResult(ConflictSolution.PassThrough);
            switch (playtimeConflictSolution)
            {
                case PlaytimeConflictSolution.UseLongest:
                    return UniTask.FromResult(localPlaytime > remotePlaytime ? ConflictSolution.UseLocal : ConflictSolution.UseRemote);
                case PlaytimeConflictSolution.UseShortest:
                    return UniTask.FromResult(localPlaytime < remotePlaytime ? ConflictSolution.UseLocal : ConflictSolution.UseRemote);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}