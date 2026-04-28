using System;
using System.Collections.Generic;
using System.Linq;
using FitMe.Shared;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
#if UNITY_EDITOR
using Sirenix.Utilities.Editor;
#endif
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
        public bool petrified = true;
        public BlockShape shape;
        public bool randomColor = true;
        public BlockColor color;
        public int id;
    }

    public enum ObstacleMode
    {
        Generated,
        Custom
    }
    
    [CreateAssetMenu(fileName = "Grid Preset", menuName = "FitMe/Grid/Grid/Grid Preset", order = 1)]
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
        
        [TitleGroup("Obstacle Settings")]
        [field: SerializeField] public ObstacleMode ObstacleMode {get; private set;} = ObstacleMode.Generated;
        [field: SerializeField, 
                HideIf(nameof(ObstacleMode), ObstacleMode.Custom)] 
        [Obsolete("The game no longer uses a fixed number of obstacles.")]
        public int ObstacleCount { get; private set; } = 2;
#if UNITY_EDITOR
        [field: TableMatrix(SquareCells = true, HorizontalTitle = "Obstacle Data",
            DrawElementMethod = nameof(DrawObstacleDataMatrix), Transpose = true, IsReadOnly =  true)]
#endif
        [field: SerializeField, 
                ShowIf(nameof(ObstacleMode), ObstacleMode.Custom)]
        public ObstacleData[,] CustomObstacleData = { };
        
        [Button("Refresh Obstacle Data"), 
         ShowIf(nameof(ObstacleMode), ObstacleMode.Custom), 
         DisableInPlayMode]
        private void RefreshObstacleData()
        {
            //ObstacleData = new ObstacleData[GridSize.y, GridSize.x];
            ArrayHelper.ResizeArrayKeepMembers(ref CustomObstacleData, GridSize);
            for (var row = 0; row < GridSize.y; row++)
            for (var col = 0; col < GridSize.x; col++)
            {
               
                bool hasCell;
                switch (PresetGridType)
                {
                    case GridType.Rectangle:
                        hasCell = true;
                        break;
                    case GridType.Custom:
                        var cell = customGrid[row, col];
                        hasCell = cell == 1;
                        break;
                    default:
                        hasCell = false;
                        break;
                }

                CustomObstacleData[row, col] = new ObstacleData
                {
                    hasCell = hasCell,
                    shape = BlockShape.OneByOne,
                    id = 0
                };
            }
        }
        
        [ShowInInspector, ReadOnly, DisplayAsString] private int TotalCells => PresetGridType switch
        {
            GridType.Rectangle => GridSize.x * GridSize.y,
            GridType.Custom => customGrid.Cast<int>().Count(cell => cell == 1),
            _ => 0
        };
        #endregion
        
        [field: SerializeField] public bool OverrideBag {get; private set;}
        [field: ShowIf(nameof(OverrideBag))]
        [field: SerializeField] public bool ShuffleBag { get; private set; } = true;
        [field: ShowIf(nameof(OverrideBag))]
        [field: SerializeField] public List<SpawnBlockData> SpawnBlockData { get; private set; } = new();

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
            // split the rect into 4 rows
            var row1 = rect.Padding(1).SetHeight(rect.height / 6f);
            var row2 = row1.SetY(row1.yMax);
            var row3 = row2.SetY(row2.yMax);
            var row4 = row3.SetY(row3.yMax);
            var row5 = row4.SetY(row4.yMax);
            var row6 = row5.SetY(row5.yMax);
            var obstacleColor = value.hasObstacle ? Color.white : Color.red;
            value.hasObstacle = EditorGUI.ToggleLeft(row1, "Obstacle", value.hasObstacle, 
                new GUIStyle(EditorStyles.label)
                {
                    normal = { textColor = obstacleColor },
                    active = { textColor = obstacleColor }
                });
            if (!value.hasObstacle) return value;
            value.petrified = EditorGUI.ToggleLeft(row2, "Petrified", value.petrified, 
                new GUIStyle(EditorStyles.label)
                {
                    normal = { textColor = obstacleColor },
                    active = { textColor = obstacleColor }
                });
            value.shape = (BlockShape)SirenixEditorFields.EnumDropdown(row3, value.shape); 
            value.id = SirenixEditorFields.IntField(row4, value.id);
            value.randomColor = EditorGUI.ToggleLeft(row5, "Random Color", value.randomColor, 
                new GUIStyle(EditorStyles.label)
                {
                    normal = { textColor = obstacleColor },
                    active = { textColor = obstacleColor }
                });
            if (value.randomColor)  return value;
            value.color = (BlockColor)SirenixEditorFields.EnumDropdown(row6, value.color);
            return value;
        }
        
        #endregion
#endif
    }
}