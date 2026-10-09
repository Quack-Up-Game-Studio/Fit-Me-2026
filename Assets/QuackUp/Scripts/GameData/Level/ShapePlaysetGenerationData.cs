using System;
using System.Collections.Generic;
using FitMe.Grid;
using UnityEngine;

namespace FitMe.GameData
{
    public enum ShapePlaysetConfigurationError
    {
        None,
        NullConfiguration,
        InvalidBoardDimensions,
        BoardTooLarge,
        InvalidObstacleRange,
        InvalidGeneratedBlockDimensions,
        NoFeasibleConnectedTiling
    }

    public enum ShapePlaysetGenerationError
    {
        None,
        InvalidConfiguration,
        GenerationInvariantFailed
    }

    public sealed class ShapePlaysetGenerationConfig
    {
        public int MinBoardColumns { get; }
        public int MaxBoardColumns { get; }
        public int MinBoardRows { get; }
        public int MaxBoardRows { get; }
        public int MinObstacleCells { get; }
        public int MaxObstacleCells { get; }
        public int MinGeneratedBlockColumns { get; }
        public int MaxGeneratedBlockColumns { get; }
        public int MinGeneratedBlockRows { get; }
        public int MaxGeneratedBlockRows { get; }
        public IReadOnlyList<ShapePlayerBlockEntry> PreferredPlayerBlocks { get; }

        public ShapePlaysetGenerationConfig(
            int minBoardColumns, int maxBoardColumns,
            int minBoardRows, int maxBoardRows,
            int minObstacleCells, int maxObstacleCells,
            int minGeneratedBlockColumns, int maxGeneratedBlockColumns,
            int minGeneratedBlockRows, int maxGeneratedBlockRows,
            IReadOnlyList<ShapePlayerBlockEntry> preferredPlayerBlocks = null)
        {
            MinBoardColumns = minBoardColumns;
            MaxBoardColumns = maxBoardColumns;
            MinBoardRows = minBoardRows;
            MaxBoardRows = maxBoardRows;
            MinObstacleCells = minObstacleCells;
            MaxObstacleCells = maxObstacleCells;
            MinGeneratedBlockColumns = minGeneratedBlockColumns;
            MaxGeneratedBlockColumns = maxGeneratedBlockColumns;
            MinGeneratedBlockRows = minGeneratedBlockRows;
            MaxGeneratedBlockRows = maxGeneratedBlockRows;
            PreferredPlayerBlocks = preferredPlayerBlocks == null
                ? Array.Empty<ShapePlayerBlockEntry>()
                : Array.AsReadOnly(new List<ShapePlayerBlockEntry>(preferredPlayerBlocks).ToArray());
        }
    }

    public sealed class ShapePlaysetGenerationConfigValidation
    {
        private readonly IReadOnlyList<ShapePlaysetConfigurationError> _errors;

        public IReadOnlyList<ShapePlaysetConfigurationError> Errors => _errors;
        public bool IsValid => _errors.Count == 0;

        internal ShapePlaysetGenerationConfigValidation(ShapePlaysetConfigurationError[] errors)
        {
            _errors = Array.AsReadOnly(errors ?? Array.Empty<ShapePlaysetConfigurationError>());
        }
    }

    public sealed class GeneratedShapeBlock
    {
        public int[,] Footprint { get; }
        public BlockPreset PreferredPreset { get; }
        public Vector2Int[] SolvedPlacement { get; }
        public bool IsPredefined => PreferredPreset;

        internal GeneratedShapeBlock(int[,] footprint, BlockPreset preferredPreset, Vector2Int[] solvedPlacement)
        {
            Footprint = footprint;
            PreferredPreset = preferredPreset;
            SolvedPlacement = solvedPlacement;
        }
    }

    public sealed class ShapePlaysetDifficultyMetrics
    {
        public int PlayableCellCount { get; }
        public int ObstacleCellCount { get; }
        public int PlayerCellCount { get; }
        public int BlockCount { get; }
        public int TotalLegalPlacements { get; }
        public float AverageLegalPlacementsPerBlock { get; }
        public int MinimumLegalPlacementsPerBlock { get; }
        public int MaximumLegalPlacementsPerBlock { get; }
        public float ObstacleRatio { get; }
        public int ProvisionalDifficultyScore { get; }
        public int ScoreModelVersion { get; }
        public bool IsProvisional => true;

        internal ShapePlaysetDifficultyMetrics(
            int playableCellCount, int obstacleCellCount, int playerCellCount, int blockCount,
            int totalLegalPlacements, float averageLegalPlacementsPerBlock,
            int minimumLegalPlacementsPerBlock, int maximumLegalPlacementsPerBlock,
            float obstacleRatio, int provisionalDifficultyScore, int scoreModelVersion)
        {
            PlayableCellCount = playableCellCount;
            ObstacleCellCount = obstacleCellCount;
            PlayerCellCount = playerCellCount;
            BlockCount = blockCount;
            TotalLegalPlacements = totalLegalPlacements;
            AverageLegalPlacementsPerBlock = averageLegalPlacementsPerBlock;
            MinimumLegalPlacementsPerBlock = minimumLegalPlacementsPerBlock;
            MaximumLegalPlacementsPerBlock = maximumLegalPlacementsPerBlock;
            ObstacleRatio = obstacleRatio;
            ProvisionalDifficultyScore = provisionalDifficultyScore;
            ScoreModelVersion = scoreModelVersion;
        }
    }

    public sealed class GeneratedShapePlayset
    {
        public const int CurrentGeneratorVersion = 1;

        public int Seed { get; }
        public int GeneratorVersion => CurrentGeneratorVersion;
        public int[,] BoardMask { get; }
        public int[,] ObstacleMask { get; }
        public IReadOnlyList<GeneratedShapeBlock> PlayerBlocks { get; }
        public bool UsedPredefinedBlocks { get; }
        public bool IsSolvable => true;
        public ShapePlaysetDifficultyMetrics Difficulty { get; }

        internal GeneratedShapePlayset(
            int seed, int[,] boardMask, int[,] obstacleMask,
            IReadOnlyList<GeneratedShapeBlock> playerBlocks, bool usedPredefinedBlocks,
            ShapePlaysetDifficultyMetrics difficulty)
        {
            Seed = seed;
            BoardMask = boardMask;
            ObstacleMask = obstacleMask;
            PlayerBlocks = Array.AsReadOnly(new List<GeneratedShapeBlock>(playerBlocks).ToArray());
            UsedPredefinedBlocks = usedPredefinedBlocks;
            Difficulty = difficulty;
        }
    }

    public sealed class ShapePlaysetGenerationResult
    {
        public bool IsSuccess => Playset != null && Error == ShapePlaysetGenerationError.None;
        public ShapePlaysetGenerationError Error { get; }
        public ShapePlaysetGenerationConfigValidation ConfigurationValidation { get; }
        public GeneratedShapePlayset Playset { get; }

        internal ShapePlaysetGenerationResult(
            ShapePlaysetGenerationError error,
            ShapePlaysetGenerationConfigValidation configurationValidation,
            GeneratedShapePlayset playset)
        {
            Error = error;
            ConfigurationValidation = configurationValidation;
            Playset = playset;
        }
    }
}
