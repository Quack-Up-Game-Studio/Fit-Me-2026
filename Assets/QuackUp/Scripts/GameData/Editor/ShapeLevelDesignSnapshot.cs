using System;
using System.Collections.Generic;
using FitMe.Grid;
using UnityEngine;

namespace FitMe.GameData.Editor
{
    public sealed class ShapeLevelDesignSnapshot
    {
        public bool[,] BoardCells { get; private set; }
        public bool[,] Obstacles { get; private set; }
        public int Width => BoardCells?.GetLength(1) ?? 0;
        public int Height => BoardCells?.GetLength(0) ?? 0;
        public int BoardCellCount { get; private set; }
        public int PlayableCellCount { get; private set; }
        public int ObstacleCellCount { get; private set; }
        public int VoidCellCount => Math.Max(0, Width * Height - BoardCellCount);
        public long PlayerBlockCellCount { get; private set; }
        public long CapacityDifference => PlayableCellCount - PlayerBlockCellCount;
        public IReadOnlyList<ShapeLevelValidationError> Errors => _errors;

        private readonly List<ShapeLevelValidationError> _errors = new();

        public static ShapeLevelDesignSnapshot Create(ShapeLevelDefinition level)
        {
            var snapshot = new ShapeLevelDesignSnapshot();
            if (!level)
            {
                snapshot._errors.Add(ShapeLevelValidationError.NullLevelDefinition);
                return snapshot;
            }

            var validation = ShapeLevelDataValidator.Validate(level);
            for (var index = 0; index < validation.Errors.Count; index++)
                if (!snapshot._errors.Contains(validation.Errors[index]))
                    snapshot._errors.Add(validation.Errors[index]);

            if (level.BoardSource == ShapeBoardSource.Generated)
                snapshot.BuildGeneratedBoard(level);
            else if (level.BoardSource == ShapeBoardSource.Manual)
                snapshot.BuildManualBoard(level);
            else
                snapshot.AddError(ShapeLevelValidationError.InvalidBoardSource);

            snapshot.CountPlayerCells(level);
            return snapshot;
        }

        public int[,] CreatePlayableMask()
        {
            var mask = new int[Height, Width];
            if (BoardCells == null || Obstacles == null) return mask;
            for (var row = 0; row < Height; row++)
            for (var column = 0; column < Width; column++)
                mask[row, column] = BoardCells[row, column] && !Obstacles[row, column] ? 1 : 0;
            return mask;
        }

        private void BuildGeneratedBoard(ShapeLevelDefinition level)
        {
            var board = level.GeneratedBoardMask;
            var obstacles = level.GeneratedObstacleMask;
            if (board == null || board.Length == 0 || obstacles == null ||
                board.GetLength(0) != obstacles.GetLength(0) ||
                board.GetLength(1) != obstacles.GetLength(1))
            {
                BoardCells = new bool[0, 0];
                Obstacles = new bool[0, 0];
                AddError(ShapeLevelValidationError.InvalidGeneratedObstacleMask);
                return;
            }

            if (level.GeneratedBoardSize.x != board.GetLength(1) ||
                level.GeneratedBoardSize.y != board.GetLength(0))
                AddError(ShapeLevelValidationError.InvalidBoardSize);

            BoardCells = new bool[board.GetLength(0), board.GetLength(1)];
            Obstacles = new bool[board.GetLength(0), board.GetLength(1)];
            for (var row = 0; row < Height; row++)
            for (var column = 0; column < Width; column++)
            {
                ValidateMaskValue(board[row, column]);
                ValidateMaskValue(obstacles[row, column]);
                BoardCells[row, column] = board[row, column] == 1;
                Obstacles[row, column] = obstacles[row, column] == 1;
                if (BoardCells[row, column]) BoardCellCount++;
                if (Obstacles[row, column]) ObstacleCellCount++;
                if (BoardCells[row, column] && !Obstacles[row, column]) PlayableCellCount++;
            }
        }

        private void BuildManualBoard(ShapeLevelDefinition level)
        {
            var board = level.Board;
            if (!board)
            {
                BoardCells = new bool[0, 0];
                Obstacles = new bool[0, 0];
                AddError(ShapeLevelValidationError.MissingBoard);
                return;
            }

            var width = board.GridSize.x;
            var height = board.GridSize.y;
            if (width <= 0 || height <= 0)
            {
                BoardCells = new bool[0, 0];
                Obstacles = new bool[0, 0];
                AddError(ShapeLevelValidationError.InvalidBoardSize);
                return;
            }

            BoardCells = new bool[height, width];
            Obstacles = new bool[height, width];
            if (board.PresetGridType == GridType.Rectangle)
            {
                for (var row = 0; row < height; row++)
                for (var column = 0; column < width; column++)
                    BoardCells[row, column] = true;
            }
            else if (board.PresetGridType == GridType.Custom && board.customGrid != null &&
                     board.customGrid.GetLength(0) == height && board.customGrid.GetLength(1) == width)
            {
                for (var row = 0; row < height; row++)
                for (var column = 0; column < width; column++)
                {
                    ValidateMaskValue(board.customGrid[row, column]);
                    BoardCells[row, column] = board.customGrid[row, column] == 1;
                }
            }
            else if (board.PresetGridType == GridType.Custom)
                AddError(ShapeLevelValidationError.InvalidCustomGrid);
            else
                AddError(ShapeLevelValidationError.InvalidGridType);

            if (board.ObstacleMode == ObstacleMode.Custom && board.CustomObstacleData != null &&
                board.CustomObstacleData.Length > 0)
            {
                var obstacleGrid = board.CustomObstacleData;
                if (obstacleGrid.GetLength(0) != height || obstacleGrid.GetLength(1) != width)
                    AddError(ShapeLevelValidationError.InvalidObstacleGrid);
                else
                for (var row = 0; row < height; row++)
                for (var column = 0; column < width; column++)
                {
                    var obstacle = obstacleGrid[row, column];
                    var hasObstacle = obstacle != null && obstacle.hasCell && obstacle.hasObstacle;
                    Obstacles[row, column] = hasObstacle;
                    if (hasObstacle) ObstacleCellCount++;
                }
            }

            for (var row = 0; row < height; row++)
            for (var column = 0; column < width; column++)
            {
                if (BoardCells[row, column]) BoardCellCount++;
                if (BoardCells[row, column] && !Obstacles[row, column]) PlayableCellCount++;
            }
        }

        private void CountPlayerCells(ShapeLevelDefinition level)
        {
            foreach (var entry in level.PlayerBlocks)
            {
                if (entry == null || entry.Count < 1) continue;
                int[,] schema;
                if (entry.Source == ShapePlayerBlockSource.Preset)
                    schema = entry.Preset ? entry.Preset.BlockSchema?.schema : null;
                else if (entry.Source == ShapePlayerBlockSource.InlineFootprint)
                    schema = entry.InlineFootprint;
                else
                    continue;

                if (schema == null) continue;
                var cellCount = 0;
                for (var row = 0; row < schema.GetLength(0); row++)
                for (var column = 0; column < schema.GetLength(1); column++)
                    if (schema[row, column] == 1) cellCount++;
                PlayerBlockCellCount += (long)cellCount * entry.Count;
            }
        }

        private void ValidateMaskValue(int value)
        {
            if (value != 0 && value != 1) AddError(ShapeLevelValidationError.InvalidGridValue);
        }

        private void AddError(ShapeLevelValidationError error)
        {
            if (!_errors.Contains(error)) _errors.Add(error);
        }
    }
}
