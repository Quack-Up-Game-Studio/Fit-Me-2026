using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace FitMe.Grid
{
    [CreateAssetMenu(fileName = "BlockManagerConfig", menuName = "FitMe/Block/BlockManagerConfig")]
    public class BlockManagerConfig : SerializedScriptableObject
    {
        [Title("Random Settings")]
        [field: SerializeField] public int MaxRandomAmount { get; private set; } = 3;
        [field: SerializeField] public bool UseSmartRandom { get; private set; } = true;
        [field: SerializeField] public int SmartRandomThreshold { get; private set; } = 6;
        [field: SerializeField] public int SmartRandomDepth { get; private set; } = 1;
        [field: SerializeField] public float ObjectScale { get; private set; } = 0.5f;
        [field: OdinSerialize]
        public Dictionary<BlockShape, BlockPreset> BlockPresetDictionary { get; private set; } = new();
    }
}