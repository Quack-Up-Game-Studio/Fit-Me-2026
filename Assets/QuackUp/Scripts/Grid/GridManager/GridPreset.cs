using System;
using FitMe.Shared;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace FitMe.Grid
{

    [Serializable]
    public record ObstacleData
    {
        public bool hasCell;
        public bool hasObstacle;
        public BlockShape shape;
        public int id;
    }
    
    [CreateAssetMenu(fileName = "Grid Preset", menuName = "FitMe/Grid/Grid Preset", order = 1)]
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
        
#if UNITY_EDITOR
        [field: TableMatrix(SquareCells = true, HorizontalTitle = "Obstacle Data", 
            DrawElementMethod = nameof(DrawObstacleDataMatrix), Transpose = true, IsReadOnly =  true)]
#endif
        [field: SerializeField]
        public ObstacleData[,] ObstacleData = { };
        
        [Button("Refresh Obstacle Data"), DisableInPlayMode]
        private void RefreshObstacleData()
        {
            ObstacleData = new ObstacleData[GridSize.y, GridSize.x];
            for (var row = 0; row < customGrid.GetLength(0); row++)
            for (var col = 0; col < customGrid.GetLength(1); col++)
            {
                var cell = customGrid[row, col];
                ObstacleData[row, col] = new ObstacleData
                {
                    hasCell = cell == 1,
                    shape = BlockShape.OneByOne,
                    id = 0
                };
            }
        }
        #endregion

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
        
        private static ObstacleData DrawObstacleDataMatrix(Rect rect, ObstacleData value)
        {
            if (!value.hasCell)
            {
                EditorGUI.DrawRect(rect.Padding(1), Color.grey);
                return value;
            }
            EditorGUI.DrawRect(rect.Padding(1), value.hasObstacle ? Color.red : Color.green);
            // split the rect into 3 rows
            var row1 = rect.Padding(1).SetHeight(rect.height / 3f);
            var row2 = row1.SetY(row1.yMax);
            var row3 = row2.SetY(row2.yMax);
            value.hasObstacle = EditorGUI.ToggleLeft(row1, "Obstacle", value.hasObstacle, 
                new GUIStyle(EditorStyles.label)
                {
                    normal = { textColor = value.hasObstacle ? Color.white : Color.red },
                    active = { textColor = value.hasObstacle ? Color.white : Color.red }
                });
            if (!value.hasObstacle) return value;
            value.shape = (BlockShape)SirenixEditorFields.EnumDropdown(row2, value.shape); 
            value.id = SirenixEditorFields.IntField(row3, value.id);
            return value;
        }
        
        #endregion
#endif
    }
}