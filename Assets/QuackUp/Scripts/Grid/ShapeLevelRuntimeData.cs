using System.Collections.Generic;
using FitMe.Shared;
using UnityEngine;

namespace FitMe.Grid
{
    public sealed class ShapeLevelRuntimeData
    {
        public GridPreset Board { get; }
        public IReadOnlyList<ShapeObstacleRuntimeData> Obstacles { get; }
        public IReadOnlyList<BlockPreset> PlayerBlocks { get; }

        public ShapeLevelRuntimeData(
            GridPreset board,
            IReadOnlyList<ShapeObstacleRuntimeData> obstacles,
            IReadOnlyList<BlockPreset> playerBlocks)
        {
            Board = board;
            Obstacles = obstacles ?? System.Array.Empty<ShapeObstacleRuntimeData>();
            PlayerBlocks = playerBlocks ?? System.Array.Empty<BlockPreset>();
        }
    }

    public readonly struct ShapeObstacleRuntimeData
    {
        public BlockSchema Schema { get; }
        public Vector2Int Anchor { get; }
        public BlockColor Color { get; }
        public bool Petrified { get; }

        public ShapeObstacleRuntimeData(
            BlockSchema schema,
            Vector2Int anchor,
            BlockColor color,
            bool petrified)
        {
            Schema = schema;
            Anchor = anchor;
            Color = color;
            Petrified = petrified;
        }
    }
}
