using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using QuackUp.Utils;
using MessagePack;
using MessagePack.Resolvers;
using Semver;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace QuackUp.Save
{
    [Union(0, typeof(TestMessagePackSaveData))]
    public interface IMessagePackSaveData
    {
        public string Version { get; set; }
    }
    
    public interface ISaveMigrationResolver<out T>
        where T : IMessagePackSaveData
    {
        string SourceVersion { get; }
        string TargetVersion { get; }
        ExpandoObject Migrate(ExpandoObject fullResolve);
        T Finalize(ExpandoObject expando);
    }

    public abstract class MessagePackSaveObject : SerializedScriptableObject
    {
        [Title("Settings")] 
        [InfoBox("Settings mark in red are destructive after release.\n" +
                 "Changing these settings will make existing saves incompatible!\n" +
                 "Make sure they are definite before release.", InfoMessageType.Warning),
         HideLabel,
         ShowInInspector] private InspectorPlaceholder _warning;
        [field: GUIColor("red"),
            SerializeField] public bool SaveSeparately { get; private set; } = false;
        [GUIColor("red"), 
         SerializeField] protected bool saveAsJson = false;
        [ShowIfGroup(nameof(SaveSeparately)),
            SerializeField] protected bool debugMode = true;
        [ShowIfGroup(nameof(SaveSeparately)),
         ShowIf(nameof(debugMode)),
         SerializeField] protected SaveSettings debugSaveSettings;
        [ShowIfGroup(nameof(SaveSeparately)),
         HideIf(nameof(debugMode)), GUIColor("red"),
         SerializeField] protected SaveSettings releaseSaveSettings;
        
        public abstract T GetSaveData<T>() where T : IMessagePackSaveData;

        public abstract bool TrySerializeSaveData(out byte[] bytes);
        
        public abstract void Save();
        
        public abstract void Load();

        public abstract void Reset();
        
        public abstract void LoadFromBytes(byte[] bytes);
        
        public SaveSettings CurrentSaveSettings
        {
            get
            { 
#if UNITY_EDITOR
                return debugMode ? debugSaveSettings : releaseSaveSettings; 
#else
                return releaseSaveSettings; 
#endif
            }
        }
    }
    
    public abstract class MessagePackSaveObject<T> : MessagePackSaveObject
        where T : IMessagePackSaveData
    {
        [Title("Save Management")]
        [OdinSerialize] protected T saveData;
        [OdinSerialize] protected List<ISaveMigrationResolver<T>> migrationResolvers = new();

        public override TDerived GetSaveData<TDerived>()
        {
            if (saveData is TDerived derivedData)
            {
                return derivedData;
            }
            throw new InvalidCastException($"Cannot cast save data of type {typeof(T)} to {typeof(TDerived)}.");
        }

        [Title("Debug"), 
         HideLabel,
         ShowInInspector] private InspectorPlaceholder _debugTitle;
        [ButtonGroup("Save&Load")]
        [Button("Test Save", ButtonSizes.Large)]
        protected virtual void TestSave()
        {
            DebugSave();
        }
        
        [ButtonGroup("Save&Load")]
        [Button("Test Load", ButtonSizes.Large)]
        protected virtual void TestLoad()
        {
            DebugLoad();
        }
        
        [ButtonGroup("Save&Load")]
        [Button("Test Reset", ButtonSizes.Large)]
        protected virtual void TestReset()
        {
            Reset();
        }
        
        [ButtonGroup("JSON")]
        [Button("Export JSON", ButtonSizes.Large)]
        protected void ExportJson()
        {
#if UNITY_EDITOR
            var path = UnityEditor.EditorUtility.SaveFilePanel("Export Save Data to JSON", "", 
                $"{CurrentSaveSettings.saveFileName}_export", "json");
            if (string.IsNullOrEmpty(path)) return;
#else
            var path = Path.Combine(Application.persistentDataPath,
                $"{CurrentSaveSettings.saveFileName}_export.json");
#endif
            var json = MessagePackSerializer.ConvertToJson(
                MessagePackSerializer.Serialize(saveData, ContractlessStandardResolver.Options));
            var beautifiedJson = JsonUtils.BeautifyJson(json);
            var jsonPath = Path.ChangeExtension(path, "json");
            File.WriteAllText(jsonPath, beautifiedJson);
            Debug.Log($"Dumped save data to JSON at: {jsonPath}");
        }
        
        [ButtonGroup("JSON")]
        [Button("Import JSON", ButtonSizes.Large)]
        protected void ImportJson()
        {
#if UNITY_EDITOR
            var path = UnityEditor.EditorUtility.OpenFilePanel("Select JSON Save File", "", "json");
            if (string.IsNullOrEmpty(path)) return;
            var json = File.ReadAllText(path);
            var bytes = MessagePackSerializer.ConvertFromJson(json);
            LoadFromBytes(bytes);
#else
            Debug.LogWarning("ImportJson is only available in the Unity Editor.");
#endif
        }
        
        public override bool TrySerializeSaveData(out byte[] bytes)
        {
            bytes = null;
            if (!SemVersion.TryParse(Application.version, out _))
            {
                Debug.LogError("Application version is not in valid SemVer format. Aborting serialization.");
                return false;
            }
            saveData.Version = Application.version;
            try
            {
                bytes = MessagePackSerializer.Serialize(saveData, ContractlessStandardResolver.Options);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to serialize save data: {e.Message}");
                return false;
            }
            return true;
        }

        public override void Save() 
        {
            if (!SaveSeparately)
            {
                Debug.LogError("SaveSeparately is false. Save operation must be handled by Save Manager.");
                return;
            } 
            SaveInternal();
        }
        
        private void DebugSave()
        {
            if (SaveSeparately)
            {
                SaveInternal();
                return;
            }
            DebugSaveManager.Instance.SaveManager.Save(this);
        }

        private void SaveInternal()
        {
            if (!TrySerializeSaveData(out var bytes))
            {
                Debug.LogError("Failed to serialize save data. Save operation aborted.");
                return;
            }
            WriteToFile(bytes);
        }
        
        public override void Load()
        {
            if (!SaveSeparately)
            {
                Debug.LogError("SaveSeparately is false. Load operation must be handled by Save Manager.");
                return;
            } 
            LoadInternal();
        }
        
        private void DebugLoad()
        {
            if (SaveSeparately)
            {
                LoadInternal();
                return;
            }
            DebugSaveManager.Instance.SaveManager.Load(this);
        }
        
        private void LoadInternal()
        {
            var bytes = ReadFromFile();
            if (bytes == null)
            {
                Debug.LogError("No save file found to load.");
                return;
            }
            LoadFromBytes(bytes);
        }
        
        public override void Reset() { } // Implement reset logic in derived classes if needed
        
        public override void LoadFromBytes(byte[] bytes)
        {
            T deserializedSave;
            try
            {
                deserializedSave = MessagePackSerializer.Deserialize<T>(bytes, ContractlessStandardResolver.Options);
            } 
            catch (Exception ex)
            {
                Debug.LogError($"Failed to deserialize save data: {ex.Message}");
                return;
            }
            SemVersion.TryParse(saveData.Version, out var currentVersion);
            SemVersion.TryParse(deserializedSave.Version, out var deserializedVersion);
            if (SemVersion.ComparePrecedence(currentVersion, deserializedVersion) == 0)
            {
                saveData = deserializedSave;
                Debug.Log("Save version matches current version. No migration needed.");
                return;
            }
            if (TryMigrateSave(deserializedSave.Version, saveData.Version, bytes, out var migratedSave))
            {
                saveData = migratedSave;
                Debug.Log("Save data loaded successfully.");
            }
            else
            {
                saveData = deserializedSave;
                Debug.LogWarning("Failed to migrate save data. Using deserialized data as fallback.");
            }
        }

        protected virtual bool TryMigrateSave(string from, string to, byte[] bytes, out T migratedSave)
        {
            migratedSave = default;
            Debug.Log($"Save version ({from}) is different from current version ({to}). Attempting migration.");
            if (!TryFindShortestMigrationPath(from, to, out var path))
            {
                Debug.LogWarning($"No migration path found from version {from} to {to}. Migration aborted.");
                return false;
            }
            ExpandoObject expando;
            try
            {
                expando = MessagePackSerializer.Deserialize<ExpandoObject>(bytes, ExpandoObjectResolver.Options);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to deserialize save data to ExpandoObject for migration: {ex.Message}");
                return false;
            }
            foreach (var resolver in path)
            {
                expando = resolver.Migrate(expando);
                Debug.Log($"Migration successful from version {resolver.SourceVersion} to {resolver.TargetVersion}.");
            }
            migratedSave = path.Last().Finalize(expando);
            Debug.Log("All migrations completed.");
            return true;
        }

        private bool TryFindShortestMigrationPath(string sourceVersion, string targetVersion, out List<ISaveMigrationResolver<T>> path)
        {
            path = new List<ISaveMigrationResolver<T>>();
            if (migrationResolvers == null)
                throw new ArgumentNullException(nameof(migrationResolvers));

            if (sourceVersion == null)
                throw new ArgumentNullException(nameof(sourceVersion));

            if (targetVersion == null)
                throw new ArgumentNullException(nameof(targetVersion));

            if (sourceVersion == targetVersion)
                return false; // No migration needed

            // Build adjacency list
            var graph = migrationResolvers
                .GroupBy(r => r.SourceVersion)
                .ToDictionary(g => g.Key, g => g.ToList());

            // BFS setup
            var queue = new Queue<MigrationPath>();
            var visited = new Dictionary<string, MigrationPath>();

            var initialPath = new MigrationPath(sourceVersion, new List<ISaveMigrationResolver<T>>());
            queue.Enqueue(initialPath);
            visited[sourceVersion] = initialPath;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                if (!graph.TryGetValue(current.Version, out var value1))
                    continue;

                foreach (var resolver in value1)
                {
                    string nextVersion = resolver.TargetVersion;

                    // Skip if we've found a better path to this version already
                    if (visited.ContainsKey(nextVersion) &&
                        visited[nextVersion].Path.Count <= current.Path.Count + 1)
                    {
                        continue;
                    }

                    var newPath = new List<ISaveMigrationResolver<T>>(current.Path) { resolver };
                    var newMigrationPath = new MigrationPath(nextVersion, newPath);

                    visited[nextVersion] = newMigrationPath;

                    if (nextVersion == targetVersion)
                    {
                        // We found a path, but continue to check if there's a shorter one
                        continue;
                    }

                    queue.Enqueue(newMigrationPath);
                }
            }
            if (!visited.TryGetValue(targetVersion, out var pathData))
                return false;
            path = pathData.Path;
            return true;
        }

        protected virtual void WriteToFile(byte[] bytes)
        {
            var fullPath = CurrentSaveSettings.GetFullSavePath();
            if (!Directory.Exists(Path.GetDirectoryName(fullPath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            }
            if (saveAsJson) 
            {
                var json = MessagePackSerializer.ConvertToJson(bytes);
                var beautifiedJson = JsonUtils.BeautifyJson(json);
                File.WriteAllText(fullPath, beautifiedJson);
                return;
            }
            File.WriteAllBytes(fullPath, bytes);
        }
        
        protected virtual byte[] ReadFromFile()
        {
            var fullPath = CurrentSaveSettings.GetFullSavePath();
            if (!saveAsJson) return File.Exists(fullPath) ? File.ReadAllBytes(fullPath) : null;
            if (!File.Exists(fullPath)) return null;
            var json = File.ReadAllText(fullPath);
            return MessagePackSerializer.ConvertFromJson(json);
        }
        
        // Helper class to track paths during BFS
        private class MigrationPath
        {
            public string Version { get; }
            public List<ISaveMigrationResolver<T>> Path { get; }
    
            public MigrationPath(string version, List<ISaveMigrationResolver<T>> path)
            {
                Version = version;
                Path = path;
            }
        }
    }
}