using System;
using System.Collections.Generic;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using UnityEngine;

namespace FitMe.GameData
{
    [CreateAssetMenu(fileName = "Shape Level World Catalog", menuName = "FitMe/GameData/Level/Shape Level World Catalog")]
    public sealed class ShapeLevelWorldCatalog : SerializedScriptableObject
    {
        [InfoBox("$InspectorValidationMessage", InfoMessageType.Error, nameof(HasInspectorValidationErrors))]
        [SerializeField, ListDrawerSettings(DraggableItems = true)]
        private List<ShapeLevelWorldEntry> _entries = new();

#if UNITY_EDITOR
        [NonSerialized] private string _inspectorValidationMessage;
        [NonSerialized] private bool _hasInspectorValidationErrors;
#endif

        public IReadOnlyList<ShapeLevelWorldEntry> Entries => _entries ?? (IReadOnlyList<ShapeLevelWorldEntry>)Array.Empty<ShapeLevelWorldEntry>();
        public int Count => Entries.Count;

        public bool TryGetByKey(string worldKey, out ShapeLevelWorldEntry world)
        {
            world = null;
            if (string.IsNullOrWhiteSpace(worldKey)) return false;
            foreach (var entry in Entries)
            {
                if (entry == null || !string.Equals(entry.WorldKey, worldKey, StringComparison.Ordinal)) continue;
                if (world != null) { world = null; return false; }
                world = entry;
            }
            return world != null;
        }

        public int GetOrder(string worldKey)
        {
            var order = 0;
            for (var i = 0; i < Entries.Count; i++)
            {
                if (Entries[i] == null || !string.Equals(Entries[i].WorldKey, worldKey, StringComparison.Ordinal)) continue;
                if (order != 0) return 0;
                order = i + 1;
            }
            return order;
        }

        public bool IsValid
        {
            get
            {
                if (Entries.Count == 0) return false;
                var keys = new HashSet<string>(StringComparer.Ordinal);
                foreach (var entry in Entries)
                    if (!IsValidEntry(entry) || !keys.Add(entry.WorldKey)) return false;
                return true;
            }
        }

        private static bool IsValidEntry(ShapeLevelWorldEntry entry) => entry != null &&
            !string.IsNullOrWhiteSpace(entry.WorldKey) && entry.LevelCatalog && entry.LevelCatalog.Validate().IsValid;

#if UNITY_EDITOR
        private string InspectorValidationMessage => _inspectorValidationMessage;
        private bool HasInspectorValidationErrors => _hasInspectorValidationErrors;

        [Button("Validate Catalog")]
        private void ValidateCatalogManually()
        {
            RefreshInspectorValidation();
            if (_hasInspectorValidationErrors) DebugUtils.LogError(_inspectorValidationMessage);
            else DebugUtils.Log($"Shape level world catalog '{name}' is valid.");
        }

        private void OnValidate()
        {
            RefreshInspectorValidation();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < Entries.Count; i++)
            {
                var entry = Entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.WorldKey))
                    DebugUtils.LogError($"Missing world key at entry {i + 1} in {name}.");
                else if (!seen.Add(entry.WorldKey))
                    DebugUtils.LogError($"Duplicate world key '{entry.WorldKey}' in {name}.");
                if (entry != null && !entry.LevelCatalog)
                    DebugUtils.LogError($"Missing level catalog for world '{entry.WorldKey}' in {name}.");
            }
        }

        private void RefreshInspectorValidation()
        {
            var errors = new List<string>();
            if (Entries.Count == 0) errors.Add("Add at least one world entry.");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < Entries.Count; i++)
            {
                var entry = Entries[i];
                if (entry == null)
                {
                    errors.Add($"Remove null world entry at position {i + 1}.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(entry.WorldKey)) errors.Add($"World entry {i + 1} needs a non-empty key.");
                else if (!seen.Add(entry.WorldKey)) errors.Add($"World key '{entry.WorldKey}' is duplicated.");
                if (!entry.LevelCatalog) errors.Add($"World '{entry.WorldKey}' needs a level catalog.");
                else if (!entry.LevelCatalog.Validate().IsValid) errors.Add($"World '{entry.WorldKey}' references an invalid level catalog.");
            }
            _hasInspectorValidationErrors = errors.Count > 0;
            _inspectorValidationMessage = _hasInspectorValidationErrors
                ? $"World catalog validation failed:\n• {string.Join("\n• ", errors)}"
                : string.Empty;
        }
#endif
    }

    [Serializable]
    public sealed class ShapeLevelWorldEntry
    {
        [SerializeField] private string _worldKey;
        [SerializeField] private ShapeLevelCatalog _levelCatalog;
        [SerializeField, Tooltip("Complete button artwork shown when this world is unlocked.")]
        private Sprite _unlockedSprite;
        [SerializeField, Tooltip("Complete button artwork including the lock. Optional until the art is available.")]
        private Sprite _lockedSprite;
        public string WorldKey => _worldKey;
        public ShapeLevelCatalog LevelCatalog => _levelCatalog;
        public Sprite UnlockedSprite => _unlockedSprite;
        public Sprite LockedSprite => _lockedSprite;
        public ShapeLevelWorldEntry() { }
        public ShapeLevelWorldEntry(string worldKey, ShapeLevelCatalog levelCatalog)
        {
            _worldKey = worldKey;
            _levelCatalog = levelCatalog;
        }
    }
}
