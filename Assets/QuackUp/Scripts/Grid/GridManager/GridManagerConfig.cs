using System.Collections.Generic;
using FMODUnity;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace FitMe.Grid
{
    [CreateAssetMenu(fileName = "GridManagerConfig", menuName = "FitMe/Grid/Grid/GridManagerConfig")]
    public class GridManagerConfig : SerializedScriptableObject
    {
        [field: SerializeField]
        public Vector2 CellSize { get; private set; } = new(0.5f, 0.5f);
        [field: NoNoneFlag, SerializeField]
        public EndlessType EndlessType { get; private set; } = EndlessType.All;
        [field: NoNoneFlag, SerializeField]
        public GridType GeneratedGridType { get; private set; } = GridType.Rectangle;
        [field: SerializeField]
        public PresetRandomType PresetRandomType { get; private set; } = PresetRandomType.Random;
        [field: SerializeField]
        public Vector2Int RandomGridXRange { get; private set; } = new(1, 10);
        [field: SerializeField]
        public Vector2Int RandomGridYRange { get; private set; } = new(1, 10);
        [field: SerializeField]
        public Vector2Int BridgeWidthRange { get; private set; } = new(2, 3);
        [field: SerializeField]
        public GridOffsetType GridHorizontalOffsetType { get; private set; } = GridOffsetType.Automatic;
        [field: SerializeField]
        public int CustomOffsetX { get; private set; } = 0;
        [field: SerializeField]
        public GridOffsetType GridVerticalOffsetType { get; private set; } = GridOffsetType.Custom;
        [field: SerializeField]
        public int CustomOffsetY { get; private set; } = 0;
        [field: SerializeField]
        public int ComboThreshold { get; private set; } = 3;
        
        [field: SerializeField] private List<GridPreset> gridPresets = new();
        public IReadOnlyList<GridPreset> GridPresets => gridPresets;
        
        [field: SerializeField] public EventReference FitExplodeSfx { get; private set; }
    }
}