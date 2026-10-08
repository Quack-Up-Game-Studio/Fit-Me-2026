using System;
using System.Collections.Generic;
using FitMe.Grid;
using FitMe.Shared;
using UnityEngine;

namespace FitMe.GameData
{
    public enum ShapeLevelValidationError
    {
        MissingLevelId,
        InvalidLevelOrder,
        MissingBoard,
        InvalidBoardSize,
        InvalidGridType,
        InvalidCustomGrid,
        InvalidGridValue,
        EmptyPlayableBoard,
        InvalidFootprint,
        NegativeFootprintCell,
        DuplicateFootprintCell,
        FootprintNotNormalized,
        ObstacleOutOfBounds,
        ObstacleOnVoidCell,
        ObstacleOverlap,
        BlockCapacityMismatch,
        EmptyCatalog,
        NullLevelDefinition,
        DuplicateLevelId,
        DuplicateLevelOrder,
        MissingFirstLevel
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

    [Serializable]
    public struct ShapeCell
    {
        [SerializeField] private int _row;
        [SerializeField] private int _column;

        public int Row => _row;
        public int Column => _column;

        public ShapeCell(int row, int column)
        {
            _row = row;
            _column = column;
        }
    }

    [Serializable]
    public sealed class ShapeFootprint
    {
        [SerializeField] private List<ShapeCell> _cells = new();

        public IReadOnlyList<ShapeCell> Cells => _cells ?? (IReadOnlyList<ShapeCell>)Array.Empty<ShapeCell>();

        public ShapeFootprint() { }

        public ShapeFootprint(params ShapeCell[] cells)
        {
            _cells = cells == null ? new List<ShapeCell>() : new List<ShapeCell>(cells);
        }

        public int[,] ToSchema()
        {
            if (Cells.Count == 0) return new int[0, 0];

            var maxRow = 0;
            var maxColumn = 0;
            foreach (var cell in Cells)
            {
                if (cell.Row < 0 || cell.Column < 0)
                    throw new InvalidOperationException("Footprint cells must have non-negative coordinates.");
                maxRow = Math.Max(maxRow, cell.Row);
                maxColumn = Math.Max(maxColumn, cell.Column);
            }

            var schema = new int[maxRow + 1, maxColumn + 1];
            foreach (var cell in Cells) schema[cell.Row, cell.Column] = 1;
            return schema;
        }
    }

    [Serializable]
    public sealed class ShapePlayerBlockDefinition
    {
        [SerializeField] private ShapeFootprint _footprint;
        [SerializeField] private FitMe.Shared.BlockColor _color;
        [SerializeField] private bool _allowRotation = true;
        [SerializeField] private BlockShape _visualStyle = BlockShape.OneByOne;

        public ShapeFootprint Footprint => _footprint;
        public FitMe.Shared.BlockColor Color => _color;
        public bool AllowRotation => _allowRotation;
        public BlockShape VisualStyle => _visualStyle;

        public ShapePlayerBlockDefinition(
            ShapeFootprint footprint,
            FitMe.Shared.BlockColor color,
            bool allowRotation = true,
            BlockShape visualStyle = BlockShape.OneByOne)
        {
            _footprint = footprint;
            _color = color;
            _allowRotation = allowRotation;
            _visualStyle = visualStyle;
        }
    }

    [Serializable]
    public sealed class ShapeObstaclePlacement
    {
        [SerializeField] private ShapeFootprint _footprint;
        [SerializeField] private int _anchorRow;
        [SerializeField] private int _anchorColumn;
        [SerializeField] private FitMe.Shared.BlockColor _color;
        [SerializeField] private bool _petrified = true;
        [SerializeField] private BlockShape _visualStyle = BlockShape.OneByOne;

        public ShapeFootprint Footprint => _footprint;
        public int AnchorRow => _anchorRow;
        public int AnchorColumn => _anchorColumn;
        public FitMe.Shared.BlockColor Color => _color;
        public bool Petrified => _petrified;
        public BlockShape VisualStyle => _visualStyle;

        public ShapeObstaclePlacement(
            ShapeFootprint footprint,
            int anchorRow,
            int anchorColumn,
            FitMe.Shared.BlockColor color,
            bool petrified = true,
            BlockShape visualStyle = BlockShape.OneByOne)
        {
            _footprint = footprint;
            _anchorRow = anchorRow;
            _anchorColumn = anchorColumn;
            _color = color;
            _petrified = petrified;
            _visualStyle = visualStyle;
        }
    }

    [CreateAssetMenu(fileName = "Shape Level", menuName = "FitMe/GameData/Level/Shape Level")]
    public sealed class ShapeLevelDefinition : ScriptableObject
    {
        [SerializeField] private string _levelId;
        [SerializeField] private int _order;
        [SerializeField] private FitMe.Grid.GridPreset _board;
        [SerializeField] private List<ShapeObstaclePlacement> _obstacles = new();
        [SerializeField] private List<ShapePlayerBlockDefinition> _playerBlocks = new();

        public string LevelId => _levelId;
        public int Order => _order;
        public FitMe.Grid.GridPreset Board => _board;
        public IReadOnlyList<ShapeObstaclePlacement> Obstacles =>
            _obstacles ?? (IReadOnlyList<ShapeObstaclePlacement>)Array.Empty<ShapeObstaclePlacement>();
        public IReadOnlyList<ShapePlayerBlockDefinition> PlayerBlocks =>
            _playerBlocks ?? (IReadOnlyList<ShapePlayerBlockDefinition>)Array.Empty<ShapePlayerBlockDefinition>();

        public ShapeLevelRuntimeData ToRuntimeData()
        {
            var validation = ShapeLevelDataValidator.Validate(this);
            if (!validation.IsValid)
                throw new InvalidOperationException($"Cannot create runtime data for invalid Shape Level '{LevelId}': {string.Join(", ", validation.Errors)}");

            var obstacles = new List<ShapeObstacleRuntimeData>(Obstacles.Count);
            foreach (var obstacle in Obstacles)
            {
                obstacles.Add(new ShapeObstacleRuntimeData(
                    new FitMe.Grid.BlockSchema(obstacle.Footprint.ToSchema(), 0),
                    new Vector2Int(obstacle.AnchorRow, obstacle.AnchorColumn),
                    obstacle.Color,
                    obstacle.Petrified,
                    obstacle.VisualStyle));
            }

            var playerBlocks = new List<ShapePlayerBlockRuntimeData>(PlayerBlocks.Count);
            foreach (var block in PlayerBlocks)
            {
                playerBlocks.Add(new ShapePlayerBlockRuntimeData(
                    new FitMe.Grid.BlockSchema(block.Footprint.ToSchema(), 0),
                    block.Color,
                    block.AllowRotation,
                    block.VisualStyle));
            }

            return new ShapeLevelRuntimeData(Board, obstacles, playerBlocks);
        }
    }

    [CreateAssetMenu(fileName = "Shape Level Catalog", menuName = "FitMe/GameData/Level/Shape Level Catalog")]
    public sealed class ShapeLevelCatalog : ScriptableObject
    {
        [SerializeField] private List<ShapeLevelDefinition> _levels = new();

        public IReadOnlyList<ShapeLevelDefinition> Levels =>
            _levels ?? (IReadOnlyList<ShapeLevelDefinition>)Array.Empty<ShapeLevelDefinition>();

        public bool TryGetById(string levelId, out ShapeLevelDefinition level)
        {
            level = null;
            if (string.IsNullOrEmpty(levelId) || _levels == null) return false;

            foreach (var candidate in _levels)
            {
                if (!candidate || !string.Equals(candidate.LevelId, levelId, StringComparison.Ordinal)) continue;
                if (level != null)
                {
                    level = null;
                    return false;
                }

                level = candidate;
            }

            return level != null;
        }

        public bool TryGetByOrder(int order, out ShapeLevelDefinition level)
        {
            level = null;
            if (order <= 0 || _levels == null) return false;

            foreach (var candidate in _levels)
            {
                if (!candidate || candidate.Order != order) continue;
                if (level != null)
                {
                    level = null;
                    return false;
                }

                level = candidate;
            }

            return level != null;
        }

        public IReadOnlyList<ShapeLevelDefinition> GetOrderedLevels()
        {
            var ordered = new List<ShapeLevelDefinition>();
            if (_levels == null) return ordered;

            foreach (var level in _levels)
                if (level) ordered.Add(level);

            ordered.Sort((left, right) =>
            {
                var orderComparison = left.Order.CompareTo(right.Order);
                return orderComparison != 0
                    ? orderComparison
                    : string.Compare(left.LevelId, right.LevelId, StringComparison.Ordinal);
            });
            return ordered;
        }

        public ShapeLevelValidationResult Validate() => ShapeLevelDataValidator.Validate(this);

        internal IReadOnlyList<ShapeLevelDefinition> GetLevelsForValidation() => _levels;
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

            if (string.IsNullOrWhiteSpace(level.LevelId)) result.Add(ShapeLevelValidationError.MissingLevelId);
            if (level.Order <= 0) result.Add(ShapeLevelValidationError.InvalidLevelOrder);
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

            var occupied = new bool[rows, columns];
            var obstacleCellCount = 0;
            foreach (var obstacle in level.Obstacles)
            {
                if (obstacle == null || !ValidateFootprint(obstacle.Footprint, result)) continue;

                foreach (var cell in obstacle.Footprint.Cells)
                {
                    var row = obstacle.AnchorRow + cell.Row;
                    var column = obstacle.AnchorColumn + cell.Column;
                    if (row < 0 || row >= rows || column < 0 || column >= columns)
                    {
                        result.Add(ShapeLevelValidationError.ObstacleOutOfBounds);
                        continue;
                    }
                    if (!playable[row, column])
                    {
                        result.Add(ShapeLevelValidationError.ObstacleOnVoidCell);
                        continue;
                    }
                    if (occupied[row, column])
                    {
                        result.Add(ShapeLevelValidationError.ObstacleOverlap);
                        continue;
                    }

                    occupied[row, column] = true;
                    obstacleCellCount++;
                }
            }

            var playerAtomCount = 0;
            foreach (var block in level.PlayerBlocks)
            {
                if (block == null || !ValidateFootprint(block.Footprint, result)) continue;
                playerAtomCount += block.Footprint.Cells.Count;
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
                result.Add(ShapeLevelValidationError.MissingFirstLevel);
                return result;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var orders = new HashSet<int>();
            var hasFirstLevel = false;
            foreach (var level in levels)
            {
                if (!level)
                {
                    result.Add(ShapeLevelValidationError.NullLevelDefinition);
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(level.LevelId) && !ids.Add(level.LevelId))
                    result.Add(ShapeLevelValidationError.DuplicateLevelId);
                if (level.Order > 0 && !orders.Add(level.Order))
                    result.Add(ShapeLevelValidationError.DuplicateLevelOrder);
                if (level.Order == 1) hasFirstLevel = true;
                result.AddRange(Validate(level).Errors);
            }

            if (!hasFirstLevel) result.Add(ShapeLevelValidationError.MissingFirstLevel);
            return result;
        }

        private static bool ValidateFootprint(ShapeFootprint footprint, ShapeLevelValidationResult result)
        {
            if (footprint == null || footprint.Cells.Count == 0)
            {
                result.Add(ShapeLevelValidationError.InvalidFootprint);
                return false;
            }

            var rows = new HashSet<int>();
            var columns = new HashSet<int>();
            var coordinates = new HashSet<long>();
            var isValid = true;
            foreach (var cell in footprint.Cells)
            {
                if (cell.Row < 0 || cell.Column < 0)
                {
                    result.Add(ShapeLevelValidationError.NegativeFootprintCell);
                    isValid = false;
                    continue;
                }

                var key = ((long)cell.Row << 32) | (uint)cell.Column;
                if (!coordinates.Add(key))
                {
                    result.Add(ShapeLevelValidationError.DuplicateFootprintCell);
                    isValid = false;
                }
                rows.Add(cell.Row);
                columns.Add(cell.Column);
            }

            if (isValid && (!rows.Contains(0) || !columns.Contains(0)))
            {
                result.Add(ShapeLevelValidationError.FootprintNotNormalized);
                isValid = false;
            }

            return isValid;
        }
    }
}
