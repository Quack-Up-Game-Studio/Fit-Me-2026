using System.Collections.Generic;
using FitMe.Shared;
using UnityEngine;

namespace FitMe.Grid
{
    public sealed class ShapeLevelRuntimeData
    {
        public GridPreset Board { get; }
        public IReadOnlyList<ShapeObstacleRuntimeData> Obstacles { get; }
        public IReadOnlyList<ShapePlayerBlockRuntimeData> PlayerBlocks { get; }

        public ShapeLevelRuntimeData(
            GridPreset board,
            IReadOnlyList<ShapeObstacleRuntimeData> obstacles,
            IReadOnlyList<ShapePlayerBlockRuntimeData> playerBlocks)
        {
            Board = board;
            Obstacles = obstacles ?? System.Array.Empty<ShapeObstacleRuntimeData>();
            PlayerBlocks = playerBlocks ?? System.Array.Empty<ShapePlayerBlockRuntimeData>();
        }
    }

    public readonly struct ShapePlayerBlockRuntimeData
    {
        public BlockSchema Schema { get; }
        public BlockColor Color { get; }
        public bool AllowRotation { get; }
        public BlockShape VisualStyle { get; }

        public ShapePlayerBlockRuntimeData(
            BlockSchema schema,
            BlockColor color,
            bool allowRotation,
            BlockShape visualStyle)
        {
            Schema = schema;
            Color = color;
            AllowRotation = allowRotation;
            VisualStyle = visualStyle;
        }
    }

    public readonly struct ShapeObstacleRuntimeData
    {
        public BlockSchema Schema { get; }
        public Vector2Int Anchor { get; }
        public BlockColor Color { get; }
        public bool Petrified { get; }
        public BlockShape VisualStyle { get; }

        public ShapeObstacleRuntimeData(
            BlockSchema schema,
            Vector2Int anchor,
            BlockColor color,
            bool petrified,
            BlockShape visualStyle)
        {
            Schema = schema;
            Anchor = anchor;
            Color = color;
            Petrified = petrified;
            VisualStyle = visualStyle;
        }
    }
}
