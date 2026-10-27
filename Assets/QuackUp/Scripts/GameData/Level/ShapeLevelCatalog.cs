using System;
using System.Collections.Generic;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using UnityEngine;

namespace FitMe.GameData
{
    [CreateAssetMenu(fileName = "Shape Level Catalog", menuName = "FitMe/GameData/Level/Shape Level Catalog")]
    public sealed class ShapeLevelCatalog : SerializedScriptableObject
    {
#if UNITY_EDITOR
        [InfoBox("$InspectorValidationMessage", InfoMessageType.Error, nameof(HasInspectorValidationErrors))]
#endif
        [SerializeField, ListDrawerSettings(DraggableItems = true)]
        private List<ShapeLevelCatalogEntry> _entries = new();

#if UNITY_EDITOR
        [NonSerialized] private string _inspectorValidationMessage;
        [NonSerialized] private bool _hasInspectorValidationErrors;
#endif

        public IReadOnlyList<ShapeLevelCatalogEntry> Entries
        {
            get
            {
                if (_entries != null) return _entries;
                return Array.Empty<ShapeLevelCatalogEntry>();
            }
        }

        public int Count => Entries.Count;

        public bool TryGetById(string levelId, out ShapeLevelDefinition level)
        {
            level = null;
            if (string.IsNullOrWhiteSpace(levelId)) return false;

            ShapeLevelDefinition candidate = null;
            var found = false;
            foreach (var entry in Entries)
            {
                if (entry == null || !string.Equals(entry.Key, levelId, StringComparison.Ordinal)) continue;
                if (found) return false;
                found = true;
                candidate = entry.LevelDefinition;
            }

            if (!found || !candidate) return false;
            level = candidate;
            return true;
        }

        public bool TryGetId(ShapeLevelDefinition level, out string levelId)
        {
            levelId = null;
            if (!level) return false;

            foreach (var entry in Entries)
            {
                if (entry == null || entry.LevelDefinition != level || GetOrder(entry.Key) == 0) continue;
                levelId = entry.Key;
                return true;
            }

            return false;
        }

        public IReadOnlyList<ShapeLevelDefinition> GetOrderedLevels()
        {
            var ordered = new List<ShapeLevelDefinition>();
            foreach (var entry in Entries)
            {
                var level = entry?.LevelDefinition;
                if (level) ordered.Add(level);
            }
            return ordered;
        }

        public int GetOrder(string levelId)
        {
            if (string.IsNullOrWhiteSpace(levelId)) return 0;

            var order = 0;
            for (var index = 0; index < Entries.Count; index++)
            {
                var entry = Entries[index];
                if (entry == null || !string.Equals(entry.Key, levelId, StringComparison.Ordinal)) continue;
                if (order != 0) return 0;
                order = index + 1;
            }

            return order;
        }

        public string GetIdAtOrder(int order)
        {
            if (order < 1 || order > Count) return null;
            var key = Entries[order - 1]?.Key;
            return string.IsNullOrWhiteSpace(key) ? null : key;
        }

        public ShapeLevelValidationResult Validate() => ShapeLevelDataValidator.Validate(this);

        internal IReadOnlyList<ShapeLevelCatalogEntry> GetEntriesForValidation() => Entries;

#if UNITY_EDITOR
        private string InspectorValidationMessage => _inspectorValidationMessage;
        private bool HasInspectorValidationErrors => _hasInspectorValidationErrors;

        [Button("Validate Catalog")]
        private void ValidateCatalogManually()
        {
            var validation = RefreshInspectorValidation();
            if (validation.IsValid)
                DebugUtils.Log($"Shape level catalog '{name}' is valid.");
            else
                DebugUtils.LogError(_inspectorValidationMessage);
        }

        private void OnValidate()
        {
            RefreshInspectorValidation();

            for (var index = 0; index < Entries.Count; index++)
            {
                var entry = Entries[index];
                if (entry != null && !string.IsNullOrWhiteSpace(entry.Key)) continue;
                DebugUtils.LogError($"Missing level key at entry {index + 1} in {name}.");
            }

            foreach (var duplicateKey in ShapeLevelDataValidator.GetDuplicateCatalogKeys(Entries))
                DebugUtils.LogError($"Duplicate level key '{duplicateKey}' in {name}.");
        }

        private ShapeLevelValidationResult RefreshInspectorValidation()
        {
            var validation = Validate();
            _hasInspectorValidationErrors = !validation.IsValid;
            _inspectorValidationMessage = validation.IsValid
                ? string.Empty
                : FormatValidationErrors(validation.Errors);
            return validation;
        }

        private static string FormatValidationErrors(IReadOnlyList<ShapeLevelValidationError> errors)
        {
            var seen = new HashSet<ShapeLevelValidationError>();
            var messages = new List<string>();
            for (var index = 0; index < errors.Count; index++)
            {
                var error = errors[index];
                if (!seen.Add(error)) continue;
                messages.Add(error switch
                {
                    ShapeLevelValidationError.EmptyCatalog => "Add at least one level entry.",
                    ShapeLevelValidationError.MissingCatalogKey => "Every entry needs a non-empty key.",
                    ShapeLevelValidationError.DuplicateCatalogKey => "Entry keys must be unique (case-sensitive).",
                    ShapeLevelValidationError.NullCatalogEntry => "Remove null entries from the catalog.",
                    ShapeLevelValidationError.NullLevelDefinition => "Every entry must reference a level definition.",
                    _ => error.ToString()
                });
            }

            return $"Catalog validation failed:\n• {string.Join("\n• ", messages)}";
        }
#endif
    }

    [Serializable]
    public sealed class ShapeLevelCatalogEntry
    {
        [SerializeField] private string _key;
        [SerializeField] private ShapeLevelDefinition _levelDefinition;

        public string Key => _key;
        public ShapeLevelDefinition LevelDefinition => _levelDefinition;

        public ShapeLevelCatalogEntry() { }

        public ShapeLevelCatalogEntry(string key, ShapeLevelDefinition levelDefinition)
        {
            _key = key;
            _levelDefinition = levelDefinition;
        }
    }
}
