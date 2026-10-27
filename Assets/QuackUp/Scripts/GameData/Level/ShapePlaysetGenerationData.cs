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
        InvalidVoidRange,
        InvalidCombinedCellRange,
        InvalidGeneratedBlockDimensions,
        InvalidPreferredBlockSupply,
        InvalidPlayableRegionThreshold,
        InvalidSearchBudget,
        InvalidSingletonFallbackLimit,
        InvalidShapeStylePreference
    }

    public enum ShapePlaysetGenerationError
    {
        None,
        InvalidConfiguration,
        CandidateRetriesExhausted,
        ObstacleRepairFailed,
        ObstacleRepairBudgetExceeded,
        TilingFailed,
        TilingBudgetExceeded,
        GenerationInvariantFailed
    }

    public enum ShapePlaysetFeasibilityStatus
    {
        ProvenFeasible,
        ProvenInfeasible,
        Unproven
    }

    public enum ShapePlaysetOptimizationStatus
    {
        NotRecorded,
        ProvenOptimal,
        BudgetLimited
    }

    public sealed class ShapePlaysetGenerationConfig
    {
        public int MinBoardColumns { get; }
        public int MaxBoardColumns { get; }
        public int MinBoardRows { get; }
        public int MaxBoardRows { get; }
        public int MinObstacleCells { get; }
        public int MaxObstacleCells { get; }
        public int MinVoidCells { get; }
        public int MaxVoidCells { get; }
        public int MinGeneratedBlockColumns { get; }
        public int MaxGeneratedBlockColumns { get; }
        public int MinGeneratedBlockRows { get; }
        public int MaxGeneratedBlockRows { get; }
        public int MinPlayableRegionCells { get; }
        public int MaxObstacleRepairNodes { get; }
        public int MaxTilingSearchNodes { get; }
        public int MaxCandidateAttempts { get; }
        public int MaxSingletonFallbackBlocks { get; }
        public float RectangularPreference { get; }
        public float RepetitionPenalty { get; }
        public IReadOnlyList<ShapePlayerBlockEntry> PreferredPlayerBlocks { get; }

        public ShapePlaysetGenerationConfig(
            int minBoardColumns, int maxBoardColumns,
            int minBoardRows, int maxBoardRows,
            int minObstacleCells, int maxObstacleCells,
            int minGeneratedBlockColumns, int maxGeneratedBlockColumns,
            int minGeneratedBlockRows, int maxGeneratedBlockRows,
            IReadOnlyList<ShapePlayerBlockEntry> preferredPlayerBlocks = null,
            int minPlayableRegionCells = 10,
            int maxObstacleRepairNodes = 50000,
            int maxTilingSearchNodes = 100000,
            int maxCandidateAttempts = 8,
            int maxSingletonFallbackBlocks = 0,
            int minVoidCells = 0,
            int maxVoidCells = 0,
            float rectangularPreference = 0.35f,
            float repetitionPenalty = 0.5f)
        {
            MinBoardColumns = minBoardColumns;
            MaxBoardColumns = maxBoardColumns;
            MinBoardRows = minBoardRows;
            MaxBoardRows = maxBoardRows;
            MinObstacleCells = minObstacleCells;
            MaxObstacleCells = maxObstacleCells;
            MinVoidCells = minVoidCells;
            MaxVoidCells = maxVoidCells;
            MinGeneratedBlockColumns = minGeneratedBlockColumns;
            MaxGeneratedBlockColumns = maxGeneratedBlockColumns;
            MinGeneratedBlockRows = minGeneratedBlockRows;
            MaxGeneratedBlockRows = maxGeneratedBlockRows;
            MinPlayableRegionCells = minPlayableRegionCells;
            MaxObstacleRepairNodes = maxObstacleRepairNodes;
            MaxTilingSearchNodes = maxTilingSearchNodes;
            MaxCandidateAttempts = maxCandidateAttempts;
            MaxSingletonFallbackBlocks = maxSingletonFallbackBlocks;
            RectangularPreference = rectangularPreference;
            RepetitionPenalty = repetitionPenalty;
            PreferredPlayerBlocks = preferredPlayerBlocks == null
                ? Array.Empty<ShapePlayerBlockEntry>()
                : Array.AsReadOnly(new List<ShapePlayerBlockEntry>(preferredPlayerBlocks).ToArray());
        }
    }

    public sealed class ShapePlayerBlockConfigSnapshot
    {
        private readonly int[,] _presetSchema;

        public string PresetId { get; }
        public int Count { get; }
        public int[,] PresetSchema => (int[,])_presetSchema.Clone();

        internal ShapePlayerBlockConfigSnapshot(string presetId, int count, int[,] presetSchema)
        {
            PresetId = presetId ?? string.Empty;
            Count = count;
            _presetSchema = presetSchema == null ? new int[0, 0] : (int[,])presetSchema.Clone();
        }
    }

    public sealed class ShapePlaysetGenerationConfigSnapshot
    {
        public int MinBoardColumns { get; }
        public int MaxBoardColumns { get; }
        public int MinBoardRows { get; }
        public int MaxBoardRows { get; }
        public int MinObstacleCells { get; }
        public int MaxObstacleCells { get; }
        public int MinVoidCells { get; }
        public int MaxVoidCells { get; }
        public int MinGeneratedBlockColumns { get; }
        public int MaxGeneratedBlockColumns { get; }
        public int MinGeneratedBlockRows { get; }
        public int MaxGeneratedBlockRows { get; }
        public int MinPlayableRegionCells { get; }
        public int MaxObstacleRepairNodes { get; }
        public int MaxTilingSearchNodes { get; }
        public int MaxCandidateAttempts { get; }
        public int MaxSingletonFallbackBlocks { get; }
        public float RectangularPreference { get; }
        public float RepetitionPenalty { get; }
        public IReadOnlyList<ShapePlayerBlockConfigSnapshot> PreferredPlayerBlocks { get; }

        internal ShapePlaysetGenerationConfigSnapshot(ShapePlaysetGenerationConfig config)
        {
            MinBoardColumns = config.MinBoardColumns;
            MaxBoardColumns = config.MaxBoardColumns;
            MinBoardRows = config.MinBoardRows;
            MaxBoardRows = config.MaxBoardRows;
            MinObstacleCells = config.MinObstacleCells;
            MaxObstacleCells = config.MaxObstacleCells;
            MinVoidCells = config.MinVoidCells;
            MaxVoidCells = config.MaxVoidCells;
            MinGeneratedBlockColumns = config.MinGeneratedBlockColumns;
            MaxGeneratedBlockColumns = config.MaxGeneratedBlockColumns;
            MinGeneratedBlockRows = config.MinGeneratedBlockRows;
            MaxGeneratedBlockRows = config.MaxGeneratedBlockRows;
            MinPlayableRegionCells = config.MinPlayableRegionCells;
            MaxObstacleRepairNodes = config.MaxObstacleRepairNodes;
            MaxTilingSearchNodes = config.MaxTilingSearchNodes;
            MaxCandidateAttempts = config.MaxCandidateAttempts;
            MaxSingletonFallbackBlocks = config.MaxSingletonFallbackBlocks;
            RectangularPreference = config.RectangularPreference;
            RepetitionPenalty = config.RepetitionPenalty;
            var preferredBlocks = new List<ShapePlayerBlockConfigSnapshot>(config.PreferredPlayerBlocks.Count);
            for (var index = 0; index < config.PreferredPlayerBlocks.Count; index++)
            {
                var entry = config.PreferredPlayerBlocks[index];
                preferredBlocks.Add(new ShapePlayerBlockConfigSnapshot(
                    entry.Preset.Id, entry.Count, entry.Preset.BlockSchema?.schema));
            }
            PreferredPlayerBlocks = Array.AsReadOnly(preferredBlocks.ToArray());
        }
    }

    public sealed class ShapePlaysetGenerationConfigValidation
    {
        private readonly IReadOnlyList<ShapePlaysetConfigurationError> _errors;

        public IReadOnlyList<ShapePlaysetConfigurationError> Errors => _errors;
        public bool IsValid => _errors.Count == 0;
        public ShapePlaysetFeasibilityStatus FeasibilityStatus { get; }

        internal ShapePlaysetGenerationConfigValidation(
            ShapePlaysetConfigurationError[] errors,
            ShapePlaysetFeasibilityStatus feasibilityStatus = ShapePlaysetFeasibilityStatus.Unproven)
        {
            _errors = Array.AsReadOnly(errors ?? Array.Empty<ShapePlaysetConfigurationError>());
            FeasibilityStatus = feasibilityStatus;
        }
    }

    public sealed class GeneratedShapeBlock
    {
        private readonly int[,] _footprint;
        private readonly Vector2Int[] _solvedPlacement;

        public int[,] Footprint => (int[,])_footprint.Clone();
        public BlockPreset PreferredPreset { get; }
        public Vector2Int[] SolvedPlacement => (Vector2Int[])_solvedPlacement.Clone();
        /// <summary>Clockwise quarter turns from the block footprint to its solved board orientation.</summary>
        public int PlacementRotationQuarterTurns { get; }
        public bool IsPredefined => PreferredPreset;

        internal GeneratedShapeBlock(
            int[,] footprint, BlockPreset preferredPreset,
            Vector2Int[] solvedPlacement, int placementRotationQuarterTurns)
        {
            _footprint = footprint == null ? new int[0, 0] : (int[,])footprint.Clone();
            PreferredPreset = preferredPreset;
            _solvedPlacement = solvedPlacement == null ? Array.Empty<Vector2Int>() : (Vector2Int[])solvedPlacement.Clone();
            PlacementRotationQuarterTurns = placementRotationQuarterTurns;
        }
    }

    public sealed class ShapePlaysetDifficultyMetrics
    {
        public int PlayableCellCount { get; }
        public int ObstacleCellCount { get; }
        public int VoidCellCount { get; }
        public int PlayerCellCount { get; }
        public int BlockCount { get; }
        public int TotalLegalPlacements { get; }
        public float AverageLegalPlacementsPerBlock { get; }
        public int MinimumLegalPlacementsPerBlock { get; }
        public int MaximumLegalPlacementsPerBlock { get; }
        public float ObstacleRatio { get; }
        public int SolutionCount { get; }
        public bool HasSolutionCount => SolutionCount > 0;
        public bool IsSolutionCountExact { get; }
        public int ProvisionalDifficultyScore { get; }
        public int ScoreModelVersion { get; }
        public bool IsProvisional => true;

        internal ShapePlaysetDifficultyMetrics(
            int playableCellCount, int obstacleCellCount, int voidCellCount, int playerCellCount, int blockCount,
            int totalLegalPlacements, float averageLegalPlacementsPerBlock,
            int minimumLegalPlacementsPerBlock, int maximumLegalPlacementsPerBlock,
            float obstacleRatio, int provisionalDifficultyScore, int scoreModelVersion,
            int solutionCount, bool isSolutionCountExact)
        {
            PlayableCellCount = playableCellCount;
            ObstacleCellCount = obstacleCellCount;
            VoidCellCount = voidCellCount;
            PlayerCellCount = playerCellCount;
            BlockCount = blockCount;
            TotalLegalPlacements = totalLegalPlacements;
            AverageLegalPlacementsPerBlock = averageLegalPlacementsPerBlock;
            MinimumLegalPlacementsPerBlock = minimumLegalPlacementsPerBlock;
            MaximumLegalPlacementsPerBlock = maximumLegalPlacementsPerBlock;
            ObstacleRatio = obstacleRatio;
            SolutionCount = solutionCount;
            IsSolutionCountExact = isSolutionCountExact;
            ProvisionalDifficultyScore = provisionalDifficultyScore;
            ScoreModelVersion = scoreModelVersion;
        }
    }

    public sealed class GeneratedShapePlayset
    {
        public const int CurrentGeneratorVersion = 6;

        public int Seed { get; }
        public int AttemptIndex { get; }
        public int GeneratorVersion => CurrentGeneratorVersion;
        public ShapePlaysetGenerationConfigSnapshot Configuration { get; }
        private readonly int[,] _boardMask;
        private readonly int[,] _obstacleMask;

        public int[,] BoardMask => (int[,])_boardMask.Clone();
        public int[,] ObstacleMask => (int[,])_obstacleMask.Clone();
        public IReadOnlyList<GeneratedShapeBlock> PlayerBlocks { get; }
        public bool UsedPredefinedBlocks { get; }
        public bool IsSolvable => true;
        public ShapePlaysetDifficultyMetrics Difficulty { get; }
        public ShapePlaysetOptimizationStatus OptimizationStatus { get; }
        public int OptimizationNodesVisited { get; }

        internal GeneratedShapePlayset(
            int seed, int attemptIndex, ShapePlaysetGenerationConfig config,
            int[,] boardMask, int[,] obstacleMask,
            IReadOnlyList<GeneratedShapeBlock> playerBlocks, bool usedPredefinedBlocks,
            ShapePlaysetDifficultyMetrics difficulty, ShapePlaysetOptimizationStatus optimizationStatus,
            int optimizationNodesVisited)
        {
            Seed = seed;
            AttemptIndex = attemptIndex;
            Configuration = new ShapePlaysetGenerationConfigSnapshot(config);
            _boardMask = boardMask == null ? new int[0, 0] : (int[,])boardMask.Clone();
            _obstacleMask = obstacleMask == null ? new int[0, 0] : (int[,])obstacleMask.Clone();
            PlayerBlocks = Array.AsReadOnly(new List<GeneratedShapeBlock>(playerBlocks).ToArray());
            UsedPredefinedBlocks = usedPredefinedBlocks;
            Difficulty = difficulty;
            OptimizationStatus = optimizationStatus;
            OptimizationNodesVisited = optimizationNodesVisited;
        }
    }

    public sealed class ShapePlaysetCandidateFailureCounts
    {
        public int ObstacleRepairFailures { get; }
        public int ObstacleRepairBudgetExhaustions { get; }
        public int TilingFailures { get; }
        public int TilingBudgetExhaustions { get; }
        public int GenerationInvariantFailures { get; }

        internal ShapePlaysetCandidateFailureCounts(
            int obstacleRepairFailures, int obstacleRepairBudgetExhaustions,
            int tilingFailures, int tilingBudgetExhaustions,
            int generationInvariantFailures)
        {
            ObstacleRepairFailures = obstacleRepairFailures;
            ObstacleRepairBudgetExhaustions = obstacleRepairBudgetExhaustions;
            TilingFailures = tilingFailures;
            TilingBudgetExhaustions = tilingBudgetExhaustions;
            GenerationInvariantFailures = generationInvariantFailures;
        }
    }

    public sealed class ShapePlaysetGenerationResult
    {
        public bool IsSuccess => Playset != null && Error == ShapePlaysetGenerationError.None;
        public ShapePlaysetGenerationError Error { get; }
        public ShapePlaysetGenerationError LastAttemptError { get; }
        public int AttemptsTried { get; }
        public ShapePlaysetGenerationConfigValidation ConfigurationValidation { get; }
        public ShapePlaysetCandidateFailureCounts CandidateFailures { get; }
        public GeneratedShapePlayset Playset { get; }

        internal ShapePlaysetGenerationResult(
            ShapePlaysetGenerationError error,
            ShapePlaysetGenerationConfigValidation configurationValidation,
            GeneratedShapePlayset playset,
            int attemptsTried = 0,
            ShapePlaysetGenerationError lastAttemptError = ShapePlaysetGenerationError.None,
            ShapePlaysetCandidateFailureCounts candidateFailures = null)
        {
            Error = error;
            AttemptsTried = attemptsTried;
            LastAttemptError = lastAttemptError;
            ConfigurationValidation = configurationValidation;
            CandidateFailures = candidateFailures ??
                new ShapePlaysetCandidateFailureCounts(0, 0, 0, 0, 0);
            Playset = playset;
        }
    }
}
