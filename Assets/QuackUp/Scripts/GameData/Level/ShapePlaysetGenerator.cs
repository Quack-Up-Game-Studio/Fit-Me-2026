using System;
using System.Collections.Generic;
using FitMe.Grid;
using UnityEngine;

namespace FitMe.GameData
{
    /// <summary>
    /// Generates deterministic playsets with arbitrary obstacle masks and an exact-cover set of player blocks.
    /// Playzone regions and every occupied block footprint use four-way connectivity.
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
                (long)config.MinObstacleCells > (long)config.MinBoardColumns * config.MinBoardRows - 1 ||
                (long)config.MaxObstacleCells > (long)config.MaxBoardColumns * config.MaxBoardRows - 1)
                errors.Add(ShapePlaysetConfigurationError.InvalidObstacleRange);

            if (config.MinPlayableRegionCells < 1)
                errors.Add(ShapePlaysetConfigurationError.InvalidPlayableRegionThreshold);

            if (config.MaxObstacleRepairNodes < 1 || config.MaxTilingSearchNodes < 1 ||
                config.MaxCandidateAttempts < 1)
                errors.Add(ShapePlaysetConfigurationError.InvalidSearchBudget);

            if (config.MaxSingletonFallbackBlocks < 0)
                errors.Add(ShapePlaysetConfigurationError.InvalidSingletonFallbackLimit);

            if (!HasValidPreferredSupply(config))
                errors.Add(ShapePlaysetConfigurationError.InvalidPreferredBlockSupply);

            return new ShapePlaysetGenerationConfigValidation(
                errors.ToArray(), errors.Count == 0
                    ? ShapePlaysetFeasibilityStatus.Unproven
                    : ShapePlaysetFeasibilityStatus.ProvenInfeasible);
        }

        public static ShapePlaysetGenerationResult Generate(ShapePlaysetGenerationConfig config, int seed)
        {
            var validation = ValidateConfiguration(config);
            if (!validation.IsValid)
                return new ShapePlaysetGenerationResult(
                    ShapePlaysetGenerationError.InvalidConfiguration, validation, null);

            var lastAttemptError = ShapePlaysetGenerationError.GenerationInvariantFailed;
            var candidateFailures = new CandidateFailureAccumulator();
            for (var attempt = 0; attempt < config.MaxCandidateAttempts; attempt++)
            {
                var result = GenerateCandidate(
                    config, seed, attempt, DeriveAttemptSeed(seed, attempt), validation);
                if (result.IsSuccess)
                    return new ShapePlaysetGenerationResult(
                        result.Error, result.ConfigurationValidation, result.Playset, attempt + 1,
                        candidateFailures: candidateFailures.ToSnapshot());
                lastAttemptError = result.Error;
                candidateFailures.Record(result.Error);
            }

            return new ShapePlaysetGenerationResult(
                ShapePlaysetGenerationError.CandidateRetriesExhausted, validation, null,
                config.MaxCandidateAttempts, lastAttemptError, candidateFailures.ToSnapshot());
        }

        private static ShapePlaysetGenerationResult GenerateCandidate(
            ShapePlaysetGenerationConfig config, int seed, int attemptIndex, int attemptSeed,
            ShapePlaysetGenerationConfigValidation validation)
        {
            var dimensionAndCountRandom = new SeededRandom(DeriveAttemptSeed(attemptSeed, 0));
            var obstacleMaskRandom = new SeededRandom(DeriveAttemptSeed(attemptSeed, 1));
            var columns = dimensionAndCountRandom.NextInclusive(
                config.MinBoardColumns, config.MaxBoardColumns);
            var rows = dimensionAndCountRandom.NextInclusive(config.MinBoardRows, config.MaxBoardRows);
            var selectedObstacleCellCount = dimensionAndCountRandom.NextInclusive(
                config.MinObstacleCells, config.MaxObstacleCells);
            var board = new int[rows, columns];
            for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++) board[row, column] = 1;

            var obstacleMask = CreateObstacleMask(
                rows, columns, selectedObstacleCellCount, obstacleMaskRandom);
            if (obstacleMask == null)
                return new ShapePlaysetGenerationResult(
                    ShapePlaysetGenerationError.GenerationInvariantFailed, validation, null);
            if (!TryRepairObstacleMask(obstacleMask, config.MinPlayableRegionCells,
                    config.MaxObstacleRepairNodes, out var repairBudgetExceeded))
                return new ShapePlaysetGenerationResult(
                    repairBudgetExceeded
                        ? ShapePlaysetGenerationError.ObstacleRepairBudgetExceeded
                        : ShapePlaysetGenerationError.ObstacleRepairFailed,
                    validation, null);
            var playableMask = new int[rows, columns];
            for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
            {
                if (obstacleMask[row, column] != 0) continue;
                playableMask[row, column] = 1;
            }
            if (!ArePlayableRegionsAcceptable(obstacleMask, config.MinPlayableRegionCells))
                return new ShapePlaysetGenerationResult(
                    ShapePlaysetGenerationError.GenerationInvariantFailed, validation, null);

            if (!TryBuildFallbackBlocks(playableMask, config, out var generatedBlocks,
                    out var usedPredefined, out var tilingBudgetExceeded))
                return new ShapePlaysetGenerationResult(
                    tilingBudgetExceeded
                        ? ShapePlaysetGenerationError.TilingBudgetExceeded
                        : ShapePlaysetGenerationError.TilingFailed,
                    validation, null);

            var metrics = CalculateMetrics(
                playableMask, CountCells(obstacleMask), generatedBlocks);
            return Success(seed, attemptIndex, config, board, obstacleMask,
                generatedBlocks, usedPredefined, metrics);
        }

        private static int DeriveAttemptSeed(int seed, int attempt)
        {
            unchecked
            {
                var value = (uint)seed + 0x9E3779B9u * (uint)(attempt + 1);
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                value ^= value >> 16;
                return (int)value;
            }
        }

        private static int[,] CreateObstacleMask(
            int rows, int columns, int obstacleCellCount, SeededRandom random)
        {
            var mask = new int[rows, columns];
            if (obstacleCellCount == 0) return mask;
            if (obstacleCellCount < 0 || obstacleCellCount >= rows * columns) return null;

            var cells = new Vector2Int[rows * columns];
            var index = 0;
            for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
                cells[index++] = new Vector2Int(row, column);

            for (var selected = 0; selected < obstacleCellCount; selected++)
            {
                var swapIndex = selected + random.NextInclusive(0, cells.Length - selected - 1);
                var temporary = cells[selected];
                cells[selected] = cells[swapIndex];
                cells[swapIndex] = temporary;
                var cell = cells[selected];
                mask[cell.x, cell.y] = 1;
            }
            return mask;
        }


        private static bool TryBuildFallbackBlocks(
            int[,] playableMask, ShapePlaysetGenerationConfig config,
            out List<GeneratedShapeBlock> generatedBlocks,
            out bool usedPredefined, out bool budgetExceeded)
        {
            generatedBlocks = new List<GeneratedShapeBlock>();
            usedPredefined = false;
            var preferredBlocks = new List<PreferredBlockCandidate>();
            for (var entryIndex = 0; entryIndex < config.PreferredPlayerBlocks.Count; entryIndex++)
            {
                var entry = config.PreferredPlayerBlocks[entryIndex];
                var footprint = GetPresetFootprint(entry.Preset);
                for (var repeat = 0; repeat < entry.Count; repeat++)
                    preferredBlocks.Add(new PreferredBlockCandidate(
                        footprint, entry.Preset, CountCells(footprint)));
            }

            var context = new TilingSearchContext(playableMask, config, preferredBlocks);
            SearchMixedTiling(context, 0, 0, 0, 0);
            budgetExceeded = context.BudgetExceeded;
            if (budgetExceeded || context.BestBlocks == null) return false;

            generatedBlocks = context.BestBlocks;
            usedPredefined = context.BestPreferredBlockCount > 0;
            return true;
        }

        private static void SearchMixedTiling(
            TilingSearchContext context, int occupiedCellCount,
            int preferredCellCount, int preferredBlockCount, int singletonCount)
        {
            if (context.StopSearch || context.BudgetExceeded) return;
            if (!ConsumeTilingNode(ref context.NodesVisited, context.Config.MaxTilingSearchNodes))
            {
                context.BudgetExceeded = true;
                return;
            }

            var anchorIndex = FindFirstUncoveredCell(context.PlayableMask, context.Covered);
            if (anchorIndex < 0)
            {
                context.ConsiderCurrentSolution(preferredCellCount, preferredBlockCount, singletonCount);
                return;
            }
            if (CanPruneTilingBranch(context, occupiedCellCount,
                    preferredCellCount, preferredBlockCount, singletonCount)) return;

            var preferredCandidates = EnumeratePreferredPlacements(context, anchorIndex);
            if (context.BudgetExceeded) return;
            for (var candidateIndex = 0; candidateIndex < preferredCandidates.Count; candidateIndex++)
            {
                var candidate = preferredCandidates[candidateIndex];
                SetCovered(context, candidate.CellIndices, true);
                context.UsedPreferred[candidate.PreferredBlockIndex] = true;
                context.CurrentBlocks.Add(new GeneratedShapeBlock(
                    candidate.Footprint, candidate.PreferredPreset, candidate.Placement,
                    candidate.PlacementRotationQuarterTurns));
                SearchMixedTiling(context, occupiedCellCount + candidate.CellIndices.Length,
                    preferredCellCount + candidate.CellIndices.Length, preferredBlockCount + 1,
                    singletonCount);
                context.CurrentBlocks.RemoveAt(context.CurrentBlocks.Count - 1);
                context.UsedPreferred[candidate.PreferredBlockIndex] = false;
                SetCovered(context, candidate.CellIndices, false);
                if (context.StopSearch || context.BudgetExceeded) return;
            }

            var fallbackCandidates = EnumerateFallbackCandidates(
                context.PlayableMask, context.Covered, anchorIndex,
                singletonCount < context.Config.MaxSingletonFallbackBlocks,
                context.Config, ref context.NodesVisited, out var budgetExceeded);
            if (budgetExceeded)
            {
                context.BudgetExceeded = true;
                return;
            }
            for (var candidateIndex = 0; candidateIndex < fallbackCandidates.Count; candidateIndex++)
            {
                var candidate = fallbackCandidates[candidateIndex];
                SetCovered(context, candidate.CellIndices, true);
                context.CurrentBlocks.Add(new GeneratedShapeBlock(
                    candidate.Footprint, null, candidate.Placement,
                    candidate.PlacementRotationQuarterTurns));
                var isSingleton = candidate.CellIndices.Length == 1;
                SearchMixedTiling(context, occupiedCellCount + candidate.CellIndices.Length,
                    preferredCellCount, preferredBlockCount,
                    singletonCount + (isSingleton ? 1 : 0));
                context.CurrentBlocks.RemoveAt(context.CurrentBlocks.Count - 1);
                SetCovered(context, candidate.CellIndices, false);
                if (context.StopSearch || context.BudgetExceeded) return;
            }
        }

        private static bool CanPruneTilingBranch(
            TilingSearchContext context, int occupiedCellCount,
            int preferredCellCount, int preferredBlockCount, int singletonCount)
        {
            if (context.BestBlocks == null) return false;
            var remainingPreferredCells = 0;
            var remainingPreferredCount = 0;
            for (var index = 0; index < context.PreferredBlocks.Count; index++)
            {
                if (context.UsedPreferred[index]) continue;
                remainingPreferredCells += context.PreferredBlocks[index].CellCount;
                remainingPreferredCount++;
            }
            var uncoveredCellCount = context.TotalPlayableCells - occupiedCellCount;
            var maximumPreferredCells = preferredCellCount +
                Math.Min(remainingPreferredCells, uncoveredCellCount);
            if (maximumPreferredCells < context.BestPreferredCellCount) return true;
            if (maximumPreferredCells > context.BestPreferredCellCount) return false;

            var maximumPreferredBlocks = preferredBlockCount + remainingPreferredCount;
            if (maximumPreferredBlocks < context.BestPreferredBlockCount) return true;
            return maximumPreferredBlocks == context.BestPreferredBlockCount &&
                   singletonCount >= context.BestSingletonCount;
        }

        private static List<TilingPlacementCandidate> EnumeratePreferredPlacements(
            TilingSearchContext context, int anchorIndex)
        {
            var candidates = new List<TilingPlacementCandidate>();
            var seenCandidates = new HashSet<string>();
            var columns = context.PlayableMask.GetLength(1);
            var anchorRow = anchorIndex / columns;
            var anchorColumn = anchorIndex % columns;
            for (var preferredIndex = 0; preferredIndex < context.PreferredBlocks.Count; preferredIndex++)
            {
                if (context.UsedPreferred[preferredIndex] || HasEarlierAvailableDuplicate(context, preferredIndex))
                    continue;
                var preferred = context.PreferredBlocks[preferredIndex];
                var source = GetCoordinates(preferred.Footprint);
                var seenRotations = new HashSet<string>();
                for (var rotation = 0; rotation < 4; rotation++)
                {
                    var rotated = RotateCoordinates(source, rotation);
                    var rotationKey = CreateCoordinateShapeKey(rotated);
                    if (!seenRotations.Add(rotationKey)) continue;
                    for (var offsetIndex = 0; offsetIndex < rotated.Length; offsetIndex++)
                    {
                        var originRow = anchorRow - rotated[offsetIndex].x;
                        var originColumn = anchorColumn - rotated[offsetIndex].y;
                        var cellIndices = new int[rotated.Length];
                        var placement = new Vector2Int[rotated.Length];
                        var fits = true;
                        for (var cellIndex = 0; cellIndex < rotated.Length; cellIndex++)
                        {
                            var row = originRow + rotated[cellIndex].x;
                            var column = originColumn + rotated[cellIndex].y;
                            if (row < 0 || row >= context.PlayableMask.GetLength(0) ||
                                column < 0 || column >= columns ||
                                context.PlayableMask[row, column] == 0 || context.Covered[row, column])
                            {
                                fits = false;
                                break;
                            }
                            cellIndices[cellIndex] = row * columns + column;
                            placement[cellIndex] = new Vector2Int(row, column);
                        }
                        if (!fits) continue;
                        Array.Sort(cellIndices);
                        Array.Sort(placement, (left, right) => left.x != right.x
                            ? left.x.CompareTo(right.x)
                            : left.y.CompareTo(right.y));
                        var key = preferredIndex + ":" + CreateShapeKey(cellIndices);
                        if (!seenCandidates.Add(key)) continue;
                        if (!ConsumeTilingNode(ref context.NodesVisited, context.Config.MaxTilingSearchNodes))
                        {
                            context.BudgetExceeded = true;
                            return candidates;
                        }
                        candidates.Add(new TilingPlacementCandidate(
                            cellIndices, preferred.Footprint, preferred.Preset, placement,
                            preferredIndex, rotation, key));
                    }
                }
            }
            candidates.Sort((left, right) =>
            {
                var area = right.CellIndices.Length.CompareTo(left.CellIndices.Length);
                if (area != 0) return area;
                var preferredIndex = left.PreferredBlockIndex.CompareTo(right.PreferredBlockIndex);
                return preferredIndex != 0 ? preferredIndex : string.CompareOrdinal(left.Key, right.Key);
            });
            return candidates;
        }

        private static bool HasEarlierAvailableDuplicate(TilingSearchContext context, int preferredIndex)
        {
            var current = context.PreferredBlocks[preferredIndex];
            for (var index = 0; index < preferredIndex; index++)
                if (!context.UsedPreferred[index] &&
                    ReferenceEquals(context.PreferredBlocks[index].Preset, current.Preset)) return true;
            return false;
        }

        private static Vector2Int[] RotateCoordinates(IReadOnlyList<Vector2Int> source, int rotation)
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
            return rotated;
        }

        private static string CreateCoordinateShapeKey(IReadOnlyList<Vector2Int> coordinates)
        {
            var parts = new string[coordinates.Count];
            for (var index = 0; index < coordinates.Count; index++)
                parts[index] = coordinates[index].x + "," + coordinates[index].y;
            return string.Join(";", parts);
        }

        private static void SetCovered(TilingSearchContext context, IReadOnlyList<int> cellIndices, bool covered)
        {
            var columns = context.PlayableMask.GetLength(1);
            for (var index = 0; index < cellIndices.Count; index++)
                context.Covered[cellIndices[index] / columns, cellIndices[index] % columns] = covered;
        }

        private static List<FallbackShapeCandidate> EnumerateFallbackCandidates(
            int[,] playableMask, bool[,] covered, int anchorIndex, bool allowSingleton,
            ShapePlaysetGenerationConfig config, ref int nodesVisited, out bool budgetExceeded)
        {
            var candidates = new List<FallbackShapeCandidate>();
            budgetExceeded = false;
            var columns = playableMask.GetLength(1);
            var shapeCells = new List<int> { anchorIndex };
            var shapeMembership = new HashSet<int> { anchorIndex };
            var visitedShapes = new HashSet<string>();
            ExploreFallbackShapes(playableMask, covered, config, allowSingleton,
                shapeCells, shapeMembership, visitedShapes, candidates,
                ref nodesVisited, out budgetExceeded);
            if (budgetExceeded) return candidates;

            candidates.Sort((left, right) =>
            {
                var area = right.CellIndices.Length.CompareTo(left.CellIndices.Length);
                if (area != 0) return area;
                return string.CompareOrdinal(left.Key, right.Key);
            });
            return candidates;
        }

        private static void ExploreFallbackShapes(
            int[,] playableMask, bool[,] covered, ShapePlaysetGenerationConfig config,
            bool allowSingleton, List<int> shapeCells, HashSet<int> shapeMembership,
            HashSet<string> visitedShapes, List<FallbackShapeCandidate> candidates,
            ref int nodesVisited, out bool budgetExceeded)
        {
            budgetExceeded = false;
            var key = CreateShapeKey(shapeCells);
            if (!visitedShapes.Add(key)) return;
            if (!ConsumeTilingNode(ref nodesVisited, config.MaxTilingSearchNodes))
            {
                budgetExceeded = true;
                return;
            }

            var bounds = GetCellBounds(shapeCells, playableMask.GetLength(1));
            var height = bounds.maxRow - bounds.minRow + 1;
            var width = bounds.maxColumn - bounds.minColumn + 1;
            var isSingleton = shapeCells.Count == 1;
            if ((isSingleton && allowSingleton) || (!isSingleton &&
                    (IsWithinDimensions(height, width,
                         config.MinGeneratedBlockRows, config.MaxGeneratedBlockRows,
                         config.MinGeneratedBlockColumns, config.MaxGeneratedBlockColumns) ||
                     IsWithinDimensions(width, height,
                         config.MinGeneratedBlockRows, config.MaxGeneratedBlockRows,
                         config.MinGeneratedBlockColumns, config.MaxGeneratedBlockColumns))))
                candidates.Add(CreateFallbackCandidate(
                    shapeCells, bounds, playableMask.GetLength(1), key, config));

            var maximumDimension = Math.Max(config.MaxGeneratedBlockRows, config.MaxGeneratedBlockColumns);
            if (height > maximumDimension || width > maximumDimension) return;

            var neighbors = new List<int>();
            for (var index = 0; index < shapeCells.Count; index++)
            {
                var cellIndex = shapeCells[index];
                var row = cellIndex / playableMask.GetLength(1);
                var column = cellIndex % playableMask.GetLength(1);
                AddFallbackNeighbor(row - 1, column, playableMask, covered, shapeMembership, neighbors);
                AddFallbackNeighbor(row, column - 1, playableMask, covered, shapeMembership, neighbors);
                AddFallbackNeighbor(row, column + 1, playableMask, covered, shapeMembership, neighbors);
                AddFallbackNeighbor(row + 1, column, playableMask, covered, shapeMembership, neighbors);
            }
            neighbors.Sort();
            for (var index = 0; index < neighbors.Count; index++)
            {
                var neighbor = neighbors[index];
                var row = neighbor / playableMask.GetLength(1);
                var column = neighbor % playableMask.GetLength(1);
                if (Math.Max(bounds.maxRow, row) - Math.Min(bounds.minRow, row) + 1 > maximumDimension ||
                    Math.Max(bounds.maxColumn, column) - Math.Min(bounds.minColumn, column) + 1 > maximumDimension)
                    continue;
                shapeCells.Add(neighbor);
                shapeMembership.Add(neighbor);
                ExploreFallbackShapes(playableMask, covered, config, allowSingleton,
                    shapeCells, shapeMembership, visitedShapes, candidates,
                    ref nodesVisited, out budgetExceeded);
                shapeMembership.Remove(neighbor);
                shapeCells.RemoveAt(shapeCells.Count - 1);
                if (budgetExceeded) return;
            }
        }

        private static void AddFallbackNeighbor(
            int row, int column, int[,] playableMask, bool[,] covered,
            HashSet<int> shapeMembership, List<int> neighbors)
        {
            if (row < 0 || row >= playableMask.GetLength(0) ||
                column < 0 || column >= playableMask.GetLength(1) ||
                playableMask[row, column] == 0 || covered[row, column]) return;
            var index = row * playableMask.GetLength(1) + column;
            if (!shapeMembership.Contains(index)) neighbors.Add(index);
        }

        private static FallbackShapeCandidate CreateFallbackCandidate(
            IReadOnlyList<int> shapeCells, CellBounds bounds, int boardColumns,
            string key, ShapePlaysetGenerationConfig config)
        {
            var height = bounds.maxRow - bounds.minRow + 1;
            var width = bounds.maxColumn - bounds.minColumn + 1;
            var boardFootprint = new int[height, width];
            var placement = new Vector2Int[shapeCells.Count];
            for (var index = 0; index < shapeCells.Count; index++)
            {
                var row = shapeCells[index] / boardColumns;
                var column = shapeCells[index] % boardColumns;
                boardFootprint[row - bounds.minRow, column - bounds.minColumn] = 1;
                placement[index] = new Vector2Int(row, column);
            }
            Array.Sort(placement, (left, right) => left.x != right.x
                ? left.x.CompareTo(right.x)
                : left.y.CompareTo(right.y));

            var footprint = boardFootprint;
            var placementRotationQuarterTurns = 0;
            if (!IsWithinDimensions(height, width,
                    config.MinGeneratedBlockRows, config.MaxGeneratedBlockRows,
                    config.MinGeneratedBlockColumns, config.MaxGeneratedBlockColumns))
            {
                footprint = RotateFootprint(boardFootprint);
                placementRotationQuarterTurns = 3;
            }

            var cellIndices = new int[shapeCells.Count];
            for (var index = 0; index < shapeCells.Count; index++) cellIndices[index] = shapeCells[index];
            Array.Sort(cellIndices);
            return new FallbackShapeCandidate(
                cellIndices, footprint, placement, placementRotationQuarterTurns, key);
        }

        private static int FindFirstUncoveredCell(int[,] playableMask, bool[,] covered)
        {
            for (var row = 0; row < playableMask.GetLength(0); row++)
            for (var column = 0; column < playableMask.GetLength(1); column++)
                if (playableMask[row, column] != 0 && !covered[row, column])
                    return row * playableMask.GetLength(1) + column;
            return -1;
        }

        private static CellBounds GetCellBounds(IReadOnlyList<int> cellIndices, int boardColumns)
        {
            var minRow = int.MaxValue;
            var minColumn = int.MaxValue;
            var maxRow = int.MinValue;
            var maxColumn = int.MinValue;
            for (var index = 0; index < cellIndices.Count; index++)
            {
                var row = cellIndices[index] / boardColumns;
                var column = cellIndices[index] % boardColumns;
                minRow = Math.Min(minRow, row);
                minColumn = Math.Min(minColumn, column);
                maxRow = Math.Max(maxRow, row);
                maxColumn = Math.Max(maxColumn, column);
            }
            return new CellBounds(minRow, minColumn, maxRow, maxColumn);
        }

        private static string CreateShapeKey(IReadOnlyList<int> cellIndices)
        {
            var sorted = new int[cellIndices.Count];
            for (var index = 0; index < cellIndices.Count; index++) sorted[index] = cellIndices[index];
            Array.Sort(sorted);
            return string.Join(",", sorted);
        }

        private static bool ConsumeTilingNode(ref int nodesVisited, int nodeBudget)
        {
            if (nodesVisited >= nodeBudget) return false;
            nodesVisited++;
            return true;
        }

        private static int[,] RotateFootprint(int[,] footprint)
        {
            var rows = footprint.GetLength(0);
            var columns = footprint.GetLength(1);
            var rotated = new int[columns, rows];
            for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
                rotated[column, rows - row - 1] = footprint[row, column];
            return rotated;
        }


        private static ShapePlaysetGenerationResult Success(
            int seed, int attemptIndex, ShapePlaysetGenerationConfig config,
            int[,] board, int[,] obstacleMask,
            IReadOnlyList<GeneratedShapeBlock> blocks, bool usedPredefined,
            ShapePlaysetDifficultyMetrics metrics) =>
            new ShapePlaysetGenerationResult(
                ShapePlaysetGenerationError.None,
                new ShapePlaysetGenerationConfigValidation(
                    Array.Empty<ShapePlaysetConfigurationError>(),
                    ShapePlaysetFeasibilityStatus.ProvenFeasible),
                new GeneratedShapePlayset(
                    seed, attemptIndex, config, board, obstacleMask, blocks, usedPredefined, metrics));

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
                var value = schema[row, column];
                if (value != 0 && value != 1) return null;
                if (value == 0) continue;
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
            IsWithinDimensions(footprint.GetLength(0), footprint.GetLength(1),
                minRows, maxRows, minColumns, maxColumns);

        private static bool IsWithinDimensions(
            int rows, int columns, int minRows, int maxRows, int minColumns, int maxColumns) =>
            rows >= minRows && rows <= maxRows && columns >= minColumns && columns <= maxColumns;

        private static bool AreFourConnected(int[,] mask)
        {
            var total = CountCells(mask);
            if (total == 0) return false;
            var visited = new bool[mask.GetLength(0), mask.GetLength(1)];
            var queue = new Queue<Vector2Int>();
            var startFound = false;
            for (var row = 0; row < mask.GetLength(0) && !startFound; row++)
            for (var column = 0; column < mask.GetLength(1); column++)
            {
                if (mask[row, column] == 0) continue;
                queue.Enqueue(new Vector2Int(row, column));
                visited[row, column] = true;
                startFound = true;
                break;
            }

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

        private static bool TryRepairObstacleMask(
            int[,] obstacleMask, int minimumRegionCells, int nodeBudget, out bool budgetExceeded)
        {
            budgetExceeded = false;
            var nodesVisited = 0;
            var clearedCells = new List<Vector2Int>();
            var protectedCells = new bool[obstacleMask.GetLength(0), obstacleMask.GetLength(1)];

            while (true)
            {
                var components = FindPlayableComponents(obstacleMask);
                if (components.Count <= 1) break;

                var targetIndex = 0;
                for (var index = 1; index < components.Count; index++)
                    if (components[index].Count > components[targetIndex].Count)
                        targetIndex = index;

                var sourceIndex = -1;
                for (var index = 0; index < components.Count; index++)
                {
                    if (index == targetIndex || components[index].Count >= minimumRegionCells) continue;
                    sourceIndex = index;
                    break;
                }
                if (sourceIndex < 0) break;

                if (!TryFindConnectionPath(obstacleMask, components[sourceIndex], components[targetIndex],
                        ref nodesVisited, nodeBudget, out var path, out budgetExceeded))
                    return false;

                var clearedOnPath = 0;
                for (var index = 0; index < path.Count; index++)
                {
                    var cell = path[index];
                    protectedCells[cell.x, cell.y] = true;
                    if (obstacleMask[cell.x, cell.y] == 0) continue;
                    obstacleMask[cell.x, cell.y] = 0;
                    clearedCells.Add(cell);
                    clearedOnPath++;
                }
                if (clearedOnPath == 0) return false;
            }

            if (!ArePlayableRegionsAcceptable(obstacleMask, minimumRegionCells)) return false;
            if (clearedCells.Count > 0 &&
                !TryRelocateClearedObstacles(obstacleMask, protectedCells, clearedCells.Count,
                    0, minimumRegionCells, ref nodesVisited, nodeBudget, out budgetExceeded))
                return false;

            return ArePlayableRegionsAcceptable(obstacleMask, minimumRegionCells);
        }

        private static List<List<Vector2Int>> FindPlayableComponents(int[,] obstacleMask)
        {
            var components = new List<List<Vector2Int>>();
            var visited = new bool[obstacleMask.GetLength(0), obstacleMask.GetLength(1)];
            var queue = new Queue<Vector2Int>();
            for (var row = 0; row < obstacleMask.GetLength(0); row++)
            for (var column = 0; column < obstacleMask.GetLength(1); column++)
            {
                if (obstacleMask[row, column] != 0 || visited[row, column]) continue;
                var component = new List<Vector2Int>();
                visited[row, column] = true;
                queue.Enqueue(new Vector2Int(row, column));
                while (queue.Count > 0)
                {
                    var cell = queue.Dequeue();
                    component.Add(cell);
                    EnqueuePlayableCell(cell.x - 1, cell.y, obstacleMask, visited, queue);
                    EnqueuePlayableCell(cell.x + 1, cell.y, obstacleMask, visited, queue);
                    EnqueuePlayableCell(cell.x, cell.y - 1, obstacleMask, visited, queue);
                    EnqueuePlayableCell(cell.x, cell.y + 1, obstacleMask, visited, queue);
                }
                components.Add(component);
            }
            return components;
        }

        private static void EnqueuePlayableCell(
            int row, int column, int[,] obstacleMask, bool[,] visited, Queue<Vector2Int> queue)
        {
            if (row < 0 || row >= obstacleMask.GetLength(0) ||
                column < 0 || column >= obstacleMask.GetLength(1) ||
                obstacleMask[row, column] != 0 || visited[row, column]) return;
            visited[row, column] = true;
            queue.Enqueue(new Vector2Int(row, column));
        }

        private static bool ArePlayableRegionsAcceptable(int[,] obstacleMask, int minimumRegionCells)
        {
            var components = FindPlayableComponents(obstacleMask);
            if (components.Count == 0) return false;
            if (components.Count == 1) return true;
            for (var index = 0; index < components.Count; index++)
                if (components[index].Count < minimumRegionCells) return false;
            return true;
        }

        private static bool TryFindConnectionPath(
            int[,] obstacleMask, IReadOnlyList<Vector2Int> source, IReadOnlyList<Vector2Int> target,
            ref int nodesVisited, int nodeBudget, out List<Vector2Int> path, out bool budgetExceeded)
        {
            path = new List<Vector2Int>();
            budgetExceeded = false;
            var rows = obstacleMask.GetLength(0);
            var columns = obstacleMask.GetLength(1);
            var distances = new int[rows, columns];
            var previous = new Vector2Int[rows, columns];
            var visited = new bool[rows, columns];
            var targets = new bool[rows, columns];
            for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
            {
                distances[row, column] = int.MaxValue;
                previous[row, column] = new Vector2Int(-1, -1);
            }
            for (var index = 0; index < source.Count; index++)
                distances[source[index].x, source[index].y] = 0;
            for (var index = 0; index < target.Count; index++)
                targets[target[index].x, target[index].y] = true;

            while (true)
            {
                var current = new Vector2Int(-1, -1);
                var bestDistance = int.MaxValue;
                for (var row = 0; row < rows; row++)
                for (var column = 0; column < columns; column++)
                    if (!visited[row, column] && distances[row, column] < bestDistance)
                    {
                        bestDistance = distances[row, column];
                        current = new Vector2Int(row, column);
                    }
                if (current.x < 0) return false;
                if (nodesVisited >= nodeBudget)
                {
                    budgetExceeded = true;
                    return false;
                }
                nodesVisited++;
                visited[current.x, current.y] = true;
                if (targets[current.x, current.y])
                {
                    while (current.x >= 0)
                    {
                        path.Add(current);
                        current = previous[current.x, current.y];
                    }
                    path.Reverse();
                    return true;
                }

                RelaxPathCell(current.x - 1, current.y, current, obstacleMask, distances, previous, visited);
                RelaxPathCell(current.x, current.y - 1, current, obstacleMask, distances, previous, visited);
                RelaxPathCell(current.x, current.y + 1, current, obstacleMask, distances, previous, visited);
                RelaxPathCell(current.x + 1, current.y, current, obstacleMask, distances, previous, visited);
            }
        }

        private static void RelaxPathCell(
            int row, int column, Vector2Int from, int[,] obstacleMask,
            int[,] distances, Vector2Int[,] previous, bool[,] visited)
        {
            if (row < 0 || row >= obstacleMask.GetLength(0) ||
                column < 0 || column >= obstacleMask.GetLength(1) || visited[row, column]) return;
            var distance = distances[from.x, from.y] + (obstacleMask[row, column] == 0 ? 0 : 1);
            if (distance >= distances[row, column]) return;
            distances[row, column] = distance;
            previous[row, column] = from;
        }

        private static bool TryRelocateClearedObstacles(
            int[,] obstacleMask, bool[,] protectedCells, int remaining, int firstCandidate,
            int minimumRegionCells, ref int nodesVisited, int nodeBudget, out bool budgetExceeded)
        {
            budgetExceeded = false;
            if (remaining == 0) return ArePlayableRegionsAcceptable(obstacleMask, minimumRegionCells);
            var rows = obstacleMask.GetLength(0);
            var columns = obstacleMask.GetLength(1);
            var cellCount = rows * columns;
            for (var candidate = firstCandidate; candidate < cellCount; candidate++)
            {
                var row = candidate / columns;
                var column = candidate % columns;
                if (obstacleMask[row, column] != 0 || protectedCells[row, column]) continue;
                if (nodesVisited >= nodeBudget)
                {
                    budgetExceeded = true;
                    return false;
                }
                nodesVisited++;
                obstacleMask[row, column] = 1;
                if (ArePlayableRegionsAcceptable(obstacleMask, minimumRegionCells) &&
                    TryRelocateClearedObstacles(obstacleMask, protectedCells, remaining - 1,
                        candidate + 1, minimumRegionCells, ref nodesVisited, nodeBudget,
                        out budgetExceeded))
                    return true;
                obstacleMask[row, column] = 0;
                if (budgetExceeded) return false;
            }
            return false;
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
                if (footprint == null ||
                    (!IsWithinDimensions(footprint,
                         config.MinGeneratedBlockRows, config.MaxGeneratedBlockRows,
                         config.MinGeneratedBlockColumns, config.MaxGeneratedBlockColumns) &&
                     !IsWithinDimensions(footprint.GetLength(1), footprint.GetLength(0),
                         config.MinGeneratedBlockRows, config.MaxGeneratedBlockRows,
                         config.MinGeneratedBlockColumns, config.MaxGeneratedBlockColumns)) ||
                    !AreFourConnected(footprint)) return false;
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

        private sealed class TilingSearchContext
        {
            public readonly int[,] PlayableMask;
            public readonly bool[,] Covered;
            public readonly ShapePlaysetGenerationConfig Config;
            public readonly List<PreferredBlockCandidate> PreferredBlocks;
            public readonly bool[] UsedPreferred;
            public readonly List<GeneratedShapeBlock> CurrentBlocks = new List<GeneratedShapeBlock>();
            public readonly int TotalPlayableCells;
            public readonly int TotalPreferredCells;
            public int NodesVisited;
            public bool BudgetExceeded;
            public bool StopSearch;
            public List<GeneratedShapeBlock> BestBlocks;
            public int BestPreferredCellCount = -1;
            public int BestPreferredBlockCount = -1;
            public int BestSingletonCount = int.MaxValue;

            public TilingSearchContext(
                int[,] playableMask, ShapePlaysetGenerationConfig config,
                List<PreferredBlockCandidate> preferredBlocks)
            {
                PlayableMask = playableMask;
                Covered = new bool[playableMask.GetLength(0), playableMask.GetLength(1)];
                Config = config;
                PreferredBlocks = preferredBlocks;
                UsedPreferred = new bool[preferredBlocks.Count];
                TotalPlayableCells = CountCells(playableMask);
                var preferredCellCount = 0;
                for (var index = 0; index < preferredBlocks.Count; index++)
                    preferredCellCount += preferredBlocks[index].CellCount;
                TotalPreferredCells = preferredCellCount;
            }

            public void ConsiderCurrentSolution(
                int preferredCellCount, int preferredBlockCount, int singletonCount)
            {
                var isBetter = BestBlocks == null ||
                    preferredCellCount > BestPreferredCellCount ||
                    (preferredCellCount == BestPreferredCellCount &&
                     preferredBlockCount > BestPreferredBlockCount) ||
                    (preferredCellCount == BestPreferredCellCount &&
                     preferredBlockCount == BestPreferredBlockCount &&
                     singletonCount < BestSingletonCount);
                if (!isBetter) return;

                BestBlocks = new List<GeneratedShapeBlock>(CurrentBlocks);
                BestPreferredCellCount = preferredCellCount;
                BestPreferredBlockCount = preferredBlockCount;
                BestSingletonCount = singletonCount;
                if (TotalPreferredCells <= TotalPlayableCells &&
                    preferredCellCount == TotalPreferredCells &&
                    preferredBlockCount == PreferredBlocks.Count && singletonCount == 0)
                    StopSearch = true;
            }
        }

        private sealed class PreferredBlockCandidate
        {
            public readonly int[,] Footprint;
            public readonly BlockPreset Preset;
            public readonly int CellCount;

            public PreferredBlockCandidate(int[,] footprint, BlockPreset preset, int cellCount)
            {
                Footprint = footprint;
                Preset = preset;
                CellCount = cellCount;
            }
        }

        private sealed class TilingPlacementCandidate
        {
            public readonly int[] CellIndices;
            public readonly int[,] Footprint;
            public readonly BlockPreset PreferredPreset;
            public readonly Vector2Int[] Placement;
            public readonly int PreferredBlockIndex;
            public readonly int PlacementRotationQuarterTurns;
            public readonly string Key;

            public TilingPlacementCandidate(
                int[] cellIndices, int[,] footprint, BlockPreset preferredPreset,
                Vector2Int[] placement, int preferredBlockIndex,
                int placementRotationQuarterTurns, string key)
            {
                CellIndices = cellIndices;
                Footprint = footprint;
                PreferredPreset = preferredPreset;
                Placement = placement;
                PreferredBlockIndex = preferredBlockIndex;
                PlacementRotationQuarterTurns = placementRotationQuarterTurns;
                Key = key;
            }
        }

        private readonly struct CellBounds
        {
            public readonly int minRow;
            public readonly int minColumn;
            public readonly int maxRow;
            public readonly int maxColumn;

            public CellBounds(int minRow, int minColumn, int maxRow, int maxColumn)
            {
                this.minRow = minRow;
                this.minColumn = minColumn;
                this.maxRow = maxRow;
                this.maxColumn = maxColumn;
            }
        }

        private sealed class FallbackShapeCandidate
        {
            public readonly int[] CellIndices;
            public readonly int[,] Footprint;
            public readonly Vector2Int[] Placement;
            public readonly int PlacementRotationQuarterTurns;
            public readonly string Key;

            public FallbackShapeCandidate(
                int[] cellIndices, int[,] footprint, Vector2Int[] placement,
                int placementRotationQuarterTurns, string key)
            {
                CellIndices = cellIndices;
                Footprint = footprint;
                Placement = placement;
                PlacementRotationQuarterTurns = placementRotationQuarterTurns;
                Key = key;
            }
        }

        private sealed class CandidateFailureAccumulator
        {
            private int _obstacleRepairFailures;
            private int _obstacleRepairBudgetExhaustions;
            private int _tilingFailures;
            private int _tilingBudgetExhaustions;
            private int _generationInvariantFailures;

            public void Record(ShapePlaysetGenerationError error)
            {
                switch (error)
                {
                    case ShapePlaysetGenerationError.ObstacleRepairFailed:
                        _obstacleRepairFailures++;
                        break;
                    case ShapePlaysetGenerationError.ObstacleRepairBudgetExceeded:
                        _obstacleRepairBudgetExhaustions++;
                        break;
                    case ShapePlaysetGenerationError.TilingFailed:
                        _tilingFailures++;
                        break;
                    case ShapePlaysetGenerationError.TilingBudgetExceeded:
                        _tilingBudgetExhaustions++;
                        break;
                    case ShapePlaysetGenerationError.GenerationInvariantFailed:
                        _generationInvariantFailures++;
                        break;
                }
            }

            public ShapePlaysetCandidateFailureCounts ToSnapshot() =>
                new ShapePlaysetCandidateFailureCounts(
                    _obstacleRepairFailures, _obstacleRepairBudgetExhaustions,
                    _tilingFailures, _tilingBudgetExhaustions,
                    _generationInvariantFailures);
        }

        private sealed class SeededRandom
        {
            private uint _state;

            public SeededRandom(int seed) => _state = unchecked((uint)seed);

            public int NextInclusive(int minimum, int maximum)
            {
                if (minimum == maximum) return minimum;
                var range = (ulong)((long)maximum - minimum + 1);
                var fullRange = 1UL << 32;
                var acceptedRange = fullRange - fullRange % range;
                uint value;
                do value = NextUInt();
                while ((ulong)value >= acceptedRange);
                return (int)((long)minimum + (long)((ulong)value % range));
            }

            private uint NextUInt()
            {
                unchecked
                {
                    _state += 0x9E3779B9u;
                    var value = _state;
                    value = (value ^ (value >> 16)) * 0x85EBCA6Bu;
                    value = (value ^ (value >> 13)) * 0xC2B2AE35u;
                    return value ^ (value >> 16);
                }
            }
        }
    }
}
