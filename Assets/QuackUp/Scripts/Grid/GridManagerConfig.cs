using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace FitMe.Grid
{
    [CreateAssetMenu(fileName = "GridManagerConfig", menuName = "FitMe/Grid/GridManagerConfig")]
    public class GridManagerConfig : SerializedScriptableObject
    {
        [TitleGroup("Grid Settings")]
        [field: SerializeField]
        [ValidateInput("@EndlessType != EndlessType.None", "Endless type cannot be None")]
        public EndlessType EndlessType { get; private set; } = EndlessType.All;
        [TitleGroup("Grid Settings")]
        [field: SerializeField][ValidateInput("@GeneratedGridType != GridType.None", "Grid type cannot be None")]
        [ShowIf("@EndlessType.HasFlag(EndlessType.Generated)")]
        public GridType GeneratedGridType { get; private set; } = GridType.Rectangle;
        [TitleGroup("Grid Settings")]
        [field: SerializeField]
        [ShowIf(nameof(EndlessType), EndlessType.Preset)]
        public PresetRandomType PresetRandomType { get; private set; } = PresetRandomType.Random;
        [TitleGroup("Grid Settings")]
        [field: SerializeField]
        public bool UseDifficultyPreset { get; private set; } = true;
        [TitleGroup("Grid Settings")]
        [field: SerializeField][MinMaxSlider(1, 20, ShowFields = true)]
        public Vector2Int RandomGridXRange { get; private set; } = new(1, 10);
        [TitleGroup("Grid Settings")]
        [field: SerializeField][MinMaxSlider(1, 20, ShowFields = true)]
        public Vector2Int RandomGridYRange { get; private set; } = new(1, 10);
        [TitleGroup("Grid Settings")]
        [field: SerializeField][MinMaxSlider(1, 20, ShowFields = true)] 
        [ShowIf("@GeneratedGridType.HasFlag(GridType.Custom)")]
        public Vector2Int BridgeWidthRange { get; private set; } = new(2, 3);
        [TitleGroup("Grid Settings")] 
        //[field: SerializeField][OnValueChanged(nameof(UpdateGridOffset))]
        public GridOffsetType GridHorizontalOffsetType { get; private set; } = GridOffsetType.Automatic;
        [TitleGroup("Grid Settings")]
        [field: SerializeField]
        //[OnValueChanged(nameof(UpdateGridOffset))]
        public int CustomOffsetX { get; private set; } = 0;
        [TitleGroup("Grid Settings")]
        //[field: SerializeField][OnValueChanged(nameof(UpdateGridOffset))]
        public GridOffsetType GridVerticalOffsetType { get; private set; } = GridOffsetType.Custom;
        [TitleGroup("Grid Settings")]
        [field: SerializeField]
        //[OnValueChanged(nameof(UpdateGridOffset))]
        public int CustomOffsetY { get; private set; } = 0;
        [TitleGroup("Grid Settings")]
        [field: SerializeField]
        public int DestroyThreshold { get; private set; } = 3;
    }
}