using System.Collections.Generic;
using FitMe.Shared;
using FMODUnity;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace FitMe.Grid
{
    [CreateAssetMenu(fileName = "BlockManagerConfig", menuName = "FitMe/Grid/Block/BlockManagerConfig")]
    public class BlockManagerConfig : SerializedScriptableObject
    {
        [Title("Random Settings")]
        [field: SerializeField] public int MaxRandomAmount { get; private set; } = 3;
        [field: SerializeField] public int MinRandomAmount { get; private set; } = 1;
        [field: SerializeField] public bool UseSmartRandom { get; private set; } = true;
        [field: SerializeField] public int SmartRandomThreshold { get; private set; } = 9;
        [field: SerializeField] public int SmartRandomDepth { get; private set; } = 1;
        [field: SerializeField] public float PreviewScale { get; private set; } = 0.25f;
        [field: SerializeField] public float ObjectScale { get; private set; } = 0.5f;
        [field: OdinSerialize] private Dictionary<BlockShape, BlockPreset> _blockPresetDictionary = new();
        public IReadOnlyDictionary<BlockShape, BlockPreset> BlockPresetDictionary => _blockPresetDictionary;
        [field: OdinSerialize] private Dictionary<BlockShape, BlockConfig> _blockConfigDictionary = new();
        public IReadOnlyDictionary<BlockShape, BlockConfig> BlockConfigDictionary => _blockConfigDictionary;
        [field: SerializeField] public bool CanRefill { get; private set; } = true;
        [field: OdinSerialize] private Dictionary<BlockShape, int> _bagSettings = new();
        public IReadOnlyDictionary<BlockShape, int> BagSettings => _bagSettings;
        
        [Title("Block Settings")]
        [field: SerializeField, SortingLayer] public int SpawnSortingLayer { get; private set; }
        [field: SerializeField, SortingLayer] public int PickUpSortingLayer { get; private set; }
        [field: SerializeField, SortingLayer] public int GridSortingLayer { get; private set; }
        [field: OdinSerialize] private Dictionary<BlockColor, Color> _atomColorDict = new();
        public IReadOnlyDictionary<BlockColor, Color> AtomColorDict => _atomColorDict;
        [field: SerializeField] public bool AllowPickUpAfterPlacement { get; private set; }
        [field: SerializeField] public bool RotateClockwise { get; private set; } = true;
        [field: SerializeField] public float PickUpScaleMultiplier { get; private set; } = 1.2f;
        [field: SerializeField] public Vector2 SwitchIdleTimeRange { get; private set; } = new(30f, 60f);
        
        [Title("Audios")] 
        [field: SerializeField] public EventReference PlaceSucceedSfx { get; private set; }
        [field: SerializeField] public EventReference PlaceFailSfx { get; private set; }
    }
}