using System;
using System.Collections.Generic;
using FitMe.Shared;
using UnityEngine;

namespace FitMe.Grid
{
    public sealed class ShapeLevelRuntimeData
    {
        private readonly int[,] _playableMask;

        public GridPreset Board { get; }
        public Vector2Int GridSize { get; }
        public int[,] PlayableMask => (int[,])_playableMask.Clone();
        public IReadOnlyList<ShapeObstacleRuntimeData> Obstacles { get; }
        public IReadOnlyList<ShapePlayerBlockRuntimeEntry> PlayerBlockEntries { get; }
        // Kept as a compatibility view for preset-only callers. Inline entries are null here.
        public IReadOnlyList<BlockPreset> PlayerBlocks { get; }

        public ShapeLevelRuntimeData(
            GridPreset board,
            int[,] playableMask,
            IReadOnlyList<ShapeObstacleRuntimeData> obstacles,
            IReadOnlyList<ShapePlayerBlockRuntimeEntry> playerBlockEntries)
        {
            Board = board;
            _playableMask = playableMask == null ? new int[0, 0] : (int[,])playableMask.Clone();
            GridSize = new Vector2Int(_playableMask.GetLength(1), _playableMask.GetLength(0));
            Obstacles = obstacles ?? Array.Empty<ShapeObstacleRuntimeData>();
            PlayerBlockEntries = playerBlockEntries ?? Array.Empty<ShapePlayerBlockRuntimeEntry>();

            var presets = new List<BlockPreset>(PlayerBlockEntries.Count);
            for (var i = 0; i < PlayerBlockEntries.Count; i++)
                presets.Add(PlayerBlockEntries[i].Preset);
            PlayerBlocks = presets.AsReadOnly();
        }

        public ShapeLevelRuntimeData(
            GridPreset board,
            IReadOnlyList<ShapeObstacleRuntimeData> obstacles,
            IReadOnlyList<BlockPreset> playerBlocks)
            : this(board, CreatePlayableMask(board), obstacles, CreatePlayerBlockEntries(playerBlocks))
        {
        }

        private static int[,] CreatePlayableMask(GridPreset board)
        {
            if (!board || board.GridSize.x <= 0 || board.GridSize.y <= 0) return new int[0, 0];
            var mask = new int[board.GridSize.y, board.GridSize.x];
            if (board.PresetGridType == GridType.Rectangle)
            {
                for (var row = 0; row < mask.GetLength(0); row++)
                for (var column = 0; column < mask.GetLength(1); column++)
                    mask[row, column] = 1;
            }
            else if (board.PresetGridType == GridType.Custom && board.customGrid != null &&
                     board.customGrid.GetLength(0) == mask.GetLength(0) &&
                     board.customGrid.GetLength(1) == mask.GetLength(1))
            {
                Array.Copy(board.customGrid, mask, board.customGrid.Length);
            }
            return mask;
        }

        private static IReadOnlyList<ShapePlayerBlockRuntimeEntry> CreatePlayerBlockEntries(
            IReadOnlyList<BlockPreset> playerBlocks)
        {
            if (playerBlocks == null) return Array.Empty<ShapePlayerBlockRuntimeEntry>();
            var entries = new List<ShapePlayerBlockRuntimeEntry>(playerBlocks.Count);
            for (var i = 0; i < playerBlocks.Count; i++)
            {
                var preset = playerBlocks[i];
                entries.Add(new ShapePlayerBlockRuntimeEntry(preset, preset ? preset.BlockSchema?.schema : null));
            }
            return entries.AsReadOnly();
        }
    }

    public sealed class ShapePlayerBlockRuntimeEntry
    {
        private readonly int[,] _schema;

        public BlockPreset Preset { get; }
        public int[,] Schema => _schema == null ? null : (int[,])_schema.Clone();

        public ShapePlayerBlockRuntimeEntry(BlockPreset preset, int[,] schema)
        {
            Preset = preset;
            _schema = schema == null ? new int[0, 0] : (int[,])schema.Clone();
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