using System.Collections.Generic;
using FitMe.Grid;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace FitMe.GameData.Editor
{
    [CustomEditor(typeof(ShapeLevelDefinition))]
    public sealed class ShapeLevelDefinitionEditor : OdinEditor
    {
        private ShapeLevelDesignSnapshot _snapshot;
        private ShapeLevelTilingResult _tilingResult;
        private IReadOnlyList<ShapeLevelValidationError> _tilingValidationErrors;

        protected override void OnEnable()
        {
            base.OnEnable();
            RefreshDesignMetrics();
        }

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            base.OnInspectorGUI();
            if (EditorGUI.EndChangeCheck()) RefreshDesignMetrics();
            if (_snapshot == null) RefreshDesignMetrics();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Design Heuristics", EditorStyles.boldLabel);
            DrawMetrics();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh Design Metrics")) RefreshDesignMetrics();
            if (GUILayout.Button("Validate Tiling")) ValidateTiling();
            EditorGUILayout.EndHorizontal();

            DrawTilingResult();
        }

        private void RefreshDesignMetrics()
        {
            _snapshot = ShapeLevelDesignSnapshot.Create((ShapeLevelDefinition)target);
            _tilingResult = null;
            _tilingValidationErrors = null;
            Repaint();
        }

        private void ValidateTiling()
        {
            RefreshDesignMetrics();
            var blockingErrors = new List<ShapeLevelValidationError>();
            foreach (var error in _snapshot.Errors)
                if (error != ShapeLevelValidationError.BlockCapacityMismatch)
                    blockingErrors.Add(error);
            if (blockingErrors.Count > 0)
            {
                _tilingValidationErrors = blockingErrors;
                return;
            }

            var blockSchemas = new List<int[,]>();
            foreach (var entry in ((ShapeLevelDefinition)target).PlayerBlocks)
            {
                var schema = entry.Source == ShapePlayerBlockSource.Preset
                    ? entry.Preset.BlockSchema?.schema
                    : entry.InlineFootprint;
                for (var copy = 0; copy < entry.Count; copy++)
                    blockSchemas.Add(schema);
            }

            _tilingResult = ShapeLevelTilingSolver.Solve(
                _snapshot.CreatePlayableMask(), blockSchemas,
                ShapeTilingValidationSettings.instance.MaxSearchNodes);
        }

        private void DrawMetrics()
        {
            if (_snapshot.Errors.Count > 0)
                EditorGUILayout.HelpBox(FormatValidationErrors(_snapshot.Errors), MessageType.Error);

            EditorGUILayout.LabelField("Board size", $"{_snapshot.Width} × {_snapshot.Height}");
            EditorGUILayout.LabelField("Playable cells", _snapshot.PlayableCellCount.ToString());
            EditorGUILayout.LabelField("Obstacle cells", _snapshot.ObstacleCellCount.ToString());
            EditorGUILayout.LabelField("Void cells", _snapshot.VoidCellCount.ToString());
            EditorGUILayout.LabelField("Player block cells", _snapshot.PlayerBlockCellCount.ToString());
            EditorGUILayout.LabelField("Unfilled / excess cells", _snapshot.CapacityDifference.ToString());
            DrawBoardPreview();
        }

        private void DrawBoardPreview()
        {
            if (_snapshot.Width <= 0 || _snapshot.Height <= 0) return;

            const float maxSide = 300f;
            var cellSize = Mathf.Clamp(maxSide / Mathf.Max(_snapshot.Width, _snapshot.Height), 4f, 22f);
            var rect = GUILayoutUtility.GetRect(
                _snapshot.Width * cellSize, _snapshot.Height * cellSize,
                GUILayout.Width(_snapshot.Width * cellSize),
                GUILayout.Height(_snapshot.Height * cellSize));

            for (var row = 0; row < _snapshot.Height; row++)
            for (var column = 0; column < _snapshot.Width; column++)
            {
                var cell = new Rect(rect.x + column * cellSize, rect.y + row * cellSize, cellSize, cellSize);
                EditorGUI.DrawRect(cell, new Color(0.04f, 0.05f, 0.07f));
                cell.xMin += 1f;
                cell.yMin += 1f;
                cell.xMax -= 1f;
                cell.yMax -= 1f;

                var color = !_snapshot.BoardCells[row, column]
                    ? new Color(0.14f, 0.15f, 0.17f)
                    : _snapshot.Obstacles[row, column]
                        ? new Color(0.78f, 0.25f, 0.19f)
                        : new Color(0.30f, 0.66f, 0.40f);
                EditorGUI.DrawRect(cell, color);
            }

            EditorGUILayout.BeginHorizontal();
            DrawLegendItem(new Color(0.30f, 0.66f, 0.40f), "Playable");
            DrawLegendItem(new Color(0.78f, 0.25f, 0.19f), "Obstacle");
            DrawLegendItem(new Color(0.14f, 0.15f, 0.17f), "Void");
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawLegendItem(Color color, string label)
        {
            var rect = GUILayoutUtility.GetRect(12f, 12f, GUILayout.Width(12f), GUILayout.Height(12f));
            EditorGUI.DrawRect(rect, color);
            EditorGUILayout.LabelField(label, GUILayout.Width(68f));
        }

        private void DrawTilingResult()
        {
            if (_tilingValidationErrors != null)
            {
                EditorGUILayout.HelpBox(
                    "Tiling check not run because the level data is invalid:\n" +
                    FormatValidationErrors(_tilingValidationErrors), MessageType.Error);
                return;
            }
            if (_tilingResult == null) return;

            var messageType = _tilingResult.Status switch
            {
                ShapeLevelTilingStatus.Feasible => MessageType.Info,
                ShapeLevelTilingStatus.Infeasible => MessageType.Error,
                ShapeLevelTilingStatus.SearchLimitReached => MessageType.Warning,
                _ => MessageType.Error
            };
            var message = _tilingResult.Status switch
            {
                ShapeLevelTilingStatus.Feasible => "A complete tiling was found.",
                ShapeLevelTilingStatus.Infeasible => "No complete tiling exists for this exact block list.",
                ShapeLevelTilingStatus.SearchLimitReached =>
                    "Search limit reached; result is inconclusive. Adjust the shared budget in Project Settings → Fit Me → Shape Level Validation.",
                _ => "The solver rejected the supplied board or block data."
            };
            EditorGUILayout.HelpBox(
                $"{message}\nStatus: {_tilingResult.Status} · Search nodes: {_tilingResult.NodesVisited}",
                messageType);
        }

        private static string FormatValidationErrors(IReadOnlyList<ShapeLevelValidationError> errors)
        {
            var lines = new string[errors.Count];
            for (var index = 0; index < errors.Count; index++)
                lines[index] = "• " + errors[index];
            return string.Join("\n", lines);
        }
    }
}
