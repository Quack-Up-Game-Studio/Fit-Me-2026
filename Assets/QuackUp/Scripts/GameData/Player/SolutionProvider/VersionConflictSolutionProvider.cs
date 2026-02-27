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
    public class VersionConflictSolutionProvider : ISaveConflictSolutionProvider
    {
        public enum VersionConflictSolution
        {
            UseLatest,
            UseOldest,
            UseSpecific
        }

        [SerializeField] private VersionConflictSolution versionConflictSolution;
        [SerializeField, 
         ShowIf(nameof(versionConflictSolution), VersionConflictSolution.UseSpecific)] 
        private string specificVersion = string.Empty;
        
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

            var localVersionNull = !SemVersion.TryParse(localPlayerData.Version, out var localVersion);
            var remoteVersionNull = !SemVersion.TryParse(remotePlayerData.Version, out var remoteVersion);
            if (localVersionNull || remoteVersionNull)
            {
                DebugUtils.LogError($"Local version is null: {localVersionNull}. Remote version is null: {remoteVersionNull}. Passing through.");
                return UniTask.FromResult(ConflictSolution.PassThrough);
            }

            DebugUtils.Log($"Local version: {localVersion}. Remote version: {remoteVersion}.");
            if (versionConflictSolution is VersionConflictSolution.UseSpecific)
            {
                var specificVersionNull = !SemVersion.TryParse(specificVersion, out var specificVersionSemver);
                if (specificVersionNull)
                {
                    DebugUtils.LogError("Specific version doesn't follow the semver rule! Passing through.");
                    return UniTask.FromResult(ConflictSolution.PassThrough);
                }
                var localVersionMatched = localVersion == specificVersionSemver;
                var remoteVersionMatched = remoteVersion == specificVersionSemver;
                if (localVersionMatched && remoteVersionMatched) return UniTask.FromResult(ConflictSolution.PassThrough);
                if (remoteVersionMatched) return UniTask.FromResult(ConflictSolution.UseRemote);
                if (localVersionMatched) return UniTask.FromResult(ConflictSolution.UseLocal);
            }
            
            var precedence = SemVersion.ComparePrecedence(localVersion, remoteVersion);
            switch (versionConflictSolution)
            {
                case VersionConflictSolution.UseLatest:
                    return precedence switch
                    {
                        0 => UniTask.FromResult(ConflictSolution.PassThrough),
                        > 0 => UniTask.FromResult(ConflictSolution.UseLocal), //local is newer than remote
                        < 0 => UniTask.FromResult(ConflictSolution.UseRemote) //local is older than remote
                    };
                case VersionConflictSolution.UseOldest:
                    return precedence switch
                    {
                        0 => UniTask.FromResult(ConflictSolution.PassThrough),
                        > 0 => UniTask.FromResult(ConflictSolution.UseRemote), //local is newer than remote
                        < 0 => UniTask.FromResult(ConflictSolution.UseLocal) //local is older than remote
                    };
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}