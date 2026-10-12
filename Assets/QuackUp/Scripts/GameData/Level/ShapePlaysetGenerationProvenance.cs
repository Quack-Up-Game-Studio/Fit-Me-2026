using System;
using System.Collections.Generic;
using FitMe.Grid;
using UnityEngine;

namespace FitMe.GameData
{
    [Serializable]
    public sealed class ShapePlaysetPreferredBlockProvenance
    {
        [SerializeField] private string _presetId;
        [SerializeField] private int _count;
        [SerializeField] private Vector2Int _schemaSize;
        [SerializeField] private List<int> _schemaCells = new();

        public string PresetId => _presetId ?? string.Empty;
        public int Count => _count;
        public int[,] PresetSchema => ExpandSchema(_schemaSize, _schemaCells);

        internal ShapePlaysetPreferredBlockProvenance(ShapePlayerBlockConfigSnapshot snapshot)
        {
            _presetId = snapshot.PresetId;
            _count = snapshot.Count;
            var schema = snapshot.PresetSchema;
            _schemaSize = new Vector2Int(schema.GetLength(1), schema.GetLength(0));
            _schemaCells = FlattenSchema(schema);
        }

        private static List<int> FlattenSchema(int[,] schema)
        {
            var cells = new List<int>(schema.Length);
            for (var row = 0; row < schema.GetLength(0); row++)
            for (var column = 0; column < schema.GetLength(1); column++)
                cells.Add(schema[row, column]);
            return cells;
        }

        private static int[,] ExpandSchema(Vector2Int size, IReadOnlyList<int> cells)
        {
            if (size.x <= 0 || size.y <= 0) return new int[0, 0];
            cells ??= Array.Empty<int>();
            var schema = new int[size.y, size.x];
            for (var index = 0; index < schema.Length && index < cells.Count; index++)
                schema[index / size.x, index % size.x] = cells[index];
            return schema;
        }
    }

    [Serializable]
    public sealed class ShapePlaysetConfigurationProvenance
    {
        [SerializeField] private int _minBoardColumns;
        [SerializeField] private int _maxBoardColumns;
        [SerializeField] private int _minBoardRows;
        [SerializeField] private int _maxBoardRows;
        [SerializeField] private int _minObstacleCells;
        [SerializeField] private int _maxObstacleCells;
        [SerializeField] private int _minGeneratedBlockColumns;
        [SerializeField] private int _maxGeneratedBlockColumns;
        [SerializeField] private int _minGeneratedBlockRows;
        [SerializeField] private int _maxGeneratedBlockRows;
        [SerializeField] private int _minPlayableRegionCells;
        [SerializeField] private int _maxObstacleRepairNodes;
        [SerializeField] private int _maxTilingSearchNodes;
        [SerializeField] private int _maxCandidateAttempts;
        [SerializeField] private int _maxSingletonFallbackBlocks;
        [SerializeField] private List<ShapePlaysetPreferredBlockProvenance> _preferredPlayerBlocks = new();

        public int MinBoardColumns => _minBoardColumns;
        public int MaxBoardColumns => _maxBoardColumns;
        public int MinBoardRows => _minBoardRows;
        public int MaxBoardRows => _maxBoardRows;
        public int MinObstacleCells => _minObstacleCells;
        public int MaxObstacleCells => _maxObstacleCells;
        public int MinGeneratedBlockColumns => _minGeneratedBlockColumns;
        public int MaxGeneratedBlockColumns => _maxGeneratedBlockColumns;
        public int MinGeneratedBlockRows => _minGeneratedBlockRows;
        public int MaxGeneratedBlockRows => _maxGeneratedBlockRows;
        public int MinPlayableRegionCells => _minPlayableRegionCells;
        public int MaxObstacleRepairNodes => _maxObstacleRepairNodes;
        public int MaxTilingSearchNodes => _maxTilingSearchNodes;
        public int MaxCandidateAttempts => _maxCandidateAttempts;
        public int MaxSingletonFallbackBlocks => _maxSingletonFallbackBlocks;
        public IReadOnlyList<ShapePlaysetPreferredBlockProvenance> PreferredPlayerBlocks =>
            _preferredPlayerBlocks == null
                ? Array.Empty<ShapePlaysetPreferredBlockProvenance>()
                : _preferredPlayerBlocks.AsReadOnly();

        internal ShapePlaysetConfigurationProvenance(ShapePlaysetGenerationConfigSnapshot snapshot)
        {
            _minBoardColumns = snapshot.MinBoardColumns;
            _maxBoardColumns = snapshot.MaxBoardColumns;
            _minBoardRows = snapshot.MinBoardRows;
            _maxBoardRows = snapshot.MaxBoardRows;
            _minObstacleCells = snapshot.MinObstacleCells;
            _maxObstacleCells = snapshot.MaxObstacleCells;
            _minGeneratedBlockColumns = snapshot.MinGeneratedBlockColumns;
            _maxGeneratedBlockColumns = snapshot.MaxGeneratedBlockColumns;
            _minGeneratedBlockRows = snapshot.MinGeneratedBlockRows;
            _maxGeneratedBlockRows = snapshot.MaxGeneratedBlockRows;
            _minPlayableRegionCells = snapshot.MinPlayableRegionCells;
            _maxObstacleRepairNodes = snapshot.MaxObstacleRepairNodes;
            _maxTilingSearchNodes = snapshot.MaxTilingSearchNodes;
            _maxCandidateAttempts = snapshot.MaxCandidateAttempts;
            _maxSingletonFallbackBlocks = snapshot.MaxSingletonFallbackBlocks;
            _preferredPlayerBlocks = new List<ShapePlaysetPreferredBlockProvenance>(
                snapshot.PreferredPlayerBlocks.Count);
            for (var index = 0; index < snapshot.PreferredPlayerBlocks.Count; index++)
                _preferredPlayerBlocks.Add(
                    new ShapePlaysetPreferredBlockProvenance(snapshot.PreferredPlayerBlocks[index]));
        }
    }

    [Serializable]
    public sealed class ShapePlaysetDifficultyProvenance
    {
        [SerializeField] private int _playableCellCount;
        [SerializeField] private int _obstacleCellCount;
        [SerializeField] private int _playerCellCount;
        [SerializeField] private int _blockCount;
        [SerializeField] private int _totalLegalPlacements;
        [SerializeField] private float _averageLegalPlacementsPerBlock;
        [SerializeField] private int _minimumLegalPlacementsPerBlock;
        [SerializeField] private int _maximumLegalPlacementsPerBlock;
        [SerializeField] private float _obstacleRatio;
        [SerializeField] private int _provisionalDifficultyScore;
        [SerializeField] private int _scoreModelVersion;

        public int PlayableCellCount => _playableCellCount;
        public int ObstacleCellCount => _obstacleCellCount;
        public int PlayerCellCount => _playerCellCount;
        public int BlockCount => _blockCount;
        public int TotalLegalPlacements => _totalLegalPlacements;
        public float AverageLegalPlacementsPerBlock => _averageLegalPlacementsPerBlock;
        public int MinimumLegalPlacementsPerBlock => _minimumLegalPlacementsPerBlock;
        public int MaximumLegalPlacementsPerBlock => _maximumLegalPlacementsPerBlock;
        public float ObstacleRatio => _obstacleRatio;
        public int ProvisionalDifficultyScore => _provisionalDifficultyScore;
        public int ScoreModelVersion => _scoreModelVersion;
        public bool IsProvisional => true;

        internal ShapePlaysetDifficultyProvenance(ShapePlaysetDifficultyMetrics metrics)
        {
            _playableCellCount = metrics.PlayableCellCount;
            _obstacleCellCount = metrics.ObstacleCellCount;
            _playerCellCount = metrics.PlayerCellCount;
            _blockCount = metrics.BlockCount;
            _totalLegalPlacements = metrics.TotalLegalPlacements;
            _averageLegalPlacementsPerBlock = metrics.AverageLegalPlacementsPerBlock;
            _minimumLegalPlacementsPerBlock = metrics.MinimumLegalPlacementsPerBlock;
            _maximumLegalPlacementsPerBlock = metrics.MaximumLegalPlacementsPerBlock;
            _obstacleRatio = metrics.ObstacleRatio;
            _provisionalDifficultyScore = metrics.ProvisionalDifficultyScore;
            _scoreModelVersion = metrics.ScoreModelVersion;
        }
    }

    [Serializable]
    public sealed class ShapePlaysetGenerationProvenance
    {
        [SerializeField] private int _seed;
        [SerializeField] private int _attemptIndex;
        [SerializeField] private int _generatorVersion;
        [SerializeField] private ShapePlaysetConfigurationProvenance _configuration;
        [SerializeField] private ShapePlaysetDifficultyProvenance _difficulty;

        public int Seed => _seed;
        public int AttemptIndex => _attemptIndex;
        public int GeneratorVersion => _generatorVersion;
        public ShapePlaysetConfigurationProvenance Configuration => _configuration;
        public ShapePlaysetDifficultyProvenance Difficulty => _difficulty;
        /// <summary>Records generation origin only; it does not assert that current level data is unchanged.</summary>
        public bool IsOriginOnly => true;

        internal ShapePlaysetGenerationProvenance(GeneratedShapePlayset playset)
        {
            _seed = playset.Seed;
            _attemptIndex = playset.AttemptIndex;
            _generatorVersion = playset.GeneratorVersion;
            _configuration = new ShapePlaysetConfigurationProvenance(playset.Configuration);
            _difficulty = new ShapePlaysetDifficultyProvenance(playset.Difficulty);
        }
    }
}
