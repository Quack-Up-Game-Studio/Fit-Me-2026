using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace QuackUp.Save
{
    [CreateAssetMenu(fileName = "MessagePackSaveConfig", menuName = "QuackUp/Save/MessagePackSaveConfig", order = 0)]
    [ShowOdinSerializedPropertiesInInspector]
    public class MessagePackSaveConfig : SerializedScriptableObject
    {
        [field: SerializeField] public bool LoadAtStart { get; private set; } = true;
        [field: OdinSerialize] private Dictionary<string, MessagePackSaveObject> initialSaveObjects = new();
        public IReadOnlyDictionary<string, MessagePackSaveObject> InitialSaveObjects => initialSaveObjects;
        [SerializeField] private bool debugMode = true;
        [ShowIf(nameof(debugMode)), 
         SerializeField] private SaveSettings debugSaveSettings;
        [HideIf(nameof(debugMode)),
         SerializeField] private SaveSettings releaseSaveSettings;
        
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

        /// <summary>Test-only factory: builds a config wired to the given settings (EditMode tests).</summary>
        public static MessagePackSaveConfig CreateForTests(SaveSettings settings)
        {
            var config = CreateInstance<MessagePackSaveConfig>();
            config.debugSaveSettings = settings;
            return config;
        }
    }
}