using System;
using Cysharp.Threading.Tasks;
using QuackUp.Save;
using QuackUp.Utils;

namespace FitMe.GameData
{
    [Serializable]
    public class PlayerIdConflictSolutionProvider : ISaveConflictSolutionProvider
    {
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
            
            var localId = localPlayerData.PlayerID;
            var remoteId = remotePlayerData.PlayerID;
            DebugUtils.Log($"Local ID: {localId}. Remote ID: {remoteId}.");
            if (localId.Equals(remoteId)) return UniTask.FromResult(ConflictSolution.PassThrough);
            // Take remote if IDs are different, as it's more likely that the local save data is corrupted (e.g. due to a failed write) if the player ID is different.
            return UniTask.FromResult(ConflictSolution.UseRemote);
        }
    }
}