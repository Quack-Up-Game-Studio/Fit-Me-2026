using System;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using UnityEditor;
using UnityEngine;

namespace FitMe.Grid
{
    [Serializable]
    public struct InfectionSettings
    {
        [field: SerializeField] public Vector2Int InfectionCountRange { get; private set; }
        [field: SerializeField] public Vector2 InfectionTimeRange { get; private set; }
        [field: SerializeField] public Vector2 FirstInfectTimeRange { get; private set; }
        [field: SerializeField] public float PreInfectTime { get; private set; }
        [ShowInInspector, ReadOnly] public bool CanInfect => InfectionCountRange.x >= 1;
    }
    
    [CreateAssetMenu(fileName = "Grid Preset", menuName = "MadDuck/Grid Preset", order = 1)]
    [ShowOdinSerializedPropertiesInInspector]
    public class GridPreset : SerializedScriptableObject
    {
        #region Inspectors
        [TitleGroup("Grid Settings")]
        // [field: SerializeField] 
        // public GameDifficulty GameDifficulty { get; set; } = GameDifficulty.Medium;
        [field: SerializeField]
        [field: ValidateInput("@PresetGridType != GridType.All && PresetGridType != GridType.None", 
            "Grid preset must have either Rectangle or Custom grid type.")]
        //[field: UnflagEnum]
        public GridType PresetGridType { get; set; } = GridType.Rectangle;
        [TitleGroup("Grid Settings")]
        [field: SerializeField] [MinValue(1)]
        public Vector2Int GridSize { get; set; } = new(10, 10);
        [TitleGroup("Grid Settings")]
        [Button("Refresh Custom Grid"), ShowIf("@PresetGridType.HasFlag(GridType.Custom)"), DisableInPlayMode]
        private void RefreshCustomGrid()
        {
            ArrayHelper.ResizeArrayKeepMembers(ref customGrid, GridSize);
        }
        [TitleGroup("Grid Settings")]
        [Button("Clear Custom Grid"), ShowIf("@PresetGridType.HasFlag(GridType.Custom)"), DisableInPlayMode]
        private void ClearCustomGrid()
        {
            customGrid = new int[GridSize.y, GridSize.x];
        }
        [TitleGroup("Grid Settings")]
        #if UNITY_EDITOR
        [field: TableMatrix(SquareCells = true, HorizontalTitle = "Custom Grid",
            DrawElementMethod = nameof(DrawCustomGridMatrix), Transpose = true)]
        #endif
        [field: SerializeField, ShowIf("@PresetGridType.HasFlag(GridType.Custom)")]
        public int[,] customGrid = { };
        #endregion
        
        [field: TitleGroup("Infection Settings")]
        [field: SerializeField] public bool AllowInfection { get; set; } = true;
        [field: TitleGroup("Infection Settings")] 
        [field: SerializeField, ShowIf(nameof(AllowInfection))]
        public InfectionSettings InfectionSettings { get; set; }

        #if UNITY_EDITOR
        #region Table Matrix
        private static int DrawCustomGridMatrix(Rect rect, int value)
        {
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                value = value == 1 ? 0 : 1; // Toggle between 0 and 1
                GUI.changed = true;
                Event.current.Use();
            }

            EditorGUI.DrawRect(rect.Padding(1), value == 1 ? Color.green : Color.grey);
            return value;
        }
        #endregion
        #endif
    }
}