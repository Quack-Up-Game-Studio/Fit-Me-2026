using Redcode.Extensions;
#if UNITY_EDITOR
using Sirenix.OdinInspector.Editor;
using UnityEditor;
#endif
using UnityEngine;
using VContainer;

namespace FitMe.Grid
{
    public class GridPreview : MonoBehaviour
    {
        [SerializeField] private UnityEngine.Grid grid;
        [SerializeField] private GridPreset preset;
        [SerializeField] private BlockPreset blockPreset;
        [SerializeField] private bool drawAllCustomGridCells = true;
        [SerializeField] private GridManagerConfig config;
        
        private GridManager _gridManager;
        
        private GridPreset CurrentGridPreset => Application.isPlaying ? _gridManager.CurrentGridPreset : preset;

        private int[,] CurrentGridArray
        {
            get
            {
                if (Application.isPlaying) return _gridManager.CurrentGridPreset.customGrid;
                if (preset) return preset.customGrid;
                if (blockPreset) return blockPreset.BlockSchema.schema;
                return null;
            }
        }

        [Inject]
        public void Construct(
            GridManager gridManager,
            GridManagerConfig config)
        {
            _gridManager = gridManager;
            this.config = config;
        }
        
#if UNITY_EDITOR
        #region Editor
        public void OnSceneGUI()
        {
            DrawGrid();
        }
        
        private void DrawGrid()
        {
            if (CurrentGridArray == null) return;
            var row = CurrentGridArray.GetLength(0);
            var column = CurrentGridArray.GetLength(1);
            for (var x = 0; x < row; x++)
            for (var y = 0; y < column; y++)
            {
                var textColor = Color.green;
                var handleColor = Color.green;
                if (CurrentGridArray[x, y] == 0)
                {
                    if (!drawAllCustomGridCells) continue;
                    handleColor = Color.red;
                    textColor = Color.red;
                }
                Handles.color = handleColor;
                var arrayIndex = new Vector2Int(x, y);
                var offset = GridUtils.CalculateGridOffset(config, new Vector2Int(column, row));
                if (column % 2 != 0)
                {
                    grid.cellSize = config.CellSize;
                    grid.transform.SetPositionX(-grid.cellSize.x / 2f + config.CustomGridPositionOffsetX);
                }
                else
                {
                    grid.transform.SetPositionX(0f + config.CustomGridPositionOffsetX);
                }
                if (row % 2 != 0)
                {
                    grid.cellSize = config.CellSize;
                    grid.transform.SetPositionY(grid.cellSize.y / 2f + config.CustomGridPositionOffsetY);
                }
                else
                {
                    grid.transform.SetPositionY(0f + config.CustomGridPositionOffsetY);
                }
                var gridIndex = GridUtils.ArrayToGridIndex(arrayIndex, offset);
                var bounds = grid.GetCellBounds(gridIndex);
                Handles.DrawWireCube(bounds.center, bounds.size);
                Handles.Label(bounds.center, arrayIndex.ToString(), style: new GUIStyle
                {
                    fontSize = 10,
                    normal = new GUIStyleState
                    {
                        textColor = textColor
                    },
                    alignment = TextAnchor.MiddleCenter
                });
            }
        }
        
        #endregion
#endif
    }
    
#if UNITY_EDITOR
    [CustomEditor(typeof(GridPreview), true)]
    public class GridPreviewEditor : OdinEditor
    {
        public void OnSceneGUI()
        {
            if (target is not GridPreview gridPreview) return;
            gridPreview.OnSceneGUI();
        }
    }
#endif
}