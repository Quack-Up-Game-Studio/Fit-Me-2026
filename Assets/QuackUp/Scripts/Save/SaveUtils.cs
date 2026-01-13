using System;
using Sirenix.OdinInspector;
using Unity.Plastic.Newtonsoft.Json;
using Unity.Plastic.Newtonsoft.Json.Linq;

namespace QuackUp.Save
{
    public enum SaveLocation
    {
        PersistentDataPath,
        DataPath,
        Custom
    }
    
    [Serializable]
    public record SaveSettings
    {
        public SaveLocation saveLocation = SaveLocation.DataPath;
        public bool encryptSave;
        [ShowIf(nameof(encryptSave))] public string encryptionKey;
        public string saveDirectory = "TestSave";
        public string saveFileName = "testSave";
        
        [Button("Select Save Location")]
        private void SelectSaveLocation()
        {
#if UNITY_EDITOR
            var path = UnityEditor.EditorUtility.OpenFolderPanel("Select Save Location", "", "");
            if (string.IsNullOrEmpty(path)) return;
            if (path.StartsWith(UnityEngine.Application.dataPath))
            {
                saveLocation = SaveLocation.DataPath;
                saveDirectory = path.Substring(UnityEngine.Application.dataPath.Length)
                    .TrimStart(System.IO.Path.DirectorySeparatorChar)
                    .TrimStart(System.IO.Path.AltDirectorySeparatorChar);
            }
            else if (path.StartsWith(UnityEngine.Application.persistentDataPath))
            {
                saveLocation = SaveLocation.PersistentDataPath;
                saveDirectory = path.Substring(UnityEngine.Application.persistentDataPath.Length)
                    .TrimStart(System.IO.Path.DirectorySeparatorChar)
                    .TrimStart(System.IO.Path.AltDirectorySeparatorChar);
            }
            else
            {
                saveLocation = SaveLocation.Custom;
                saveDirectory = path;
            }
#else
            UnityEngine.Debug.LogWarning("SelectSaveLocation is only available in the Unity Editor.");
#endif
        }
        
        public string GetFullSavePath()
        {
            string basePath = saveLocation switch
            {
                SaveLocation.DataPath => UnityEngine.Application.dataPath,
                SaveLocation.PersistentDataPath => UnityEngine.Application.persistentDataPath,
                SaveLocation.Custom => string.Empty,
                _ => throw new ArgumentOutOfRangeException()
            };
            return System.IO.Path.Combine(basePath, saveDirectory, saveFileName);
        }

        public SaveSettings Copy() => this with { };
    }

    public static class JsonUtils
    {
        public static string BeautifyJson(string unPrettyJson)
        {
            var parsedJson = JToken.Parse(unPrettyJson);
            return parsedJson.ToString(Formatting.Indented);
        }
    }
}