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
        private const int MaximumExpandedPreferredBlockCount = 64;

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
                config.MinObstacleCells > MaximumAcceptedObstacleCells || config.MaxObstacleCells < 0 ||
                (long)config.MinObstacleCells > (long)config.MinBoardColumns * config.MinBoardRows - 1)
                errors.Add(ShapePlaysetConfigurationError.InvalidObstacleRange);

            if (errors.Count == 0 && !CanConstructFallback(config))
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
            if (!TryChooseConstructiveLayout(rows, columns, config, random,
                    out var tileRows, out var tileColumns, out var selectedObstacleCellCount))
                return new ShapePlaysetGenerationResult(
                    ShapePlaysetGenerationError.GenerationInvariantFailed, validation, null);
            var board = new int[rows, columns];
            for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++) board[row, column] = 1;

            var obstacleMask = CreateConnectedObstacleMask(
                rows, columns, tileRows, tileColumns, selectedObstacleCellCount);
            if (obstacleMask == null)
                return new ShapePlaysetGenerationResult(
                    ShapePlaysetGenerationError.GenerationInvariantFailed, validation, null);
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
            if (!AreFourConnected(playableMask))
                return new ShapePlaysetGenerationResult(
                    ShapePlaysetGenerationError.GenerationInvariantFailed, validation, null);

            if (config.PreferredPlayerBlocks.Count > 0)
            {
                if (TryTileWithPreferredBlocks(playableMask, config, random, out var preferredBlocks))
                {
                    var preferredMetrics = CalculateMetrics(playableMask, obstacleCellCount, preferredBlocks);
                    return Success(seed, config, board, obstacleMask, preferredBlocks, true, preferredMetrics);
                }
            }

            if (!TryBuildFallbackBlocks(playableMask, tileRows, tileColumns, out var fallbackBlocks))
                return new ShapePlaysetGenerationResult(
                    ShapePlaysetGenerationError.GenerationInvariantFailed, validation, null);

            playableCellCount = CountCells(playableMask);
            obstacleCellCount = CountCells(obstacleMask);

            var fallbackMetrics = CalculateMetrics(playableMask, obstacleCellCount, fallbackBlocks);
            return Success(seed, config, board, obstacleMask, fallbackBlocks, false, fallbackMetrics);
        }

        private static int[,] CreateConnectedObstacleMask(
            int rows, int columns, int tileRows, int tileColumns, int obstacleCellCount)
        {
            var mask = new int[rows, columns];
            if (obstacleCellCount == 0) return mask;
            if (obstacleCellCount != tileRows * tileColumns ||
                (rows == tileRows && columns == tileColumns)) return null;
            for (var row = rows - tileRows; row < rows; row++)
            for (var column = columns - tileColumns; column < columns; column++)
                mask[row, column] = 1;
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
            int[,] playableMask, int tileRows, int tileColumns,
            out List<GeneratedShapeBlock> generatedBlocks)
        {
            generatedBlocks = new List<GeneratedShapeBlock>();
            var remaining = (int[,])playableMask.Clone();
            if (!TryCreateFixedTileTiling(remaining, tileRows, tileColumns, generatedBlocks)) return false;
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
            int seed, ShapePlaysetGenerationConfig config, int[,] board, int[,] obstacleMask,
            IReadOnlyList<GeneratedShapeBlock> blocks, bool usedPredefined,
            ShapePlaysetDifficultyMetrics metrics) =>
            new ShapePlaysetGenerationResult(
                ShapePlaysetGenerationError.None,
                new ShapePlaysetGenerationConfigValidation(Array.Empty<ShapePlaysetConfigurationError>()),
                new GeneratedShapePlayset(seed, config, board, obstacleMask, blocks, usedPredefined, metrics));

        private static ShapePlaysetDifficultyMetrics CalculateMetrics(
            int[,] playableMask, int obstacleCellCount, IReadOnlyList<GeneratedShapeBlock> blocks)
        {
            var playableCellCount = CountCells(playableMask);
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
                var legalPlacements = CountLegalPlacements(playableMask, footprint);
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

        private static int CountLegalPlacements(int[,] playableMask, int[,] footprint)
        {
            var source = GetCoordinates(footprint);
            var uniqueRotations = new HashSet<string>();
            var rows = playableMask.GetLength(0);
            var columns = playableMask.GetLength(1);
            var count = 0;
            for (var rotation = 0; rotation < 4; rotation++)
            {
                var rotated = new Vector2Int[source.Count];
                var minRow = int.MaxValue;
                var minColumn = int.MaxValue;
                for (var index = 0; index < source.Count; index++)
                {
                    var cell = source[index];
                    for (var turn = 0; turn < rotation; turn++)
                        cell = new Vector2Int(cell.y, -cell.x);
                    rotated[index] = cell;
                    minRow = Math.Min(minRow, cell.x);
                    minColumn = Math.Min(minColumn, cell.y);
                }
                for (var index = 0; index < rotated.Length; index++)
                    rotated[index] -= new Vector2Int(minRow, minColumn);
                Array.Sort(rotated, (left, right) => left.x != right.x
                    ? left.x.CompareTo(right.x)
                    : left.y.CompareTo(right.y));
                var shapeKey = string.Join(";", Array.ConvertAll(rotated,
                    cell => cell.x + "," + cell.y));
                if (!uniqueRotations.Add(shapeKey)) continue;

                for (var originRow = 0; originRow < rows; originRow++)
                for (var originColumn = 0; originColumn < columns; originColumn++)
                {
                    if (playableMask[originRow, originColumn] == 0) continue;
                    var fits = true;
                    for (var index = 0; index < rotated.Length; index++)
                    {
                        var row = originRow + rotated[index].x;
                        var column = originColumn + rotated[index].y;
                        if (row >= rows || column >= columns || playableMask[row, column] == 0)
                        {
                            fits = false;
                            break;
                        }
                    }
                    if (fits) count++;
                }
            }
            return count;
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
                if (!HasConstructiveLayout(rows, columns, config)) return false;
            }
            return true;
        }

        private static bool HasConstructiveLayout(int rows, int columns, ShapePlaysetGenerationConfig config)
        {
            for (var tileRows = config.MinGeneratedBlockRows; tileRows <= config.MaxGeneratedBlockRows; tileRows++)
            for (var tileColumns = config.MinGeneratedBlockColumns; tileColumns <= config.MaxGeneratedBlockColumns; tileColumns++)
            {
                if (rows % tileRows != 0 || columns % tileColumns != 0) continue;
                if (config.MinObstacleCells == 0) return true;
                var obstacleArea = tileRows * tileColumns;
                if (obstacleArea >= config.MinObstacleCells && obstacleArea <= config.MaxObstacleCells &&
                    obstacleArea < rows * columns) return true;
            }
            return false;
        }

        private static bool TryChooseConstructiveLayout(
            int rows, int columns, ShapePlaysetGenerationConfig config, SeededRandom random,
            out int tileRows, out int tileColumns, out int obstacleCellCount)
        {
            var layouts = new List<Vector3Int>();
            for (var candidateRows = config.MinGeneratedBlockRows;
                 candidateRows <= config.MaxGeneratedBlockRows; candidateRows++)
            for (var candidateColumns = config.MinGeneratedBlockColumns;
                 candidateColumns <= config.MaxGeneratedBlockColumns; candidateColumns++)
            {
                if (rows % candidateRows != 0 || columns % candidateColumns != 0) continue;
                if (config.MinObstacleCells == 0)
                    layouts.Add(new Vector3Int(candidateRows, candidateColumns, 0));
                var obstacleArea = candidateRows * candidateColumns;
                if (obstacleArea >= config.MinObstacleCells && obstacleArea <= config.MaxObstacleCells &&
                    obstacleArea < rows * columns)
                    layouts.Add(new Vector3Int(candidateRows, candidateColumns, obstacleArea));
            }
            if (layouts.Count == 0)
            {
                tileRows = tileColumns = obstacleCellCount = 0;
                return false;
            }
            var selected = layouts[random.NextInclusive(0, layouts.Count - 1)];
            tileRows = selected.x;
            tileColumns = selected.y;
            obstacleCellCount = selected.z;
            return true;
        }

        private static bool TryCreateFixedTileTiling(
            int[,] remaining, int tileRows, int tileColumns, List<GeneratedShapeBlock> generatedBlocks)
        {
            if (tileRows < 1 || tileColumns < 1 ||
                remaining.GetLength(0) % tileRows != 0 || remaining.GetLength(1) % tileColumns != 0)
                return false;

            for (var row = 0; row < remaining.GetLength(0); row += tileRows)
            for (var column = 0; column < remaining.GetLength(1); column += tileColumns)
            {
                var playable = true;
                var empty = true;
                for (var localRow = 0; localRow < tileRows; localRow++)
                for (var localColumn = 0; localColumn < tileColumns; localColumn++)
                {
                    var value = remaining[row + localRow, column + localColumn];
                    playable &= value == 1;
                    empty &= value == 0;
                }
                if (empty) continue;
                if (!playable) return false;

                var footprint = new int[tileRows, tileColumns];
                var placement = new Vector2Int[tileRows * tileColumns];
                var index = 0;
                for (var localRow = 0; localRow < tileRows; localRow++)
                for (var localColumn = 0; localColumn < tileColumns; localColumn++)
                {
                    footprint[localRow, localColumn] = 1;
                    placement[index++] = new Vector2Int(row + localRow, column + localColumn);
                    remaining[row + localRow, column + localColumn] = 0;
                }
                generatedBlocks.Add(new GeneratedShapeBlock(footprint, null, placement));
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

        private static bool HasValidPreferredSupply(ShapePlaysetGenerationConfig config)
        {
            if (config.PreferredPlayerBlocks.Count > MaximumPreferredBlockCount) return false;
            long totalArea = 0;
            var totalBlockCount = 0;
            for (var index = 0; index < config.PreferredPlayerBlocks.Count; index++)
            {
                var entry = config.PreferredPlayerBlocks[index];
                if (entry == null || !entry.Preset || entry.Count < 1) return false;
                totalBlockCount += entry.Count;
                if (totalBlockCount > MaximumExpandedPreferredBlockCount) return false;
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
