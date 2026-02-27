using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace QuackUp.Save
{
    public enum ConflictSolution
    {
        /// <summary>
        /// Use the local save data, which is the data on the current device. This will overwrite the remote save data on the cloud server.
        /// </summary>
        UseLocal,
        /// <summary>
        /// Use the remote save data, which is the data on the cloud server. This will overwrite the local save data.
        /// </summary>
        UseRemote,
        /// <summary>
        /// Pass through to the next conflict resolver in the list.
        /// </summary>
        PassThrough,
        /// <summary>
        /// Abort the loading process altogether.
        /// This usually signifies that both local and remote save are considered invalid, and the game should start with a fresh save data.
        /// </summary>
        Abort
    }

    public record DeserializedSaveData
    {
        public readonly Dictionary<string, IMessagePackSaveData> SaveData = new();
        
        public bool TryGetFirstSaveDataOfType<T>(out T data) where T : IMessagePackSaveData
        {
            data = default;
            foreach (var saveData in SaveData.Values)
            {
                if (saveData is T typedSaveData)
                {
                    data = typedSaveData;
                    return true;
                }
            }
            return false;
        }
        
        public bool TryGetAllSaveDataOfType<T>(out T[] data) where T : IMessagePackSaveData
        {
            var list = new List<T>();
            foreach (var saveData in SaveData.Values)
            {
                if (saveData is T typedSaveData)
                {
                    list.Add(typedSaveData);
                }
            }
            data = list.ToArray();
            return data.Length > 0;
        }
        
        public bool TryGetSaveDataByKey<T>(string key, out T data) where T : IMessagePackSaveData
        {
            data = default;
            if (!SaveData.TryGetValue(key, out var saveData))
                return false;
            if (saveData is not T typedSaveData) return false;
            data = typedSaveData;
            return true;
        }
    }
    
    public interface ISaveConflictSolutionProvider
    {
        UniTask<ConflictSolution> GetConflictSolutionAsync(DeserializedSaveData local, DeserializedSaveData remote);
    }
}