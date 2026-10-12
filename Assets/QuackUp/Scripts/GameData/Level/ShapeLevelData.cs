using System;
using System.Collections.Generic;
using FitMe.Grid;
using FitMe.Shared;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace FitMe.GameData
{
    public enum ShapeLevelValidationError
    {
        MissingBoard,
        InvalidBoardSize,
        InvalidGridType,
        InvalidCustomGrid,
        InvalidGridValue,
        EmptyPlayableBoard,
        InvalidObstacleGrid,
        ObstacleOnVoidCell,
        NullPlayerBlockPreset,
        InvalidPlayerBlockSchema,
        BlockCapacityMismatch,
        EmptyCatalog,
        MissingCatalogKey,
        NullLevelDefinition,
        NullPlayerBlockEntry,
        InvalidPlayerBlockCount,
    }

    [Serializable]
    public sealed class ShapePlayerBlockEntry
    {
        [SerializeField] private BlockPreset _preset;
        [SerializeField, Min(1)] private int _count = 1;

        public BlockPreset Preset => _preset;
        public int Count => _count;

        public ShapePlayerBlockEntry() { }

        public ShapePlayerBlockEntry(BlockPreset preset, int count)
        {
            _preset = preset;
            _count = count;
        }
    }

    public sealed class ShapeLevelValidationResult
    {
        private readonly List<ShapeLevelValidationError> _errors = new();

        public IReadOnlyList<ShapeLevelValidationError> Errors => _errors;
        public bool IsValid => _errors.Count == 0;

        internal void Add(ShapeLevelValidationError error) => _errors.Add(error);

        internal void AddRange(IReadOnlyList<ShapeLevelValidationError> errors)
        {
            for (var i = 0; i < errors.Count; i++) _errors.Add(errors[i]);
        }
    }

    [CreateAssetMenu(fileName = "Shape Level", menuName = "FitMe/GameData/Level/Shape Level")]
    public sealed class ShapeLevelDefinition : ScriptableObject
    {
        [SerializeField] private GridPreset _board;
        [SerializeField] private List<ShapePlayerBlockEntry> _playerBlocks = new();

        public GridPreset Board => _board;
        public IReadOnlyList<ShapePlayerBlockEntry> PlayerBlocks =>
            _playerBlocks ?? (IReadOnlyList<ShapePlayerBlockEntry>)Array.Empty<ShapePlayerBlockEntry>();

        public ShapeLevelRuntimeData ToRuntimeData()
        {
            var validation = ShapeLevelDataValidator.Validate(this);
            if (!validation.IsValid)
                throw new InvalidOperationException(
                    $"Shape level '{name}' is invalid: {string.Join(", ", validation.Errors)}");

            var runtimePlayerBlocks = new List<BlockPreset>();
            foreach (var entry in PlayerBlocks)
            for (var i = 0; i < entry.Count; i++)
                runtimePlayerBlocks.Add(entry.Preset);

            return new ShapeLevelRuntimeData(
                Board,
                ShapeLevelObstacleBuilder.Build(Board),
                runtimePlayerBlocks);
        }
    }

    [CreateAssetMenu(fileName = "Shape Level Catalog", menuName = "FitMe/GameData/Level/Shape Level Catalog")]
    public sealed class ShapeLevelCatalog : SerializedScriptableObject
    {
        [OdinSerialize] private OrderedDictionary<string, ShapeLevelDefinition> _levels = new();

        private OrderedDictionary<string, ShapeLevelDefinition> Entries =>
            _levels ?? (_levels = new OrderedDictionary<string, ShapeLevelDefinition>());

        public IReadOnlyOrderedDictionary<string, ShapeLevelDefinition> Levels => Entries;

        public bool TryGetById(string levelId, out ShapeLevelDefinition level)
        {
            level = null;
            if (string.IsNullOrEmpty(levelId) || !Entries.TryGetValue(levelId, out var candidate) || !candidate)
                return false;
            level = candidate;
            return true;
        }

        public bool TryGetId(ShapeLevelDefinition level, out string levelId)
        {
            levelId = null;
            if (!level) return false;

            var keys = Entries.Keys.GetEnumerator();
            for (var index = 0; index < Entries.Count; index++)
            {
                if (!keys.MoveNext()) return false;
                if (Entries[index] != level) continue;
                levelId = keys.Current;
                return true;
            }

            return false;
        }

        public IReadOnlyList<ShapeLevelDefinition> GetOrderedLevels()
        {
            var ordered = new List<ShapeLevelDefinition>();
            for (var i = 0; i < Entries.Count; i++)
            {
                var level = Entries[i];
                if (level) ordered.Add(level);
            }
            return ordered;
        }

        public ShapeLevelValidationResult Validate() => ShapeLevelDataValidator.Validate(this);

        internal IReadOnlyOrderedDictionary<string, ShapeLevelDefinition> GetLevelsForValidation() => Entries;
    }

    internal static class ShapeLevelObstacleBuilder
    {
        public static List<ShapeObstacleRuntimeData> Build(GridPreset board)
        {
            var result = new List<ShapeObstacleRuntimeData>();
            if (!board || board.ObstacleMode != ObstacleMode.Custom) return result;

            var obstacleGrid = board.CustomObstacleData;
            if (obstacleGrid == null || obstacleGrid.Length == 0) return result;

            var rows = obstacleGrid.GetLength(0);
            var columns = obstacleGrid.GetLength(1);
            var visited = new bool[rows, columns];
            for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
            {
                var seed = obstacleGrid[row, column];
                if (!IsObstacleCell(seed) || visited[row, column]) continue;

                var cells = new List<Vector2Int>();
                var pending = new Queue<Vector2Int>();
                visited[row, column] = true;
                pending.Enqueue(new Vector2Int(row, column));
                while (pending.Count > 0)
                {
                    var current = pending.Dequeue();
                    cells.Add(current);
                    AddNeighbor(current.x - 1, current.y);
                    AddNeighbor(current.x + 1, current.y);
                    AddNeighbor(current.x, current.y - 1);
                    AddNeighbor(current.x, current.y + 1);
                }

                var minRow = rows;
                var minColumn = columns;
                var maxRow = 0;
                var maxColumn = 0;
                foreach (var cell in cells)
                {
                    minRow = Math.Min(minRow, cell.x);
                    minColumn = Math.Min(minColumn, cell.y);
                    maxRow = Math.Max(maxRow, cell.x);
                    maxColumn = Math.Max(maxColumn, cell.y);
                }

                var schema = new int[maxRow - minRow + 1, maxColumn - minColumn + 1];
                foreach (var cell in cells) schema[cell.x - minRow, cell.y - minColumn] = 1;
                result.Add(new ShapeObstacleRuntimeData(
                    new BlockSchema(schema, 0),
                    new Vector2Int(minRow, minColumn),
                    seed.color,
                    seed.petrified));

                void AddNeighbor(int neighborRow, int neighborColumn)
                {
                    if (neighborRow < 0 || neighborRow >= rows || neighborColumn < 0 || neighborColumn >= columns ||
                        visited[neighborRow, neighborColumn]) return;

                    var neighbor = obstacleGrid[neighborRow, neighborColumn];
                    if (!IsObstacleCell(neighbor) || !HasSameObstacleAppearance(seed, neighbor)) return;
                    visited[neighborRow, neighborColumn] = true;
                    pending.Enqueue(new Vector2Int(neighborRow, neighborColumn));
                }
            }

            return result;
        }

        private static bool IsObstacleCell(ObstacleData cell) => cell != null && cell.hasCell && cell.hasObstacle;

        private static bool HasSameObstacleAppearance(ObstacleData left, ObstacleData right) =>
            left.petrified == right.petrified &&
            left.color == right.color;
    }

    public static class ShapeLevelDataValidator
    {
        public static ShapeLevelValidationResult Validate(ShapeLevelDefinition level)
        {
            var result = new ShapeLevelValidationResult();
            if (!level)
            {
                result.Add(ShapeLevelValidationError.NullLevelDefinition);
                return result;
            }

            if (!level.Board)
            {
                result.Add(ShapeLevelValidationError.MissingBoard);
                return result;
            }

            var board = level.Board;
            var columns = board.GridSize.x;
            var rows = board.GridSize.y;
            if (rows <= 0 || columns <= 0)
            {
                result.Add(ShapeLevelValidationError.InvalidBoardSize);
                return result;
            }

            var playable = new bool[rows, columns];
            var playableCount = 0;
            if (board.PresetGridType == FitMe.Grid.GridType.Rectangle)
            {
                for (var row = 0; row < rows; row++)
                for (var column = 0; column < columns; column++)
                {
                    playable[row, column] = true;
                    playableCount++;
                }
            }
            else if (board.PresetGridType == FitMe.Grid.GridType.Custom)
            {
                if (board.customGrid == null ||
                    board.customGrid.GetLength(0) != rows ||
                    board.customGrid.GetLength(1) != columns)
                {
                    result.Add(ShapeLevelValidationError.InvalidCustomGrid);
                    return result;
                }

                for (var row = 0; row < rows; row++)
                for (var column = 0; column < columns; column++)
                {
                    var value = board.customGrid[row, column];
                    if (value != 0 && value != 1)
                        result.Add(ShapeLevelValidationError.InvalidGridValue);
                    playable[row, column] = value == 1;
                    if (playable[row, column]) playableCount++;
                }
            }
            else
            {
                result.Add(ShapeLevelValidationError.InvalidGridType);
                return result;
            }

            if (playableCount == 0)
            {
                result.Add(ShapeLevelValidationError.EmptyPlayableBoard);
                return result;
            }

            var obstacleCellCount = 0;
            if (board.ObstacleMode == ObstacleMode.Custom && board.CustomObstacleData != null &&
                board.CustomObstacleData.Length > 0)
            {
                var obstacleGrid = board.CustomObstacleData;
                if (obstacleGrid.GetLength(0) != rows || obstacleGrid.GetLength(1) != columns)
                {
                    result.Add(ShapeLevelValidationError.InvalidObstacleGrid);
                }
                else
                for (var row = 0; row < rows; row++)
                for (var column = 0; column < columns; column++)
                {
                    var obstacle = obstacleGrid[row, column];
                    if (obstacle == null || !obstacle.hasObstacle) continue;
                    if (!obstacle.hasCell || !playable[row, column])
                    {
                        result.Add(ShapeLevelValidationError.ObstacleOnVoidCell);
                        continue;
                    }
                    obstacleCellCount++;
                }
            }

            long playerAtomCount = 0;
            foreach (var entry in level.PlayerBlocks)
            {
                if (entry == null)
                {
                    result.Add(ShapeLevelValidationError.NullPlayerBlockEntry);
                    continue;
                }

                var hasValidCount = entry.Count > 0;
                if (!hasValidCount) result.Add(ShapeLevelValidationError.InvalidPlayerBlockCount);

                if (!entry.Preset)
                {
                    result.Add(ShapeLevelValidationError.NullPlayerBlockPreset);
                    continue;
                }

                if (!TryCountPresetCells(entry.Preset, out var blockCellCount))
                {
                    result.Add(ShapeLevelValidationError.InvalidPlayerBlockSchema);
                    continue;
                }

                if (hasValidCount) playerAtomCount += (long)blockCellCount * entry.Count;
            }

            if (playerAtomCount != playableCount - obstacleCellCount)
                result.Add(ShapeLevelValidationError.BlockCapacityMismatch);

            return result;
        }

        public static ShapeLevelValidationResult Validate(ShapeLevelCatalog catalog)
        {
            var result = new ShapeLevelValidationResult();
            if (!catalog)
            {
                result.Add(ShapeLevelValidationError.EmptyCatalog);
                return result;
            }

            var levels = catalog.GetLevelsForValidation();
            if (levels == null || levels.Count == 0)
            {
                result.Add(ShapeLevelValidationError.EmptyCatalog);
                return result;
            }

            var keys = levels.Keys.GetEnumerator();
            for (var i = 0; i < levels.Count; i++)
            {
                if (!keys.MoveNext()) break;
                var key = keys.Current;
                var level = levels[i];
                if (string.IsNullOrWhiteSpace(key)) result.Add(ShapeLevelValidationError.MissingCatalogKey);
                if (!level)
                {
                    result.Add(ShapeLevelValidationError.NullLevelDefinition);
                    continue;
                }

                result.AddRange(Validate(level).Errors);
            }

            return result;
        }

        private static bool TryCountPresetCells(BlockPreset preset, out int occupiedCellCount)
        {
            occupiedCellCount = 0;
            var schema = preset.BlockSchema?.schema;
            if (schema == null || schema.GetLength(0) == 0 || schema.GetLength(1) == 0)
                return false;

            var isValid = true;
            for (var row = 0; row < schema.GetLength(0); row++)
            for (var column = 0; column < schema.GetLength(1); column++)
            {
                var value = schema[row, column];
                if (value == 1) occupiedCellCount++;
                else if (value != 0) isValid = false;
            }

            return isValid && occupiedCellCount > 0;
        }
    }
}
