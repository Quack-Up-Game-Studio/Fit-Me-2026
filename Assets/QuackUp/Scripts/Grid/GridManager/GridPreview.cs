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
        [SerializeField] private bool drawAllCustomGridCells = true;
        [SerializeField] private GridManagerConfig config;
        
        private GridManager _gridManager;

        private GridPreset CurrentPreset => Application.isPlaying ? _gridManager.CurrentGridPreset : preset;

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
            if (!CurrentPreset) return;
            var row = CurrentPreset.GridSize.y;
            var column = CurrentPreset.GridSize.x;
            for (int x = 0; x < row; x++)
            {
                for (int y = 0; y < column; y++)
                {
                    var textColor = Color.green;
                    var handleColor = Color.green;
                    if (CurrentPreset.PresetGridType is GridType.Custom && CurrentPreset.customGrid[x, y] == 0)
                    {
                        if (!drawAllCustomGridCells) continue;
                        handleColor = Color.red;
                        textColor = Color.red;
                    }
                    Handles.color = handleColor;
                    var arrayIndex = new Vector2Int(x, y);
                    var offset = GridUtils.CalculateGridOffset(config, CurrentPreset.GridSize);
                    if (CurrentPreset.GridSize.x % 2 != 0)
                    {
                        grid.cellSize = config.CellSize;
                        grid.transform.SetPositionX(-grid.cellSize.x / 2f);
                    }
                    else
                    {
                        grid.transform.SetPositionX(0f);
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