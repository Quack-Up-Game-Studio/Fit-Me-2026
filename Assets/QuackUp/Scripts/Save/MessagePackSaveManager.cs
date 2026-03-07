using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using QuackUp.Utils;
using R3;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using CompressionLevel = System.IO.Compression.CompressionLevel;

namespace QuackUp.Save
{

    [Serializable]
    public class MessagePackSaveManager : IInitializable
    {
        private readonly MessagePackSaveConfig _config;
        [OdinSerialize, ReadOnly] private Dictionary<string, MessagePackSaveObject> _saveObjects = new();

        public Observable<bool> OnSaveResult => _onSaveResult;
        private readonly Subject<bool> _onSaveResult = new();
        public Observable<bool> OnLoadResult => _onLoadResult;
        private readonly Subject<bool> _onLoadResult = new();
        
        [Button("Test Save All")]
        private void TestSaveAll()
        {
            SaveAll();
        }

        [Button("Test Load All")]
        private void TestLoadAll()
        {
            LoadAll();
        }
        
        [Button("Test Reset All")]
        private void TestResetAll()
        {
            ResetAll();
        }
        
        [Inject]
        public MessagePackSaveManager(MessagePackSaveConfig config)
        {
            _config = config;
            foreach (var saveObject in _config.InitialSaveObjects)
            {
                RegisterSaveObject(saveObject.Key, saveObject.Value);
            }
        }

        public void Initialize()
        {
            if (!_config.LoadAtStart) return;
            Debug.Log("SaveManager: Loading all save objects at start.");
            foreach (var messagePackSaveObject in _saveObjects.Values)
            {
                messagePackSaveObject.OnInitialize();
                messagePackSaveObject.Reset();
            }
            LoadAll();
        }
        
        
        public void RegisterSaveObject(string key, MessagePackSaveObject saveObject)
        {
            _saveObjects[key] = saveObject;
        }
        
        public void UnregisterSaveObject(string key)
        {
            _saveObjects.Remove(key);
        }
        
        public MessagePackSaveObject GetSaveObject(string key)
        {
            if (_saveObjects.TryGetValue(key, out var saveObject))
            {
                return saveObject;
            }
            Debug.LogError($"Save object with key {key} not found.");
            return null;
        }
        
        public T GetSaveObject<T>(string key) where T : MessagePackSaveObject
        {
            if (_saveObjects.TryGetValue(key, out var saveObject))
            {
                if (saveObject is T typedSaveObject)
                {
                    return typedSaveObject;
                }
                Debug.LogError($"Save object with key {key} is not of type {typeof(T)}.");
                return null;
            }
            Debug.LogError($"Save object with key {key} not found.");
            return null;
        }

        public T GetFirstSaveObjectOfType<T>() where T : MessagePackSaveObject
        {
            var type = typeof(T);
            DebugUtils.Log($"_saveObjects count: {_saveObjects.Count}");
            foreach (var saveObject in _saveObjects.Values)
            {
                if (saveObject is T typedSaveObject)
                {
                    return typedSaveObject;
                }
            }
            Debug.LogError($"Save object of type {type} not found.");
            return null;
        }

        public T[] GetAllSaveObjectsOfType<T>() where T : MessagePackSaveObject
        {
            var type = typeof(T);
            var result = new List<T>();
            foreach (var saveObject in _saveObjects.Values)
            {
                if (saveObject is T typedSaveObject)
                {
                    result.Add(typedSaveObject);
                }
            }
            if (result.Count == 0)
            {
                Debug.LogError($"No save objects of type {type} found.");
            }
            return result.ToArray();
        }
        
        public void LoadAll()
        {
            foreach (var saveObject in _saveObjects.Values)
            {
                Load(saveObject);
            }
        }

        public void SaveAll()
        {
            foreach (var saveObject in _saveObjects.Values)
            {
                Save(saveObject);
            }
        }
        
        public void ResetAll()
        {
            foreach (var saveObject in _saveObjects.Values)
            {
                Reset(saveObject);
            }
        }
        
        public void Save(string key)
        {
            var saveObject = GetSaveObject(key);
            if (!saveObject) return;
            Save(saveObject);
        }

        public void Save(MessagePackSaveObject saveObject)
        {
            if (!saveObject)
                return;
            if (saveObject.SaveSeparately)
            {
                saveObject.Save();
                return;
            }
            var entryName = _saveObjects.FirstOrDefault(x => x.Value == saveObject).Key;
            if (string.IsNullOrEmpty(entryName))
            {
                Debug.LogError("Save object not registered in the save manager.");
                return;
            }
            if (!saveObject.TrySerializeSaveData(out var data))
            {
                return;
            }
            ZipAndSave(entryName, data);
        }
        
        public void Load(string key)
        {
            var saveObject = GetSaveObject(key);
            Load(saveObject);
        }

        public void Load(MessagePackSaveObject saveObject)
        {
            var data = LoadBytes(saveObject);
            if (data != null)
            {
                saveObject.LoadFromBytes(data);
            }
        }
        
        public byte[] LoadBytes(string key)
        {
            var saveObject = GetSaveObject(key);
            return LoadBytes(saveObject);
        }

        public byte[] LoadBytes(MessagePackSaveObject saveObject)
        {
            if (!saveObject)
                return null;
            if (saveObject.SaveSeparately)
            {
                return saveObject.ReadByte();
            }
            var entryName = _saveObjects.FirstOrDefault(x => x.Value == saveObject).Key;
            if (string.IsNullOrEmpty(entryName))
            {
                Debug.LogError("Save object not registered in the save manager.");
                return null;
            }
            return LoadEntryFromZip(entryName);
        }
        
        public void Reset(string key)
        {
            var saveObject = GetSaveObject(key);
            Reset(saveObject);
        }

        public void Reset(MessagePackSaveObject saveObject)
        {
            if (!saveObject) 
                return;
            saveObject.Reset();
        }
        
        private void ZipAndSave(string entryName, byte[] data)
        {
            var zipPath = Path.ChangeExtension(_config.CurrentSaveSettings.GetFullSavePath(), ".sav");
            
            try
            {
                if (!Directory.Exists(zipPath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(zipPath)!);
                }
                using (var zipArchive = ZipFile.Open(zipPath, ZipArchiveMode.Update))
                {
                    var finalName = Path.ChangeExtension(entryName, ".bin");
                    if (zipArchive.GetEntry(finalName) != null)
                    {
                        zipArchive.GetEntry(finalName)?.Delete();
                    }
                    var entry = zipArchive.CreateEntry(finalName, CompressionLevel.Optimal);
                    using var entryStream = entry.Open();
                    using var writer = new BinaryWriter(entryStream);
                    writer.Write(data);
                }
                Debug.Log($"ZIP file created successfully: {zipPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error creating ZIP file: {ex.Message}");
                throw;
            }
        }
        
        private byte[] LoadEntryFromZip(string entryName)
        {
            var zipPath = Path.ChangeExtension(_config.CurrentSaveSettings.GetFullSavePath(), ".sav");
            
            if (!File.Exists(zipPath))
                return null;
            
            try
            {
                using var zipArchive = ZipFile.OpenRead(zipPath);
                var finalName = Path.ChangeExtension(entryName, ".bin");
                var entry = zipArchive.GetEntry(finalName);
                if (entry == null)
                    return null;
                using var entryStream = entry.Open();
                using var reader = new BinaryReader(entryStream);
                Debug.Log($"ZIP file loaded successfully: {zipPath}");
                return reader.ReadBytes((int)entry.Length);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error loading ZIP file: {ex.Message}");
                throw;
            }
        }
        
        public byte[] GetZipBytes()
        {
            var zipPath = Path.ChangeExtension(_config.CurrentSaveSettings.GetFullSavePath(), ".sav");
            return !File.Exists(zipPath) ? null : File.ReadAllBytes(zipPath);
        }

        public void LoadFromZipBytes(byte[] zipBytes)
        {
            var deserializedData = DeserializeSaveDataFromZipBytes(zipBytes);
            LoadFromDeserializedData(deserializedData);
        }

        public void LoadFromDeserializedData(DeserializedSaveData deserializedSaveData)
        {
            foreach (var kvp in deserializedSaveData.SaveData)
            {
                var entryName = kvp.Key;
                var saveData = kvp.Value;
                var saveObject = GetSaveObject(entryName);
                if (!saveObject)
                {
                    Debug.LogWarning($"No save object found for entry {entryName} in deserialized data.");
                    continue;
                }
                saveObject.LoadFromDeserializedData(saveData);
            }
        }

        public DeserializedSaveData DeserializeSaveDataFromZipBytes(byte[] zipBytes)
        {
            var deserializedData = new DeserializedSaveData();
            try
            {
                using var memoryStream = new MemoryStream(zipBytes);
                using var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Read);
                foreach (var entry in zipArchive.Entries)
                {
                    var entryName = Path.GetFileNameWithoutExtension(entry.Name);
                    var saveObject = GetSaveObject(entryName);
                    if (!saveObject)
                    {
                        Debug.LogWarning($"No save object found for entry {entryName} in ZIP.");
                        continue;
                    }

                    using var entryStream = entry.Open();
                    using var reader = new BinaryReader(entryStream);
                    var data = reader.ReadBytes((int)entry.Length);
                    var deserializedSaveData = saveObject.TryDeserializeSaveData(data);
                    if (deserializedSaveData == null)
                    {
                        Debug.LogWarning($"Failed to deserialize save data for entry {entryName} in ZIP.");
                        continue;
                    }

                    deserializedData.SaveData[entryName] = deserializedSaveData;
                }

                Debug.Log("ZIP bytes deserialized successfully.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error deserializing ZIP bytes: {ex.Message}");
                throw;
            }

            return deserializedData;
        }
    }
}