using System;
using System.Collections.Generic;
using FitMe.GameData;
using FitMe.Grid;
using UnityEditor;
using UnityEngine;

namespace FitMe.GameData.Editor
{
    public sealed class ShapePlaysetGenerationEditorWindow : EditorWindow
    {
        private static readonly Color[] BlockPalette =
        {
            new(0.20f, 0.56f, 0.86f),
            new(0.34f, 0.72f, 0.42f),
            new(0.90f, 0.63f, 0.20f),
            new(0.69f, 0.40f, 0.82f),
            new(0.15f, 0.72f, 0.72f),
            new(0.88f, 0.38f, 0.42f),
            new(0.65f, 0.67f, 0.24f),
            new(0.40f, 0.52f, 0.78f)
        };

        [SerializeField] private ShapePlaysetGenerationProfile _profile;
        [SerializeField] private int _seed = 1;
        [SerializeField] private Vector2 _scrollPosition;
        [SerializeField] private bool _showTechnicalDiagnostics;
        private ShapePlaysetGenerationResult _generationResult;
        private ShapeLevelTilingResult _solutionResult;
        private IReadOnlyList<ShapeLevelTilingPlacement> _generatorVerifiedSolution;
        private ShapePlaysetDifficultyMetrics _difficultyMetrics;
        private int _selectedSolutionIndex;
        private string _generationMessage;
        private string _saveMessage;
        private MessageType _saveMessageType = MessageType.Info;

        [MenuItem("FitMe/Shape Mode/Playset Generator")]
        private static void OpenWindow()
        {
            var window = GetWindow<ShapePlaysetGenerationEditorWindow>("Shape Playset Generator");
            window.minSize = new Vector2(680f, 520f);
            window.Show();
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            EditorGUILayout.LabelField("Shape Playset Generator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Generate deterministic, solvable Shape Mode content. Generated boards and custom footprints are saved inline in a new ShapeLevelDefinition; existing level or preset assets are not overwritten.",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            _profile = (ShapePlaysetGenerationProfile)EditorGUILayout.ObjectField(
                "Generation Profile", _profile, typeof(ShapePlaysetGenerationProfile), false);
            EditorGUILayout.BeginHorizontal();
            _seed = EditorGUILayout.IntField("Seed", _seed);
            if (GUILayout.Button("◀", GUILayout.Width(32f))) StepSeed(-1);
            if (GUILayout.Button("▶", GUILayout.Width(32f))) StepSeed(1);
            if (GUILayout.Button("Randomize", GUILayout.Width(90f))) RandomizeSeed();
            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck()) ClearGeneratedResult();

            if (!_profile)
            {
                EditorGUILayout.HelpBox(
                    "Assign a ShapePlaysetGenerationProfile asset. Create one from Assets → Create → FitMe → GameData → Level → Shape Playset Generation Profile.",
                    MessageType.Warning);
                EditorGUILayout.EndScrollView();
                return;
            }

            var config = _profile.CreateConfig();
            var validation = ShapePlaysetGenerator.ValidateConfiguration(config);
            if (!validation.IsValid)
                EditorGUILayout.HelpBox($"Profile configuration is invalid: {FormatConfigurationErrors(validation.Errors)}",
                    MessageType.Error);

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(!validation.IsValid);
            if (GUILayout.Button("Generate / Refresh", GUILayout.Height(30f))) Generate(config);
            EditorGUI.EndDisabledGroup();
            var hasPlayset = _generationResult != null && _generationResult.IsSuccess &&
                             _generationResult.Playset != null;
            EditorGUI.BeginDisabledGroup(!hasPlayset);
            if (GUILayout.Button("Save as New Shape Level…", GUILayout.Height(30f))) SaveGeneratedLevel();
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_generationMessage))
                EditorGUILayout.HelpBox(_generationMessage,
                    _generationResult != null && _generationResult.IsSuccess
                        ? _generationResult.Playset.OptimizationStatus == ShapePlaysetOptimizationStatus.BudgetLimited
                            ? MessageType.Warning
                            : MessageType.Info
                        : MessageType.Error);
            if (!string.IsNullOrEmpty(_saveMessage)) EditorGUILayout.HelpBox(_saveMessage, _saveMessageType);

            if (_generationResult != null && !_generationResult.IsSuccess)
                DrawGenerationFailure(_generationResult);
            else if (hasPlayset)
                DrawGeneratedPreview(_generationResult.Playset,
                    _generationResult.ConfigurationValidation.FeasibilityStatus);

            EditorGUILayout.EndScrollView();
        }

        private void StepSeed(int delta)
        {
            _seed = unchecked(_seed + delta);
            ClearGeneratedResult();
            GUI.changed = true;
        }

        private void RandomizeSeed()
        {
            _seed = Guid.NewGuid().GetHashCode();
            ClearGeneratedResult();
            GUI.changed = true;
        }

        private void ClearGeneratedResult()
        {
            _generationResult = null;
            _solutionResult = null;
            _generatorVerifiedSolution = null;
            _difficultyMetrics = null;
            _selectedSolutionIndex = 0;
            _generationMessage = null;
            _saveMessage = null;
        }

        private void Generate(ShapePlaysetGenerationConfig config)
        {
            _solutionResult = null;
            _generatorVerifiedSolution = null;
            _difficultyMetrics = null;
            _selectedSolutionIndex = 0;
            _generationResult = ShapePlaysetGenerator.Generate(config, _seed);
            _saveMessage = null;
            if (_generationResult.IsSuccess)
            {
                var playset = _generationResult.Playset;
                _generatorVerifiedSolution = CreateVerifiedSolution(playset);
                _solutionResult = AnalyzeSolutions(playset);
                _difficultyMetrics = EvaluateDifficulty(playset, _solutionResult);
                _generationMessage = playset.OptimizationStatus == ShapePlaysetOptimizationStatus.BudgetLimited
                    ? $"Generated a valid layout with seed {playset.Seed} on attempt {playset.AttemptIndex + 1}. " +
                      "The preferred-coverage search reached its limit; this is the best layout found so far, " +
                      "not a proven maximum."
                    : $"Generated successfully with seed {playset.Seed} on attempt {playset.AttemptIndex + 1}.";
            }
            else
            {
                _generationMessage = FormatDesignerFailure(_generationResult);
            }
        }

        private static string FormatDesignerFailure(ShapePlaysetGenerationResult result)
        {
            var attempts = result.AttemptsTried;
            if (result.LastAttemptError == ShapePlaysetGenerationError.TilingBudgetExceeded &&
                result.CandidateFailures.TilingBudgetExhaustions == attempts)
                return $"Couldn’t find a complete block layout within the search limit. " +
                       $"Tried {attempts} boards and reached the tiling search limit on all of them. " +
                       "This doesn’t prove the profile can’t work. Try increasing the tiling search budget, " +
                       "simplifying the board or obstacles, or allowing smaller fallback blocks.";

            if (result.LastAttemptError == ShapePlaysetGenerationError.TilingFailed)
                return $"Couldn’t find a complete block layout for any of the {attempts} candidate boards. " +
                       "This doesn’t prove every board in the profile is impossible. Try allowing smaller " +
                       "fallback blocks, enabling singleton fallback blocks, or reducing obstacles.";

            if (result.LastAttemptError == ShapePlaysetGenerationError.ObstacleRepairBudgetExceeded)
                return $"Couldn’t place a usable obstacle layout within the search limit after {attempts} attempt(s). " +
                       "Try increasing the obstacle-repair budget or reducing the obstacle count.";

            if (result.LastAttemptError == ShapePlaysetGenerationError.ObstacleRepairFailed)
                return $"Couldn’t place obstacles while keeping enough playable space after {attempts} attempt(s). " +
                       "Try reducing the obstacle count or lowering the minimum playable-region size.";

            return $"Couldn’t generate a valid playset after {attempts} attempt(s). " +
                   "Review the profile settings and technical diagnostics below.";
        }

        private void DrawGenerationFailure(ShapePlaysetGenerationResult result)
        {
            _showTechnicalDiagnostics = EditorGUILayout.Foldout(
                _showTechnicalDiagnostics, "Technical diagnostics", true);
            if (!_showTechnicalDiagnostics) return;
            EditorGUILayout.LabelField("Configuration feasibility",
                result.ConfigurationValidation.FeasibilityStatus.ToString());
            if (result.ConfigurationValidation.FeasibilityStatus == ShapePlaysetFeasibilityStatus.Unproven)
                EditorGUILayout.HelpBox(
                    "The bounded candidate search did not prove this profile infeasible; its retry/search budgets may have been exhausted.",
                    MessageType.Warning);
            EditorGUILayout.LabelField("Last attempt", result.LastAttemptError.ToString());
            EditorGUILayout.LabelField("Obstacle repair failures", result.CandidateFailures.ObstacleRepairFailures.ToString());
            EditorGUILayout.LabelField("Obstacle repair budget exhaustions",
                result.CandidateFailures.ObstacleRepairBudgetExhaustions.ToString());
            EditorGUILayout.LabelField("Tiling failures", result.CandidateFailures.TilingFailures.ToString());
            EditorGUILayout.LabelField("Tiling budget exhaustions",
                result.CandidateFailures.TilingBudgetExhaustions.ToString());
            EditorGUILayout.LabelField("Invariant failures",
                result.CandidateFailures.GenerationInvariantFailures.ToString());
        }

        private void DrawGeneratedPreview(
            GeneratedShapePlayset playset,
            ShapePlaysetFeasibilityStatus feasibilityStatus)
        {
            var board = playset.BoardMask;
            var obstacles = playset.ObstacleMask;
            var difficulty = _difficultyMetrics ?? playset.Difficulty;
            EditorGUILayout.Space();
            DrawSolutionNavigation();
            EditorGUILayout.LabelField("Board dimensions",
                $"{board.GetLength(1)} columns × {board.GetLength(0)} rows");
            EditorGUILayout.LabelField("Attempt", $"{playset.AttemptIndex + 1} · generator v{playset.GeneratorVersion}");

            if (position.width >= 900f)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.BeginVertical(GUILayout.Width(380f));
                DrawSolvedBoard(board, obstacles, playset.PlayerBlocks, _solutionResult, _selectedSolutionIndex);
                EditorGUILayout.EndVertical();
                DrawDifficultyMetrics(difficulty, feasibilityStatus);
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                DrawSolvedBoard(board, obstacles, playset.PlayerBlocks, _solutionResult, _selectedSolutionIndex);
                DrawDifficultyMetrics(difficulty, feasibilityStatus);
            }
            EditorGUILayout.HelpBox(
                "Difficulty values are provisional generator heuristics. Saved provenance records the generation origin and does not claim the level remains unedited.",
                MessageType.Info);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Ordered player blocks", EditorStyles.boldLabel);
            for (var index = 0; index < playset.PlayerBlocks.Count; index++) DrawPlayerBlock(index, playset.PlayerBlocks[index]);
        }

        private void DrawSolvedBoard(
            int[,] board, int[,] obstacles,
            System.Collections.Generic.IReadOnlyList<GeneratedShapeBlock> blocks,
            ShapeLevelTilingResult solutionResult, int selectedSolutionIndex)
        {
            var rows = board.GetLength(0);
            var columns = board.GetLength(1);
            var blockAtCell = new int[rows, columns];
            for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++) blockAtCell[row, column] = -1;

            var hasSelectedEnumeratedSolution = solutionResult != null && selectedSolutionIndex >= 0 &&
                                                selectedSolutionIndex < solutionResult.Solutions.Count;
            var hasPlacementSolution = hasSelectedEnumeratedSolution || solutionResult == null;
            if (hasPlacementSolution)
            {
                var placements = hasSelectedEnumeratedSolution
                    ? solutionResult.Solutions[selectedSolutionIndex]
                    : _generatorVerifiedSolution;
                if (placements == null) return;
                foreach (var placement in placements)
                {
                    if (placement == null || placement.BlockIndex < 0 || placement.BlockIndex >= blocks.Count)
                        continue;
                    foreach (var position in placement.Cells)
                    {
                        var row = position.x;
                        var column = position.y;
                        if (row < 0 || row >= rows || column < 0 || column >= columns) continue;
                        blockAtCell[row, column] = placement.BlockIndex;
                    }
                }
            }
            else
            {
                for (var blockIndex = 0; blockIndex < blocks.Count; blockIndex++)
                {
                    var placement = blocks[blockIndex].SolvedPlacement;
                    for (var cellIndex = 0; cellIndex < placement.Length; cellIndex++)
                    {
                        var row = placement[cellIndex].x;
                        var column = placement[cellIndex].y;
                        if (row < 0 || row >= rows || column < 0 || column >= columns) continue;
                        blockAtCell[row, column] = blockIndex;
                    }
                }
            }

            const float maxPreviewSide = 360f;
            var cellSize = Mathf.Max(6f, maxPreviewSide / Mathf.Max(rows, columns));
            var boardRect = GUILayoutUtility.GetRect(
                columns * cellSize, rows * cellSize,
                GUILayout.Width(columns * cellSize), GUILayout.Height(rows * cellSize));
            for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
            {
                var cellRect = new Rect(
                    boardRect.x + column * cellSize, boardRect.y + row * cellSize,
                    cellSize, cellSize);
                EditorGUI.DrawRect(cellRect, new Color(0.04f, 0.05f, 0.07f));
                cellRect.xMin += 1f;
                cellRect.yMin += 1f;
                cellRect.xMax -= 1f;
                cellRect.yMax -= 1f;

                Color color;
                if (board[row, column] == 0)
                    color = new Color(0.12f, 0.13f, 0.15f);
                else if (obstacles[row, column] == 1)
                    color = new Color(0.76f, 0.24f, 0.18f);
                else if (blockAtCell[row, column] >= 0)
                    color = BlockPalette[blockAtCell[row, column] % BlockPalette.Length];
                else
                    color = new Color(0.36f, 0.38f, 0.42f);
                EditorGUI.DrawRect(cellRect, color);
                if (blockAtCell[row, column] >= 0 && cellSize >= 16f)
                    EditorGUI.LabelField(cellRect, (blockAtCell[row, column] + 1).ToString(), BlockIndexLabelStyle);
            }

            EditorGUILayout.HelpBox(
                "Board cells are colored by solved block; obstacles are red and void cells are dark.",
                MessageType.None);
        }

        private void DrawSolutionNavigation()
        {
            var storedCount = _solutionResult?.Solutions.Count ?? 0;
            _selectedSolutionIndex = storedCount == 0
                ? 0
                : Mathf.Clamp(_selectedSolutionIndex, 0, storedCount - 1);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Solved playset preview", EditorStyles.boldLabel);
            EditorGUI.BeginDisabledGroup(_selectedSolutionIndex <= 0 || storedCount == 0);
            if (GUILayout.Button("◀", GUILayout.Width(32f))) _selectedSolutionIndex--;
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(_selectedSolutionIndex >= storedCount - 1);
            if (GUILayout.Button("▶", GUILayout.Width(32f))) _selectedSolutionIndex++;
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            if (_solutionResult == null) return;
            if (_solutionResult.SolutionCount == 0)
            {
                EditorGUILayout.HelpBox(
                    _solutionResult.IsSearchComplete
                        ? "The generator's verified layout is shown, but the solution checker did not rediscover it."
                        : "The generator's verified layout is shown. Additional solution search reached its budget before finding any enumerated solutions.",
                    MessageType.Warning);
                EditorGUILayout.LabelField(
                    "Verified layout previewed · solution count inconclusive",
                    EditorStyles.miniLabel);
                return;
            }

            if (storedCount == 0)
            {
                EditorGUILayout.LabelField(
                    "Generator-verified layout shown; no solution preview was retained by the analyzer.",
                    EditorStyles.miniLabel);
                return;
            }

            var countLabel = _solutionResult.IsSearchComplete
                ? $"{_solutionResult.SolutionCount} distinct solution(s) found"
                : $"At least {_solutionResult.SolutionCount} distinct solution(s) found";
            var previewLabel = _solutionResult.IsSearchComplete &&
                               _solutionResult.SolutionCount == _solutionResult.Solutions.Count
                ? $"Solution {_selectedSolutionIndex + 1} of {_solutionResult.SolutionCount}"
                : $"Preview {_selectedSolutionIndex + 1} of {_solutionResult.Solutions.Count} retained";
            EditorGUILayout.LabelField(
                $"{countLabel} · {previewLabel} · {_solutionResult.NodesVisited:N0} search nodes",
                EditorStyles.miniLabel);
        }

        private IReadOnlyList<ShapeLevelTilingPlacement> CreateVerifiedSolution(GeneratedShapePlayset playset)
        {
            var placements = new List<ShapeLevelTilingPlacement>(playset.PlayerBlocks.Count);
            for (var blockIndex = 0; blockIndex < playset.PlayerBlocks.Count; blockIndex++)
                placements.Add(new ShapeLevelTilingPlacement(
                    blockIndex, playset.PlayerBlocks[blockIndex].SolvedPlacement));
            return Array.AsReadOnly(placements.ToArray());
        }

        private static ShapeLevelTilingResult AnalyzeSolutions(GeneratedShapePlayset playset)
        {
            var board = playset.BoardMask;
            var obstacles = playset.ObstacleMask;
            var playableMask = new int[board.GetLength(0), board.GetLength(1)];
            for (var row = 0; row < board.GetLength(0); row++)
            for (var column = 0; column < board.GetLength(1); column++)
                playableMask[row, column] = board[row, column] == 1 && obstacles[row, column] == 0 ? 1 : 0;

            var footprints = new List<int[,]>(playset.PlayerBlocks.Count);
            for (var index = 0; index < playset.PlayerBlocks.Count; index++)
                footprints.Add(playset.PlayerBlocks[index].Footprint);
            var settings = ShapeTilingValidationSettings.instance;
            return ShapeLevelTilingSolver.EnumerateSolutions(
                playableMask, footprints, settings.MaxSearchNodes, settings.MaxStoredSolutions);
        }

        private static ShapePlaysetDifficultyMetrics EvaluateDifficulty(
            GeneratedShapePlayset playset, ShapeLevelTilingResult solutionResult)
        {
            var board = playset.BoardMask;
            var obstacles = playset.ObstacleMask;
            var playableMask = new int[board.GetLength(0), board.GetLength(1)];
            var obstacleCount = 0;
            for (var row = 0; row < board.GetLength(0); row++)
            for (var column = 0; column < board.GetLength(1); column++)
            {
                var isObstacle = obstacles[row, column] == 1;
                if (isObstacle) obstacleCount++;
                playableMask[row, column] = board[row, column] == 1 && !isObstacle ? 1 : 0;
            }

            var footprints = new List<int[,]>(playset.PlayerBlocks.Count);
            for (var index = 0; index < playset.PlayerBlocks.Count; index++)
                footprints.Add(playset.PlayerBlocks[index].Footprint);
            var discoveredCount = solutionResult?.SolutionCount ?? 0;
            var knownLowerBound = Math.Max(1, discoveredCount);
            var countIsExact = solutionResult != null && solutionResult.IsSearchComplete && discoveredCount > 0;
            return ShapePlaysetDifficultyEvaluator.Evaluate(
                playableMask, obstacleCount, playset.Difficulty.VoidCellCount, footprints,
                knownLowerBound, countIsExact);
        }

        private static GUIStyle _blockIndexLabelStyle;

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

        private static void DrawDifficultyMetrics(
            ShapePlaysetDifficultyMetrics difficulty, ShapePlaysetFeasibilityStatus feasibilityStatus)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Provisional difficulty metrics", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Profile feasibility", feasibilityStatus.ToString());
            if (feasibilityStatus == ShapePlaysetFeasibilityStatus.Unproven)
                EditorGUILayout.HelpBox(
                    "This seeded board is solvable, but it does not prove every profile range is feasible.",
                    MessageType.Warning);
            EditorGUILayout.LabelField("Provisional score",
                $"{difficulty.ProvisionalDifficultyScore} · score model v{difficulty.ScoreModelVersion}");
            EditorGUILayout.LabelField("Playable cells", difficulty.PlayableCellCount.ToString());
            EditorGUILayout.LabelField("Obstacle cells", difficulty.ObstacleCellCount.ToString());
            EditorGUILayout.LabelField("Void cells", difficulty.VoidCellCount.ToString());
            EditorGUILayout.LabelField("Player cells / blocks", $"{difficulty.PlayerCellCount} / {difficulty.BlockCount}");
            EditorGUILayout.LabelField("Legal placements",
                $"{difficulty.TotalLegalPlacements} total · {difficulty.AverageLegalPlacementsPerBlock:F1} average per block");
            EditorGUILayout.LabelField("Minimum placements per block",
                difficulty.MinimumLegalPlacementsPerBlock.ToString());
            EditorGUILayout.LabelField("Maximum placements per block",
                difficulty.MaximumLegalPlacementsPerBlock.ToString());
            EditorGUILayout.LabelField("Obstacle ratio", $"{difficulty.ObstacleRatio:P1}");
            if (difficulty.HasSolutionCount)
            {
                EditorGUILayout.LabelField("Distinct solutions",
                    difficulty.IsSolutionCountExact
                        ? difficulty.SolutionCount.ToString()
                        : $"at least {difficulty.SolutionCount}");
                EditorGUILayout.HelpBox(
                    "More distinct solutions reduce the heuristic score. An at-least count uses only the known lower bound, so the score remains provisional.",
                    MessageType.None);
            }
            EditorGUILayout.EndVertical();
        }

        private static void DrawPlayerBlock(int index, GeneratedShapeBlock block)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            var blockColorRect = GUILayoutUtility.GetRect(
                16f, 16f, GUILayout.Width(16f), GUILayout.Height(16f));
            EditorGUI.DrawRect(blockColorRect, BlockPalette[index % BlockPalette.Length]);
            var source = block.IsPredefined
                ? $"Preset · {(block.PreferredPreset ? block.PreferredPreset.name : "<missing preset>")}"
                : "Generated inline footprint";
            EditorGUILayout.LabelField($"Block {index + 1} · {source}", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Solved cells / clockwise rotation",
                $"{block.SolvedPlacement.Length} · {block.PlacementRotationQuarterTurns} quarter-turn(s)");
            DrawFootprint(block.Footprint);
            EditorGUILayout.EndVertical();
        }

        private static void DrawFootprint(int[,] footprint)
        {
            var rows = footprint.GetLength(0);
            var columns = footprint.GetLength(1);
            const float cellSize = 18f;
            var rect = GUILayoutUtility.GetRect(
                columns * cellSize, rows * cellSize,
                GUILayout.Width(columns * cellSize), GUILayout.Height(rows * cellSize));
            for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
            {
                var cellRect = new Rect(
                    rect.x + column * cellSize, rect.y + row * cellSize,
                    cellSize, cellSize);
                EditorGUI.DrawRect(cellRect, new Color(0.04f, 0.05f, 0.07f));
                cellRect.xMin += 1f;
                cellRect.yMin += 1f;
                cellRect.xMax -= 1f;
                cellRect.yMax -= 1f;
                EditorGUI.DrawRect(cellRect, footprint[row, column] == 1
                    ? new Color(0.30f, 0.68f, 0.80f)
                    : new Color(0.17f, 0.18f, 0.20f));
            }
        }

        private void SaveGeneratedLevel()
        {
            if (_generationResult?.Playset == null) return;
            var defaultName = _profile
                ? $"{_profile.name}_Seed_{_generationResult.Playset.Seed}"
                : $"ShapeLevel_Seed_{_generationResult.Playset.Seed}";
            var assetPath = EditorUtility.SaveFilePanelInProject(
                "Save Generated Shape Level", defaultName, "asset",
                "Choose a new path. Existing level assets will never be overwritten.");
            if (string.IsNullOrEmpty(assetPath)) return;

            try
            {
                var level = ShapePlaysetGeneratedLevelAssetCreator.CreateNewLevelAsset(
                    _generationResult.Playset, assetPath, _difficultyMetrics);
                Selection.activeObject = level;
                EditorGUIUtility.PingObject(level);
                _saveMessage = $"Saved inline ShapeLevelDefinition: {assetPath}";
                _saveMessageType = MessageType.Info;
            }
            catch (Exception exception)
            {
                _saveMessage = exception.Message;
                _saveMessageType = MessageType.Error;
            }
        }

        private static string FormatConfigurationErrors(
            System.Collections.Generic.IReadOnlyList<ShapePlaysetConfigurationError> errors)
        {
            var messages = new string[errors.Count];
            for (var index = 0; index < errors.Count; index++) messages[index] = errors[index].ToString();
            return string.Join(", ", messages);
        }
    }
}
