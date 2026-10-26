using System;
using System.Collections.Generic;
using System.Text;
using FitMe.Grid;
using FitMe.Shared;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace FitMe.GameData
{
    [CreateAssetMenu(fileName = "Shape Level", menuName = "FitMe/GameData/Level/Shape Level")]
    [ShowOdinSerializedPropertiesInInspector]
    public sealed class ShapeLevelDefinition : SerializedScriptableObject
    {
        [SerializeField, ShowIf(nameof(BoardSource), ShapeBoardSource.Manual)] private GridPreset _board;
        [SerializeField] private List<ShapePlayerBlockEntry> _playerBlocks = new();
        [SerializeField, HideInInspector] private ShapePlaysetGenerationProvenance _generationProvenance;
        [SerializeField, EnumToggleButtons] private ShapeBoardSource _boardSource;
        [field: SerializeField, MinValue(1), ShowIf(nameof(BoardSource), ShapeBoardSource.Generated)]
        public Vector2Int GeneratedBoardSize { get; private set; } = new(10, 10);

#if UNITY_EDITOR
        [ShowIf(nameof(BoardSource), ShapeBoardSource.Generated)]
        [TableMatrix(SquareCells = true, HorizontalTitle = "Generated Board Mask",
            DrawElementMethod = nameof(DrawGeneratedBoardCell), Transpose = true)]
#endif
        [OdinSerialize] private int[,] _generatedBoardMask = new int[0, 0];

#if UNITY_EDITOR
        [ShowIf(nameof(BoardSource), ShapeBoardSource.Generated)]
        [TableMatrix(SquareCells = true, HorizontalTitle = "Generated Obstacle Mask",
            DrawElementMethod = nameof(DrawGeneratedObstacleCell), Transpose = true)]
#endif
        [OdinSerialize] private int[,] _generatedObstacleMask = new int[0, 0];

        public GridPreset Board => _board;
        public ShapeBoardSource BoardSource => _boardSource;
        public int[,] GeneratedBoardMask => Clone(_generatedBoardMask);
        public int[,] GeneratedObstacleMask => Clone(_generatedObstacleMask);
        [ShowInInspector, ReadOnly, InlineProperty]
        public ShapePlaysetGenerationProvenance GenerationProvenance => _generationProvenance;
        public IReadOnlyList<ShapePlayerBlockEntry> PlayerBlocks =>
            _playerBlocks ?? (IReadOnlyList<ShapePlayerBlockEntry>)Array.Empty<ShapePlayerBlockEntry>();

        public void SetGeneratedContent(GeneratedShapePlayset playset)
        {
            SetGeneratedContent(playset, playset?.Difficulty);
        }

        public void SetGeneratedContent(
            GeneratedShapePlayset playset, ShapePlaysetDifficultyMetrics difficultyMetrics)
        {
            if (playset == null) throw new ArgumentNullException(nameof(playset));
            var playerBlocks = new List<ShapePlayerBlockEntry>(playset.PlayerBlocks.Count);
            var groupsByKey = new Dictionary<string, int>();
            for (var index = 0; index < playset.PlayerBlocks.Count; index++)
            {
                var generatedBlock = playset.PlayerBlocks[index];
                var orientedFootprint = RotateFootprint(
                    generatedBlock.Footprint, generatedBlock.PlacementRotationQuarterTurns);
                var remainsPreset = generatedBlock.IsPredefined &&
                                    generatedBlock.PlacementRotationQuarterTurns % 4 == 0;
                var groupKey = remainsPreset
                    ? "preset:" + generatedBlock.PreferredPreset.GetInstanceID()
                    : "inline:" + (generatedBlock.PreferredPreset
                        ? generatedBlock.PreferredPreset.GetInstanceID().ToString()
                        : string.Empty) + ":" + CreateNormalizedFootprintKey(orientedFootprint);
                if (groupsByKey.TryGetValue(groupKey, out var groupIndex))
                {
                    var existing = playerBlocks[groupIndex];
                    playerBlocks[groupIndex] = existing.Source == ShapePlayerBlockSource.Preset
                        ? new ShapePlayerBlockEntry(existing.Preset, existing.Count + 1)
                        : new ShapePlayerBlockEntry(
                            existing.AppearancePreset, existing.InlineFootprint, existing.Count + 1);
                    continue;
                }

                groupsByKey.Add(groupKey, playerBlocks.Count);
                playerBlocks.Add(remainsPreset
                    ? new ShapePlayerBlockEntry(generatedBlock.PreferredPreset, 1)
                    : new ShapePlayerBlockEntry(
                        generatedBlock.PreferredPreset, orientedFootprint, 1));
            }

            SetGeneratedContent(playset.BoardMask, playset.ObstacleMask, playerBlocks);
            _generationProvenance = new ShapePlaysetGenerationProvenance(
                playset, difficultyMetrics ?? playset.Difficulty);
        }

        private static string CreateNormalizedFootprintKey(int[,] footprint)
        {
            if (footprint == null || footprint.Length == 0) return "0x0:";
            var minRow = footprint.GetLength(0);
            var minColumn = footprint.GetLength(1);
            var maxRow = -1;
            var maxColumn = -1;
            for (var row = 0; row < footprint.GetLength(0); row++)
            for (var column = 0; column < footprint.GetLength(1); column++)
            {
                if (footprint[row, column] == 0) continue;
                minRow = Math.Min(minRow, row);
                minColumn = Math.Min(minColumn, column);
                maxRow = Math.Max(maxRow, row);
                maxColumn = Math.Max(maxColumn, column);
            }
            if (maxRow < minRow || maxColumn < minColumn) return "0x0:";

            var rows = maxRow - minRow + 1;
            var columns = maxColumn - minColumn + 1;
            var key = new StringBuilder(rows * columns + 16);
            key.Append(rows).Append('x').Append(columns).Append(':');
            for (var row = minRow; row <= maxRow; row++)
            for (var column = minColumn; column <= maxColumn; column++)
                key.Append(footprint[row, column] == 0 ? '0' : '1');
            return key.ToString();
        }

        private static int[,] RotateFootprint(int[,] footprint, int clockwiseQuarterTurns)
        {
            if (footprint == null || footprint.Length == 0) return new int[0, 0];
            var rotated = Clone(footprint);
            var turns = ((clockwiseQuarterTurns % 4) + 4) % 4;
            for (var turn = 0; turn < turns; turn++)
            {
                var rows = rotated.GetLength(0);
                var columns = rotated.GetLength(1);
                var next = new int[columns, rows];
                for (var row = 0; row < rows; row++)
                for (var column = 0; column < columns; column++)
                    next[column, rows - row - 1] = rotated[row, column];
                rotated = next;
            }
            return rotated;
        }

        public void SetGeneratedContent(
            int[,] boardMask,
            int[,] obstacleMask,
            IReadOnlyList<ShapePlayerBlockEntry> playerBlocks)
        {
            _generationProvenance = null;
            _boardSource = ShapeBoardSource.Generated;
            GeneratedBoardSize = boardMask == null
                ? Vector2Int.zero
                : new Vector2Int(boardMask.GetLength(1), boardMask.GetLength(0));
            _generatedBoardMask = Clone(boardMask);
            _generatedObstacleMask = Clone(obstacleMask);
            _playerBlocks = playerBlocks == null
                ? new List<ShapePlayerBlockEntry>()
                : new List<ShapePlayerBlockEntry>(playerBlocks);
        }

        [Button("Refresh Generated Matrices"), ShowIf(nameof(BoardSource), ShapeBoardSource.Generated)]
        private void RefreshGeneratedMatrices()
        {
            var width = Math.Max(1, GeneratedBoardSize.x);
            var height = Math.Max(1, GeneratedBoardSize.y);
            _generatedBoardMask = ResizeMatrix(_generatedBoardMask, width, height);
            _generatedObstacleMask = ResizeMatrix(_generatedObstacleMask, width, height);
            GeneratedBoardSize = new Vector2Int(width, height);
        }

        public ShapeLevelRuntimeData ToRuntimeData()
        {
            var validation = ShapeLevelDataValidator.Validate(this);
            if (!validation.IsValid)
                throw new InvalidOperationException(
                    $"Shape level '{name}' is invalid: {string.Join(", ", validation.Errors)}");

            var playableMask = BuildPlayableMask();
            var obstacles = BoardSource == ShapeBoardSource.Manual
                ? ShapeLevelObstacleBuilder.Build(Board)
                : ShapeLevelObstacleBuilder.Build(CreateGeneratedObstacleData());
            var runtimePlayerBlocks = new List<ShapePlayerBlockRuntimeEntry>();
            foreach (var entry in PlayerBlocks)
            for (var i = 0; i < entry.Count; i++)
            {
                var schema = entry.Source == ShapePlayerBlockSource.Preset
                    ? entry.Preset.BlockSchema?.schema
                    : entry.InlineFootprint;
                runtimePlayerBlocks.Add(new ShapePlayerBlockRuntimeEntry(
                    entry.Source == ShapePlayerBlockSource.Preset ? entry.Preset : entry.AppearancePreset,
                    schema));
            }

            return new ShapeLevelRuntimeData(
                BoardSource == ShapeBoardSource.Manual ? Board : null,
                playableMask,
                obstacles,
                runtimePlayerBlocks);
        }

        public ShapeLevelRuntimeData ToRuntimeBoardData()
        {
            var validation = ShapeLevelDataValidator.ValidateBoard(this);
            if (!validation.IsValid)
                throw new InvalidOperationException(
                    $"Shape level '{name}' has an invalid board: {string.Join(", ", validation.Errors)}");

            var obstacles = BoardSource == ShapeBoardSource.Manual
                ? ShapeLevelObstacleBuilder.Build(Board)
                : ShapeLevelObstacleBuilder.Build(CreateGeneratedObstacleData());
            return new ShapeLevelRuntimeData(
                BoardSource == ShapeBoardSource.Manual ? Board : null,
                BuildPlayableMask(),
                obstacles,
                Array.Empty<ShapePlayerBlockRuntimeEntry>());
        }

        public void ReplacePlayerBlocks(IReadOnlyList<ShapePlayerBlockEntry> playerBlocks)
        {
            if (playerBlocks == null) throw new ArgumentNullException(nameof(playerBlocks));
            var previous = _playerBlocks;
            _playerBlocks = new List<ShapePlayerBlockEntry>(playerBlocks);
            var validation = ShapeLevelDataValidator.Validate(this);
            if (validation.IsValid) return;
            _playerBlocks = previous;
            throw new InvalidOperationException(
                $"Generated player blocks do not form a valid supply: {string.Join(", ", validation.Errors)}");
        }

        internal int[,] GetGeneratedBoardMaskForValidation() => _generatedBoardMask;
        internal int[,] GetGeneratedObstacleMaskForValidation() => _generatedObstacleMask;

        private int[,] BuildPlayableMask()
        {
            if (BoardSource == ShapeBoardSource.Generated) return Clone(_generatedBoardMask);
            var rows = Board.GridSize.y;
            var columns = Board.GridSize.x;
            if (Board.PresetGridType == FitMe.Grid.GridType.Custom)
                return Clone(Board.customGrid);
            var mask = new int[rows, columns];
            for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++) mask[row, column] = 1;
            return mask;
        }

        private ObstacleData[,] CreateGeneratedObstacleData()
        {
            var rows = _generatedObstacleMask == null ? 0 : _generatedObstacleMask.GetLength(0);
            var columns = _generatedObstacleMask == null ? 0 : _generatedObstacleMask.GetLength(1);
            var result = new ObstacleData[rows, columns];
            for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
            {
                result[row, column] = new ObstacleData
                {
                    hasCell = _generatedBoardMask != null &&
                              row < _generatedBoardMask.GetLength(0) &&
                              column < _generatedBoardMask.GetLength(1) &&
                              _generatedBoardMask[row, column] == 1,
                    hasObstacle = _generatedObstacleMask[row, column] == 1,
                    shape = BlockShape.OneByOne,
                    randomColor = false,
                    color = BlockColor.Red,
                    petrified = true
                };
            }
            return result;
        }

        private static int[,] Clone(int[,] matrix) => matrix == null ? new int[0, 0] : (int[,])matrix.Clone();

        private static int[,] ResizeMatrix(int[,] source, int width, int height)
        {
            var resized = new int[height, width];
            if (source == null) return resized;
            for (var row = 0; row < Math.Min(source.GetLength(0), height); row++)
            for (var column = 0; column < Math.Min(source.GetLength(1), width); column++)
                resized[row, column] = source[row, column];
            return resized;
        }

#if UNITY_EDITOR
        private static int DrawGeneratedBoardCell(Rect rect, int value) => DrawMatrixCell(rect, value, Color.green);
        private static int DrawGeneratedObstacleCell(Rect rect, int value) => DrawMatrixCell(rect, value, Color.red);

        private static int DrawMatrixCell(Rect rect, int value, Color activeColor)
        {
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                value = value == 1 ? 0 : 1;
                GUI.changed = true;
                Event.current.Use();
            }
            EditorGUI.DrawRect(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2),
                value == 1 ? activeColor : Color.grey);
            return value;
        }
#endif
    }
}
