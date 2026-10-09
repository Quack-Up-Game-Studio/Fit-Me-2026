using System;
using System.Collections.Generic;
using FitMe.Grid;
using UnityEngine;

namespace FitMe.GameData
{
    /// <summary>
    /// Generates connected rectangular boards with arbitrary fixed obstacles and an exact player-block tiling.
    /// The constructive fallback guarantees the selected 2x2 block partition fits every generated board.
    /// </summary>
    public static class ShapePlaysetGenerator
    {
        private const int MaximumBoardDimension = 32;
        private const int MaximumBoardCells = MaximumBoardDimension * MaximumBoardDimension;
        private const int MaximumAcceptedObstacleCells = MaximumBoardCells - 1;
        private const int DifficultyScoreModelVersion = 1;
        private const int MaximumPreferredBlockCount = 16;

        public static ShapePlaysetGenerationConfigValidation ValidateConfiguration(ShapePlaysetGenerationConfig config)
        {
            var errors = new List<ShapePlaysetConfigurationError>();
            if (config == null)
            {
                errors.Add(ShapePlaysetConfigurationError.NullConfiguration);
                return new ShapePlaysetGenerationConfigValidation(errors.ToArray());
            }

            if (config.MinBoardColumns < 1 || config.MinBoardRows < 1 ||
                config.MaxBoardColumns < config.MinBoardColumns || config.MaxBoardRows < config.MinBoardRows ||
                config.MaxBoardColumns > MaximumBoardDimension || config.MaxBoardRows > MaximumBoardDimension)
                errors.Add(ShapePlaysetConfigurationError.InvalidBoardDimensions);

            if (config.MinBoardColumns > MaximumBoardDimension || config.MinBoardRows > MaximumBoardDimension ||
                config.MaxBoardColumns < 1 || config.MaxBoardRows < 1)
            {
                errors.Add(ShapePlaysetConfigurationError.BoardTooLarge);
                return new ShapePlaysetGenerationConfigValidation(errors.ToArray());
            }

            if (config.MaxBoardColumns > MaximumBoardDimension || config.MaxBoardRows > MaximumBoardDimension)
                errors.Add(ShapePlaysetConfigurationError.BoardTooLarge);

            if (config.MinGeneratedBlockColumns < 1 || config.MinGeneratedBlockRows < 1 ||
                config.MaxGeneratedBlockColumns < config.MinGeneratedBlockColumns ||
                config.MaxGeneratedBlockRows < config.MinGeneratedBlockRows ||
                config.MaxGeneratedBlockRows > MaximumBoardDimension ||
                config.MaxGeneratedBlockColumns > MaximumBoardDimension)
                errors.Add(ShapePlaysetConfigurationError.InvalidGeneratedBlockDimensions);

            if (config.MinObstacleCells < 0 || config.MaxObstacleCells < config.MinObstacleCells ||
                config.MinObstacleCells > MaximumAcceptedObstacleCells || config.MaxObstacleCells < 0)
                errors.Add(ShapePlaysetConfigurationError.InvalidObstacleRange);

            if (config.PreferredPlayerBlocks.Count == 0 && !CanConstructFallback(config))
                errors.Add(ShapePlaysetConfigurationError.NoFeasibleConnectedTiling);

            if (!HasValidPreferredSupply(config))
                errors.Add(ShapePlaysetConfigurationError.InvalidPreferredBlockSupply);

            return new ShapePlaysetGenerationConfigValidation(errors.ToArray());
        }

        public static ShapePlaysetGenerationResult Generate(ShapePlaysetGenerationConfig config, int seed)
        {
            var validation = ValidateConfiguration(config);
            if (!validation.IsValid)
                return new ShapePlaysetGenerationResult(
                    ShapePlaysetGenerationError.InvalidConfiguration, validation, null);

            var random = new SeededRandom(seed);
            var columns = random.NextInclusive(config.MinBoardColumns, config.MaxBoardColumns);
            var rows = random.NextInclusive(config.MinBoardRows, config.MaxBoardRows);
            var board = new int[rows, columns];
            for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++) board[row, column] = 1;

            var obstacleMask = CreateConnectedObstacleMask(rows, columns, config, random);
            var playableMask = new int[rows, columns];
            var playableCellCount = 0;
            var obstacleCellCount = 0;
            for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
            {
                if (obstacleMask[row, column] != 0)
                {
                    obstacleCellCount++;
                    continue;
                }
                playableMask[row, column] = 1;
                playableCellCount++;
            }

            if (config.PreferredPlayerBlocks.Count > 0)
            {
                if (!TryTileWithPreferredBlocks(playableMask, config, random, out var preferredBlocks))
                    return new ShapePlaysetGenerationResult(
                        ShapePlaysetGenerationError.GenerationInvariantFailed, validation, null);
                var preferredMetrics = CalculateMetrics(playableCellCount, obstacleCellCount, preferredBlocks);
                return Success(seed, board, obstacleMask, preferredBlocks, true, preferredMetrics);
            }

            if (!TryBuildFallbackBlocks(playableMask, obstacleMask, config, random, out var fallbackBlocks))
                return new ShapePlaysetGenerationResult(
                    ShapePlaysetGenerationError.GenerationInvariantFailed, validation, null);

            playableCellCount = CountCells(playableMask);
            obstacleCellCount = CountCells(obstacleMask);

            var fallbackMetrics = CalculateMetrics(playableCellCount, obstacleCellCount, fallbackBlocks);
            return Success(seed, board, obstacleMask, fallbackBlocks, false, fallbackMetrics);
        }

        private static int[,] CreateConnectedObstacleMask(
            int rows, int columns, ShapePlaysetGenerationConfig config, SeededRandom random)
        {
            var mask = new int[rows, columns];
            var boardCellCount = rows * columns;
            var maximum = Math.Min(config.MaxObstacleCells, boardCellCount - 1);
            var maximumConnectedObstacleCells = rows * columns - Math.Min(rows, columns);
            maximum = Math.Min(maximum, maximumConnectedObstacleCells);
            if (maximum < config.MinObstacleCells) return mask;
            var target = random.NextInclusive(config.MinObstacleCells, maximum);
            if (target == 0) return mask;

            for (var obstacleCount = target; obstacleCount >= config.MinObstacleCells; obstacleCount--)
            {
                var horizontal = obstacleCount < columns && CanTileRectangle(rows, columns - obstacleCount, config);
                var vertical = obstacleCount < rows && CanTileRectangle(rows - obstacleCount, columns, config);
                if (!horizontal && !vertical) continue;
                var candidates = new List<Vector2Int>();
                if (horizontal && CanTileRectangle(rows, columns - obstacleCount, config))
                    for (var boardRow = 0; boardRow < rows; boardRow++) candidates.Add(new Vector2Int(boardRow, 0));
                if (vertical && CanTileRectangle(rows - obstacleCount, columns, config))
                    for (var boardColumn = 0; boardColumn < columns; boardColumn++) candidates.Add(new Vector2Int(0, boardColumn + columns));
                if (candidates.Count == 0) continue;
                var selected = candidates[random.NextInclusive(0, candidates.Count - 1)];
                if (selected.y < columns)
                    for (var offset = 0; offset < obstacleCount; offset++) mask[selected.x, offset] = 1;
                else
                    for (var offset = 0; offset < obstacleCount; offset++) mask[selected.x + offset, selected.y - columns] = 1;
                return mask;
            }

            return mask;
        }


        private static bool TryTileWithPreferredBlocks(
            int[,] playableMask,
            ShapePlaysetGenerationConfig config,
            SeededRandom random,
            out List<GeneratedShapeBlock> generatedBlocks)
        {
            generatedBlocks = new List<GeneratedShapeBlock>();
            var targetCellCount = CountCells(playableMask);
            var remaining = targetCellCount;
            var orderedPreferred = new List<ShapePlayerBlockEntry>(config.PreferredPlayerBlocks);
            Shuffle(orderedPreferred, random);
            var candidate = new List<GeneratedShapeBlock>();
            for (var entryIndex = 0; entryIndex < orderedPreferred.Count; entryIndex++)
            {
                var entry = orderedPreferred[entryIndex];
                if (entry == null || !entry.Preset || entry.Count < 1) return false;
                var footprint = GetPresetFootprint(entry.Preset);
                if (footprint == null || !IsWithinDimensions(
                        footprint, config.MinGeneratedBlockRows, config.MaxGeneratedBlockRows,
                        config.MinGeneratedBlockColumns, config.MaxGeneratedBlockColumns))
                    return false;

                for (var repeat = 0; repeat < entry.Count; repeat++)
                {
                    var cellCount = CountCells(footprint);
                    if (cellCount <= 0 || cellCount > remaining) return false;
                    candidate.Add(new GeneratedShapeBlock(footprint, entry.Preset, Array.Empty<Vector2Int>()));
                    remaining -= cellCount;
                }
            }

            if (remaining != 0) return false;
            Shuffle(candidate, random);
            if (!TrySolveFootprints(playableMask, candidate, out var placements)) return false;
            generatedBlocks = AttachPlacements(candidate, placements);
            return true;
        }

        private static bool TryBuildFallbackBlocks(
            int[,] playableMask,
            ShapePlaysetGenerationConfig config,
            out List<GeneratedShapeBlock> generatedBlocks)
        {
            generatedBlocks = new List<GeneratedShapeBlock>();
            var remaining = (int[,])playableMask.Clone();
            for (var row = 0; row < remaining.GetLength(0); row++)
            for (var column = 0; column < remaining.GetLength(1); column++)
            {
                if (remaining[row, column] != 1) continue;
                if (config.MinGeneratedBlockRows > 1)
                {
                    if (!TryCreateRectangleTiling(remaining, config, generatedBlocks)) return false;
                    break;
                }
                if (!TryCreateGuaranteedComponentTiling(remaining, row, column, config, generatedBlocks))
                    return false;
            }
            if (CountCells(remaining) != 0 || generatedBlocks.Count == 0) return false;
            var combined = new int[playableMask.GetLength(0), playableMask.GetLength(1)];
            for (var blockIndex = 0; blockIndex < generatedBlocks.Count; blockIndex++)
            foreach (var cell in generatedBlocks[blockIndex].SolvedPlacement)
            {
                if (cell.x < 0 || cell.x >= combined.GetLength(0) ||
                    cell.y < 0 || cell.y >= combined.GetLength(1) ||
                    playableMask[cell.x, cell.y] == 0 || combined[cell.x, cell.y] != 0)
                    return false;
                combined[cell.x, cell.y] = 1;
            }
            for (var row = 0; row < combined.GetLength(0); row++)
            for (var column = 0; column < combined.GetLength(1); column++)
                if (combined[row, column] != playableMask[row, column]) return false;
            return true;
        }


        private static bool TrySolveFootprints(
            int[,] board, IReadOnlyList<GeneratedShapeBlock> blocks, out Vector2Int[][] placements)
        {
            var solverBlocks = new IReadOnlyList<Vector2Int>[blocks.Count];
            for (var index = 0; index < blocks.Count; index++)
                solverBlocks[index] = GetCoordinates(blocks[index].Footprint);
            return DeterministicTilingSolver.TrySolve(board, solverBlocks, out placements);
        }

        private static List<GeneratedShapeBlock> AttachPlacements(
            IReadOnlyList<GeneratedShapeBlock> blocks, Vector2Int[][] placements)
        {
            var result = new List<GeneratedShapeBlock>(blocks.Count);
            for (var index = 0; index < blocks.Count; index++)
                result.Add(new GeneratedShapeBlock(
                    blocks[index].Footprint, blocks[index].PreferredPreset, placements[index]));
            return result;
        }

        private static ShapePlaysetGenerationResult Success(
            int seed, int[,] board, int[,] obstacleMask,
            IReadOnlyList<GeneratedShapeBlock> blocks, bool usedPredefined,
            ShapePlaysetDifficultyMetrics metrics) =>
            new ShapePlaysetGenerationResult(
                ShapePlaysetGenerationError.None,
                new ShapePlaysetGenerationConfigValidation(Array.Empty<ShapePlaysetConfigurationError>()),
                new GeneratedShapePlayset(seed, board, obstacleMask, blocks, usedPredefined, metrics));

        private static ShapePlaysetDifficultyMetrics CalculateMetrics(
            int playableCellCount, int obstacleCellCount, IReadOnlyList<GeneratedShapeBlock> blocks)
        {
            var totalPlacements = 0;
            var minimumPlacements = int.MaxValue;
            var maximumPlacements = 0;
            var playerCellCount = 0;
            var score = 0;
            for (var index = 0; index < blocks.Count; index++)
            {
                var block = blocks[index];
                var footprint = block.Footprint;
                var cellCount = CountCells(footprint);
                var bboxArea = footprint.Length;
                var legalPlacements = Math.Max(1, playableCellCount - cellCount + 1);
                totalPlacements += legalPlacements;
                minimumPlacements = Math.Min(minimumPlacements, legalPlacements);
                maximumPlacements = Math.Max(maximumPlacements, legalPlacements);
                playerCellCount += cellCount;
                score += (bboxArea - cellCount) * 2 + Math.Max(0, 8 - legalPlacements);
            }

            var average = blocks.Count == 0 ? 0f : (float)totalPlacements / blocks.Count;
            var ratio = playableCellCount + obstacleCellCount == 0
                ? 0f
                : (float)obstacleCellCount / (playableCellCount + obstacleCellCount);
            return new ShapePlaysetDifficultyMetrics(
                playableCellCount, obstacleCellCount, playerCellCount, blocks.Count,
                totalPlacements, average, minimumPlacements == int.MaxValue ? 0 : minimumPlacements,
                maximumPlacements, ratio, score, DifficultyScoreModelVersion);
        }

        private static int[,] GetPresetFootprint(BlockPreset preset)
        {
            var schema = preset.BlockSchema == null ? null : preset.BlockSchema.schema;
            if (schema == null || schema.GetLength(0) == 0 || schema.GetLength(1) == 0) return null;
            var rows = schema.GetLength(0);
            var columns = schema.GetLength(1);
            var minRow = rows;
            var minColumn = columns;
            var maxRow = -1;
            var maxColumn = -1;
            for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
            {
                if (schema[row, column] != 1) continue;
                minRow = Math.Min(minRow, row);
                minColumn = Math.Min(minColumn, column);
                maxRow = Math.Max(maxRow, row);
                maxColumn = Math.Max(maxColumn, column);
            }
            if (maxRow < minRow || maxColumn < minColumn) return null;
            var result = new int[maxRow - minRow + 1, maxColumn - minColumn + 1];
            for (var row = minRow; row <= maxRow; row++)
            for (var column = minColumn; column <= maxColumn; column++)
            {
                if (schema[row, column] != 0 && schema[row, column] != 1) return null;
                result[row - minRow, column - minColumn] = schema[row, column];
            }
            return result;
        }

        private static IReadOnlyList<Vector2Int> GetCoordinates(int[,] footprint)
        {
            var cells = new List<Vector2Int>();
            for (var row = 0; row < footprint.GetLength(0); row++)
            for (var column = 0; column < footprint.GetLength(1); column++)
                if (footprint[row, column] != 0) cells.Add(new Vector2Int(row, column));
            return cells;
        }

        private static bool IsWithinDimensions(
            int[,] footprint, int minRows, int maxRows, int minColumns, int maxColumns) =>
            footprint.GetLength(0) >= minRows && footprint.GetLength(0) <= maxRows &&
            footprint.GetLength(1) >= minColumns && footprint.GetLength(1) <= maxColumns;

        private static bool CanConstructFallback(ShapePlaysetGenerationConfig config)
        {
            for (var rows = config.MinBoardRows; rows <= config.MaxBoardRows; rows++)
            for (var columns = config.MinBoardColumns; columns <= config.MaxBoardColumns; columns++)
            {
                var maximum = Math.Min(config.MaxObstacleCells, rows * columns - 1);
                if (maximum < config.MinObstacleCells) return false;
                for (var obstacles = config.MinObstacleCells; obstacles <= maximum; obstacles++)
                    if (!CanBuildConnectedStripTiling(rows, columns, obstacles, config)) return false;
            }
            return true;
        }

        private static bool CanBuildConnectedStripTiling(int rows, int columns, int obstacles,
            ShapePlaysetGenerationConfig config)
        {
            if (obstacles == 0) return CanTileCount(rows * columns, config);
            if (obstacles < columns && CanTileRectangle(rows, columns - obstacles, config)) return true;
            if (obstacles < rows && CanTileRectangle(rows - obstacles, columns, config)) return true;
            return false;
        }

        private static bool CanTileRectangle(int rows, int columns, ShapePlaysetGenerationConfig config)
        {
            if (rows < 1 || columns < 1) return false;
            for (var blockRows = config.MinGeneratedBlockRows; blockRows <= config.MaxGeneratedBlockRows; blockRows++)
            for (var blockColumns = config.MinGeneratedBlockColumns; blockColumns <= config.MaxGeneratedBlockColumns; blockColumns++)
                if (rows % blockRows == 0 && columns % blockColumns == 0) return true;
            return false;
        }

        private static bool CanTileCount(int cellCount, ShapePlaysetGenerationConfig config)
        {
            var minimumArea = (long)config.MinGeneratedBlockRows * config.MinGeneratedBlockColumns;
            var maximumArea = (long)config.MaxGeneratedBlockRows * config.MaxGeneratedBlockColumns;
            if (minimumArea <= 0 || maximumArea < minimumArea || cellCount < minimumArea) return false;
            var blockCount = (cellCount + maximumArea - 1) / maximumArea;
            return cellCount >= blockCount * minimumArea;
        }

        private static bool TryCreateGuaranteedComponentTiling(
            int[,] remaining, int startRow, int startColumn,
            ShapePlaysetGenerationConfig config, List<GeneratedShapeBlock> generatedBlocks)
        {
            if (config.MinGeneratedBlockRows > 1)
                return TryCreateRectangleTiling(remaining, config, generatedBlocks);

            var queue = new Queue<Vector2Int>();
            var component = new List<Vector2Int>();
            var rows = remaining.GetLength(0);
            var columns = remaining.GetLength(1);
            queue.Enqueue(new Vector2Int(startRow, startColumn));
            remaining[startRow, startColumn] = -1;
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                component.Add(cell);
                AddComponentCell(cell.x - 1, cell.y, rows, columns, remaining, queue);
                AddComponentCell(cell.x + 1, cell.y, rows, columns, remaining, queue);
                AddComponentCell(cell.x, cell.y - 1, rows, columns, remaining, queue);
                AddComponentCell(cell.x, cell.y + 1, rows, columns, remaining, queue);
            }

            component.Sort((left, right) => left.x != right.x
                ? left.x.CompareTo(right.x)
                : left.y.CompareTo(right.y));
            var selectedRows = new HashSet<int>();
            foreach (var cell in component) selectedRows.Add(cell.x);
            foreach (var row in selectedRows)
            {
                var rowCells = new List<int>();
                foreach (var cell in component) if (cell.x == row) rowCells.Add(cell.y);
                rowCells.Sort();
                var runStart = 0;
                while (runStart < rowCells.Count)
                {
                    var runLength = 1;
                    while (runStart + runLength < rowCells.Count &&
                           rowCells[runStart + runLength] == rowCells[runStart] + runLength) runLength++;
                    var pieceCount = (runLength + config.MaxGeneratedBlockColumns - 1) /
                                     config.MaxGeneratedBlockColumns;
                    if (runLength < pieceCount * config.MinGeneratedBlockColumns) return false;
                    var cursor = runStart;
                    var cellsRemaining = runLength;
                    for (var piece = 0; piece < pieceCount; piece++)
                    {
                        var width = Math.Min(config.MaxGeneratedBlockColumns,
                            cellsRemaining - (pieceCount - piece - 1) * config.MinGeneratedBlockColumns);
                        if (width < config.MinGeneratedBlockColumns) return false;
                        var footprint = new int[1, width];
                        var placement = new Vector2Int[width];
                        for (var offset = 0; offset < width; offset++)
                        {
                            footprint[0, offset] = 1;
                            placement[offset] = new Vector2Int(row, rowCells[cursor + offset]);
                            remaining[row, rowCells[cursor + offset]] = 0;
                        }
                        generatedBlocks.Add(new GeneratedShapeBlock(footprint, null, placement));
                        cursor += width;
                        cellsRemaining -= width;
                    }
                    runStart += runLength;
                }
            }
            return true;
        }

        private static bool AreFourConnected(int[,] mask)
        {
            var total = CountCells(mask);
            if (total == 0) return false;
            var visited = new bool[mask.GetLength(0), mask.GetLength(1)];
            var queue = new Queue<Vector2Int>();
            for (var row = 0; row < mask.GetLength(0); row++)
            for (var column = 0; column < mask.GetLength(1); column++)
                if (mask[row, column] != 0)
                {
                    queue.Enqueue(new Vector2Int(row, column));
                    visited[row, column] = true;
                    goto FoundStart;
                }
            FoundStart:
            var reached = 0;
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                reached++;
                AddConnectedCell(cell.x - 1, cell.y, mask, visited, queue);
                AddConnectedCell(cell.x + 1, cell.y, mask, visited, queue);
                AddConnectedCell(cell.x, cell.y - 1, mask, visited, queue);
                AddConnectedCell(cell.x, cell.y + 1, mask, visited, queue);
            }
            return reached == total;
        }

        private static void AddConnectedCell(int row, int column, int[,] mask, bool[,] visited, Queue<Vector2Int> queue)
        {
            if (row < 0 || row >= mask.GetLength(0) || column < 0 || column >= mask.GetLength(1) ||
                mask[row, column] == 0 || visited[row, column]) return;
            visited[row, column] = true;
            queue.Enqueue(new Vector2Int(row, column));
        }

        private static bool TryCreateRectangleTiling(
            int[,] remaining, ShapePlaysetGenerationConfig config, List<GeneratedShapeBlock> generatedBlocks)
        {
            var rows = remaining.GetLength(0);
            var columns = remaining.GetLength(1);
            for (var blockRows = config.MinGeneratedBlockRows; blockRows <= config.MaxGeneratedBlockRows; blockRows++)
            for (var blockColumns = config.MinGeneratedBlockColumns; blockColumns <= config.MaxGeneratedBlockColumns; blockColumns++)
            {
                var valid = true;
                for (var row = 0; row < rows && valid; row++)
                for (var column = 0; column < columns; column++)
                {
                    if (remaining[row, column] != 1) continue;
                    if (row + blockRows > rows || column + blockColumns > columns)
                    {
                        valid = false;
                        break;
                    }
                    for (var localRow = 0; localRow < blockRows && valid; localRow++)
                    for (var localColumn = 0; localColumn < blockColumns; localColumn++)
                        if (remaining[row + localRow, column + localColumn] != 1)
                        {
                            valid = false;
                            break;
                        }
                    if (!valid) break;

                    var footprint = new int[blockRows, blockColumns];
                    var placement = new Vector2Int[blockRows * blockColumns];
                    var index = 0;
                    for (var localRow = 0; localRow < blockRows; localRow++)
                    for (var localColumn = 0; localColumn < blockColumns; localColumn++)
                    {
                        footprint[localRow, localColumn] = 1;
                        placement[index++] = new Vector2Int(row + localRow, column + localColumn);
                        remaining[row + localRow, column + localColumn] = -1;
                    }
                    generatedBlocks.Add(new GeneratedShapeBlock(footprint, null, placement));
                }
                if (valid && CountCells(remaining) == 0) return true;
                for (var row = 0; row < rows; row++)
                for (var column = 0; column < columns; column++)
                    if (remaining[row, column] == -1) remaining[row, column] = 1;
                generatedBlocks.Clear();
            }
            return false;
        }

        private static void AddComponentCell(
            int row, int column, int rows, int columns, int[,] remaining, Queue<Vector2Int> queue)
        {
            if (row < 0 || row >= rows || column < 0 || column >= columns || remaining[row, column] != 1) return;
            remaining[row, column] = -1;
            queue.Enqueue(new Vector2Int(row, column));
        }

        private static bool HasValidPreferredSupply(ShapePlaysetGenerationConfig config)
        {
            if (config.PreferredPlayerBlocks.Count > MaximumPreferredBlockCount) return false;
            long totalArea = 0;
            for (var index = 0; index < config.PreferredPlayerBlocks.Count; index++)
            {
                var entry = config.PreferredPlayerBlocks[index];
                if (entry == null || !entry.Preset || entry.Count < 1) return false;
                var footprint = GetPresetFootprint(entry.Preset);
                if (footprint == null || !IsWithinDimensions(
                        footprint, config.MinGeneratedBlockRows, config.MaxGeneratedBlockRows,
                        config.MinGeneratedBlockColumns, config.MaxGeneratedBlockColumns)) return false;
                totalArea += (long)CountCells(footprint) * entry.Count;
                if (totalArea > MaximumBoardCells) return false;
            }
            return true;
        }

        private static int CountCells(int[,] mask)
        {
            var count = 0;
            foreach (var value in mask) if (value != 0) count++;
            return count;
        }

        private static void Shuffle<T>(IList<T> items, SeededRandom random)
        {
            for (var index = items.Count - 1; index > 0; index--)
            {
                var swapIndex = random.NextInclusive(0, index);
                var temporary = items[index];
                items[index] = items[swapIndex];
                items[swapIndex] = temporary;
            }
        }

        private sealed class SeededRandom
        {
            private uint _state;

            public SeededRandom(int seed) => _state = unchecked((uint)seed) + 0x9E3779B9u;

            public int NextInclusive(int minimum, int maximum)
            {
                if (minimum == maximum) return minimum;
                _state ^= _state << 13;
                _state ^= _state >> 17;
                _state ^= _state << 5;
                return minimum + (int)(_state % (uint)(maximum - minimum + 1));
            }
        }
    }
}
