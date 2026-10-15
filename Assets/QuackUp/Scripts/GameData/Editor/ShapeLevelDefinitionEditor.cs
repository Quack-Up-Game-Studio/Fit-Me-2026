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
        private ShapePlaysetDifficultyMetrics _difficultyMetrics;
        private bool _tilingNeedsValidation;
        private int _selectedSolutionIndex;
        private int _validatedDefinitionFingerprint;
        private static GUIStyle _blockIndexLabelStyle;

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
            _tilingNeedsValidation = _tilingNeedsValidation ||
                                     _tilingResult != null || _tilingValidationErrors != null;
            _snapshot = ShapeLevelDesignSnapshot.Create((ShapeLevelDefinition)target);
            if (!_tilingNeedsValidation)
                _validatedDefinitionFingerprint = GetDefinitionFingerprint((ShapeLevelDefinition)target);
            _tilingResult = null;
            _tilingValidationErrors = null;
            _difficultyMetrics = null;
            _selectedSolutionIndex = 0;
            Repaint();
        }

        private void ValidateTiling()
        {
            RefreshDesignMetrics();
            _tilingNeedsValidation = false;
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

            var playableMask = _snapshot.CreatePlayableMask();
            _tilingResult = ShapeLevelTilingSolver.EnumerateSolutions(
                playableMask, blockSchemas,
                ShapeTilingValidationSettings.instance.MaxSearchNodes,
                ShapeTilingValidationSettings.instance.MaxStoredSolutions);
            if (_tilingResult.IsFeasible)
                _difficultyMetrics = ShapePlaysetDifficultyEvaluator.Evaluate(
                    playableMask, _snapshot.ObstacleCellCount, _snapshot.VoidCellCount, blockSchemas,
                    _tilingResult.SolutionCount, _tilingResult.IsSearchComplete);
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
            CheckDefinitionFingerprint();
            if (_tilingValidationErrors != null)
            {
                EditorGUILayout.HelpBox(
                    "Tiling check not run because the level data is invalid:\n" +
                    FormatValidationErrors(_tilingValidationErrors), MessageType.Error);
                return;
            }
            if (_tilingNeedsValidation && _tilingResult == null)
            {
                EditorGUILayout.HelpBox(
                    "The level definition changed since the last tiling check. Run Validate Tiling to refresh the solved preview.",
                    MessageType.Info);
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
                ShapeLevelTilingStatus.Feasible =>
                    $"{_tilingResult.SolutionCount} distinct solution(s) found.",
                ShapeLevelTilingStatus.Infeasible => "No complete tiling exists for this exact block list.",
                ShapeLevelTilingStatus.SearchLimitReached =>
                    _tilingResult.IsFeasible
                        ? $"Search limit reached; at least {_tilingResult.SolutionCount} distinct solution(s) found. " +
                          "Adjust the shared budget in Project Settings → Fit Me → Shape Level Validation to continue searching."
                        : "Search limit reached before a solution was found; result is inconclusive. Adjust the shared budget in Project Settings → Fit Me → Shape Level Validation.",
                _ => "The solver rejected the supplied board or block data."
            };
            EditorGUILayout.HelpBox(
                $"{message}\nStatus: {_tilingResult.Status} · Search nodes: {_tilingResult.NodesVisited}",
                messageType);
            if (_tilingResult.IsFeasible)
            {
                DrawDifficultyMetrics();
                if (_tilingResult.Solutions.Count > 0)
                {
                    DrawSolutionNavigation();
                    DrawSolvedTilingPreview(_tilingResult.Solutions[_selectedSolutionIndex]);
                }
                else
                    EditorGUILayout.HelpBox(
                        "A solution was verified, but no preview could be retained under the current settings. Increase Maximum Stored Solution Previews and validate again.",
                        MessageType.Warning);
            }
        }

        private void CheckDefinitionFingerprint()
        {
            var fingerprint = GetDefinitionFingerprint((ShapeLevelDefinition)target);
            if (_validatedDefinitionFingerprint == fingerprint) return;
            _validatedDefinitionFingerprint = fingerprint;
            _tilingNeedsValidation = true;
            _tilingResult = null;
            _difficultyMetrics = null;
            _selectedSolutionIndex = 0;
            _snapshot = ShapeLevelDesignSnapshot.Create((ShapeLevelDefinition)target);
            _tilingValidationErrors = _snapshot.Errors.Count > 0 ? _snapshot.Errors : null;
        }

        private static int GetDefinitionFingerprint(ShapeLevelDefinition level)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + (int)level.BoardSource;
                if (level.BoardSource == ShapeBoardSource.Generated)
                {
                    AddMatrixHash(ref hash, level.GeneratedBoardMask);
                    AddMatrixHash(ref hash, level.GeneratedObstacleMask);
                }
                else
                {
                    var board = level.Board;
                    hash = hash * 31 + (board ? board.GetInstanceID() : 0);
                    if (board)
                    {
                        hash = hash * 31 + board.GridSize.GetHashCode();
                        hash = hash * 31 + (int)board.PresetGridType;
                        hash = hash * 31 + (int)board.ObstacleMode;
                        var customGrid = board.customGrid;
                        if (customGrid != null)
                            foreach (var value in customGrid) hash = hash * 31 + value;
                        var obstacleGrid = board.CustomObstacleData;
                        if (obstacleGrid != null)
                            foreach (var cell in obstacleGrid)
                            {
                                hash = hash * 31 + (cell != null && cell.hasCell ? 1 : 0);
                                hash = hash * 31 + (cell != null && cell.hasObstacle ? 1 : 0);
                            }
                    }
                }

                foreach (var entry in level.PlayerBlocks)
                {
                    if (entry == null)
                    {
                        hash = hash * 31;
                        continue;
                    }
                    hash = hash * 31 + entry.Count;
                    hash = hash * 31 + (int)entry.Source;
                    if (entry.Source == ShapePlayerBlockSource.Preset)
                    {
                        var preset = entry.Preset;
                        hash = hash * 31 + (preset ? preset.GetInstanceID() : 0);
                        if (preset && preset.BlockSchema?.schema != null)
                            AddMatrixHash(ref hash, preset.BlockSchema.schema);
                    }
                    else
                        AddMatrixHash(ref hash, entry.InlineFootprint);
                }
                return hash;
            }
        }

        private static void AddMatrixHash(ref int hash, int[,] matrix)
        {
            if (matrix == null)
            {
                hash = hash * 31;
                return;
            }
            hash = hash * 31 + matrix.GetLength(0);
            hash = hash * 31 + matrix.GetLength(1);
            foreach (var value in matrix) hash = hash * 31 + value;
        }

        private void DrawSolutionNavigation()
        {
            var storedCount = _tilingResult.Solutions.Count;
            if (storedCount == 0) return;
            _selectedSolutionIndex = Mathf.Clamp(_selectedSolutionIndex, 0, storedCount - 1);

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(_selectedSolutionIndex == 0);
            if (GUILayout.Button("◀", GUILayout.Width(32f))) StepSolution(-1);
            EditorGUI.EndDisabledGroup();
            var countLabel = _tilingResult.IsSearchComplete
                ? _tilingResult.SolutionCount > storedCount
                    ? $"Preview {_selectedSolutionIndex + 1} of {storedCount} retained · {_tilingResult.SolutionCount} solutions total"
                    : $"Solution {_selectedSolutionIndex + 1} of {_tilingResult.SolutionCount}"
                : $"Preview {_selectedSolutionIndex + 1} of {storedCount} retained · at least {_tilingResult.SolutionCount} found";
            EditorGUILayout.LabelField(countLabel, EditorStyles.centeredGreyMiniLabel);
            EditorGUI.BeginDisabledGroup(_selectedSolutionIndex >= storedCount - 1);
            if (GUILayout.Button("▶", GUILayout.Width(32f))) StepSolution(1);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
        }

        private void StepSolution(int direction)
        {
            var storedCount = _tilingResult?.Solutions.Count ?? 0;
            if (storedCount == 0) return;
            _selectedSolutionIndex = Mathf.Clamp(_selectedSolutionIndex + direction, 0, storedCount - 1);
            Repaint();
        }

        private void DrawDifficultyMetrics()
        {
            if (_difficultyMetrics == null) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Provisional difficulty heuristic", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Provisional score",
                $"{_difficultyMetrics.ProvisionalDifficultyScore} · score model v{_difficultyMetrics.ScoreModelVersion}");
            EditorGUILayout.LabelField("Playable cells", _difficultyMetrics.PlayableCellCount.ToString());
            EditorGUILayout.LabelField("Obstacle cells", _difficultyMetrics.ObstacleCellCount.ToString());
            EditorGUILayout.LabelField("Void cells", _difficultyMetrics.VoidCellCount.ToString());
            EditorGUILayout.LabelField("Player cells / blocks",
                $"{_difficultyMetrics.PlayerCellCount} / {_difficultyMetrics.BlockCount}");
            EditorGUILayout.LabelField("Legal placements",
                $"{_difficultyMetrics.TotalLegalPlacements} total · {_difficultyMetrics.AverageLegalPlacementsPerBlock:F1} average per block");
            EditorGUILayout.LabelField("Minimum placements per block",
                _difficultyMetrics.MinimumLegalPlacementsPerBlock.ToString());
            EditorGUILayout.LabelField("Maximum placements per block",
                _difficultyMetrics.MaximumLegalPlacementsPerBlock.ToString());
            EditorGUILayout.LabelField("Distinct solutions",
                _difficultyMetrics.IsSolutionCountExact
                    ? _difficultyMetrics.SolutionCount.ToString()
                    : $"at least {_difficultyMetrics.SolutionCount}");
            EditorGUILayout.LabelField("Obstacle ratio", $"{_difficultyMetrics.ObstacleRatio:P1}");
            EditorGUILayout.HelpBox(
                "More distinct solutions reduce the heuristic score. An at-least count uses only the known lower bound, so the score remains provisional.",
                MessageType.None);
            EditorGUILayout.EndVertical();
        }

        private void DrawSolvedTilingPreview(IReadOnlyList<ShapeLevelTilingPlacement> placements)
        {
            if (_snapshot.Width <= 0 || _snapshot.Height <= 0) return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Solved Playset Preview", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Block cells are color-coded by instance; IDs are shown when cell size permits.",
                EditorStyles.miniLabel);

            var blockByCell = new int[_snapshot.Height, _snapshot.Width];
            for (var row = 0; row < _snapshot.Height; row++)
            for (var column = 0; column < _snapshot.Width; column++)
                blockByCell[row, column] = -1;

            foreach (var placement in placements)
            {
                if (placement == null || placement.BlockIndex < 0) continue;
                foreach (var position in placement.Cells)
                {
                    var row = position.x;
                    var column = position.y;
                    if (row < 0 || row >= _snapshot.Height || column < 0 || column >= _snapshot.Width)
                        continue;
                    blockByCell[row, column] = placement.BlockIndex;
                }
            }

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

                var blockIndex = blockByCell[row, column];
                var color = !_snapshot.BoardCells[row, column]
                    ? new Color(0.14f, 0.15f, 0.17f)
                    : _snapshot.Obstacles[row, column]
                        ? new Color(0.78f, 0.25f, 0.19f)
                        : blockIndex >= 0
                            ? GetPlacementColor(blockIndex)
                            : new Color(0.30f, 0.66f, 0.40f);
                EditorGUI.DrawRect(cell, color);
                if (blockIndex >= 0 && cellSize >= 16f)
                    EditorGUI.LabelField(cell, (blockIndex + 1).ToString(), BlockIndexLabelStyle);
            }

            EditorGUILayout.BeginHorizontal();
            DrawLegendItem(new Color(0.30f, 0.66f, 0.40f), "Unassigned");
            DrawLegendItem(new Color(0.78f, 0.25f, 0.19f), "Obstacle");
            DrawLegendItem(new Color(0.14f, 0.15f, 0.17f), "Void");
            EditorGUILayout.EndHorizontal();
        }

        private static Color GetPlacementColor(int blockIndex)
        {
            const float goldenRatio = 0.61803398875f;
            return Color.HSVToRGB(Mathf.Repeat(blockIndex * goldenRatio, 1f), 0.58f, 0.82f);
        }

        private static GUIStyle BlockIndexLabelStyle
        {
            get
            {
                if (_blockIndexLabelStyle != null) return _blockIndexLabelStyle;
                _blockIndexLabelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 9
                };
                _blockIndexLabelStyle.normal.textColor = Color.white;
                return _blockIndexLabelStyle;
            }
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
