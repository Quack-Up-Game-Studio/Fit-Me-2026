using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace FitMe.GameData
{
    [CreateAssetMenu(
        fileName = "Shape Playset Generation Profile",
        menuName = "FitMe/GameData/Level/Shape Playset Generation Profile")]
    public sealed class ShapePlaysetGenerationProfile : ScriptableObject
    {
        [SerializeField, MinValue(1)] private int _minBoardColumns = 6;
        [SerializeField, MinValue(1)] private int _maxBoardColumns = 10;
        [SerializeField, MinValue(1)] private int _minBoardRows = 6;
        [SerializeField, MinValue(1)] private int _maxBoardRows = 10;
        [SerializeField, MinValue(0)] private int _minObstacleCells;
        [SerializeField, MinValue(0)] private int _maxObstacleCells = 8;
        [SerializeField, MinValue(1)] private int _minGeneratedBlockColumns = 1;
        [SerializeField, MinValue(1)] private int _maxGeneratedBlockColumns = 4;
        [SerializeField, MinValue(1)] private int _minGeneratedBlockRows = 1;
        [SerializeField, MinValue(1)] private int _maxGeneratedBlockRows = 4;
        [SerializeField, MinValue(1)] private int _minPlayableRegionCells = 10;
        [SerializeField, MinValue(1)] private int _maxObstacleRepairNodes = 50000;
        [SerializeField, MinValue(1)] private int _maxTilingSearchNodes = 100000;
        [SerializeField, MinValue(1)] private int _maxCandidateAttempts = 8;
        [SerializeField, MinValue(0)] private int _maxSingletonFallbackBlocks;
        [InfoBox("Preferred presets are reusable without limit. Count is ignored; fallback blocks use the dimension settings above.", InfoMessageType.Info)]
        [SerializeField, ListDrawerSettings(ShowIndexLabels = true)]
        private List<ShapePlayerBlockEntry> _preferredPlayerBlocks = new();

        public ShapePlaysetGenerationConfig CreateConfig()
        {
            return new ShapePlaysetGenerationConfig(
                _minBoardColumns,
                _maxBoardColumns,
                _minBoardRows,
                _maxBoardRows,
                _minObstacleCells,
                _maxObstacleCells,
                _minGeneratedBlockColumns,
                _maxGeneratedBlockColumns,
                _minGeneratedBlockRows,
                _maxGeneratedBlockRows,
                _preferredPlayerBlocks,
                _minPlayableRegionCells,
                _maxObstacleRepairNodes,
                _maxTilingSearchNodes,
                _maxCandidateAttempts,
                _maxSingletonFallbackBlocks);
        }
    }
}
