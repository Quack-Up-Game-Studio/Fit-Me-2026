using System.Collections.Generic;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace FitMe.GameData
{
    [CreateAssetMenu(fileName = "Shape Level Catalog", menuName = "FitMe/GameData/Level/Shape Level Catalog")]
    public sealed class ShapeLevelCatalog : SerializedScriptableObject
    {
        [OdinSerialize] private OrderedDictionary<string, ShapeLevelDefinition> _levels = new();

        private OrderedDictionary<string, ShapeLevelDefinition> Entries =>
            _levels ?? (_levels = new OrderedDictionary<string, ShapeLevelDefinition>());

        public IReadOnlyOrderedDictionary<string, ShapeLevelDefinition> Levels => Entries;

        public bool TryGetById(string levelId, out ShapeLevelDefinition level)
        {
            level = null;
            if (string.IsNullOrEmpty(levelId) || !Entries.TryGetValue(levelId, out var candidate) || !candidate)
                return false;
            level = candidate;
            return true;
        }

        public bool TryGetId(ShapeLevelDefinition level, out string levelId)
        {
            levelId = null;
            if (!level) return false;

            var keys = Entries.Keys.GetEnumerator();
            for (var index = 0; index < Entries.Count; index++)
            {
                if (!keys.MoveNext()) return false;
                if (Entries[index] != level) continue;
                levelId = keys.Current;
                return true;
            }

            return false;
        }

        public IReadOnlyList<ShapeLevelDefinition> GetOrderedLevels()
        {
            var ordered = new List<ShapeLevelDefinition>();
            for (var i = 0; i < Entries.Count; i++)
            {
                var level = Entries[i];
                if (level) ordered.Add(level);
            }
            return ordered;
        }

        public ShapeLevelValidationResult Validate() => ShapeLevelDataValidator.Validate(this);

        internal IReadOnlyOrderedDictionary<string, ShapeLevelDefinition> GetLevelsForValidation() => Entries;
    }
}
