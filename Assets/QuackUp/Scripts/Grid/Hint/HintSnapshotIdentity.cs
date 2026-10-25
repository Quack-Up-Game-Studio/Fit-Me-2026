using System;
using System.Collections.Generic;
using System.Text;
using QuackUp.Utils;

namespace FitMe.Grid
{
    public static class HintSnapshotIdentity
    {
        public static bool TryCreate(
            int[,] playableMask,
            int[,] occupiedMask,
            int[,] obstacleMask,
            IReadOnlyList<int[,]> remainingBlockSchemas,
            int activeHandCount,
            out string identity)
        {
            identity = null;
            if (!HasSameDimensions(playableMask, occupiedMask) ||
                !HasSameDimensions(playableMask, obstacleMask) || remainingBlockSchemas == null ||
                activeHandCount < 1 || activeHandCount > remainingBlockSchemas.Count)
                return false;

            var builder = new StringBuilder();
            if (!AppendMask(builder, 'P', playableMask, null) ||
                !AppendMask(builder, 'O', occupiedMask, playableMask) ||
                !AppendMask(builder, 'X', obstacleMask, occupiedMask))
                return false;

            var shapes = new string[remainingBlockSchemas.Count];
            for (var index = 0; index < shapes.Length; index++)
                if (!TryGetOrientationShapeKey(remainingBlockSchemas[index], out shapes[index]))
                    return false;

            if (activeHandCount == shapes.Length)
                Array.Sort(shapes, StringComparer.Ordinal);

            builder.Append("|HC:").Append(activeHandCount).Append("|H:");
            if (activeHandCount < shapes.Length)
            {
                var swapPreviewIndex = activeHandCount;
                var first = shapes[0];
                var preview = shapes[swapPreviewIndex];
                if (string.CompareOrdinal(first, preview) <= 0)
                    builder.Append(first).Append('+').Append(preview);
                else
                    builder.Append(preview).Append('+').Append(first);
            }
            else
            {
                builder.Append(shapes[0]);
            }

            for (var index = 1; index < activeHandCount; index++)
                builder.Append('|').Append(shapes[index]);

            builder.Append("|Q:");
            var queueStart = activeHandCount < shapes.Length ? activeHandCount + 1 : activeHandCount;
            for (var index = queueStart; index < shapes.Length; index++)
                builder.Append('|').Append(shapes[index]);

            identity = builder.ToString();
            return true;
        }

        private static bool HasSameDimensions(int[,] first, int[,] second) =>
            first != null && second != null && first.Length > 0 &&
            first.GetLength(0) == second.GetLength(0) && first.GetLength(1) == second.GetLength(1);

        private static bool AppendMask(StringBuilder builder, char label, int[,] mask, int[,] mustContainMask)
        {
            builder.Append('|').Append(label).Append(':')
                .Append(mask.GetLength(0)).Append('x').Append(mask.GetLength(1)).Append(':');
            for (var row = 0; row < mask.GetLength(0); row++)
            for (var column = 0; column < mask.GetLength(1); column++)
            {
                var value = mask[row, column];
                if (value != 0 && value != 1) return false;
                if (value == 1 && mustContainMask != null && mustContainMask[row, column] != 1) return false;
                builder.Append(value);
            }
            return true;
        }

        private static bool TryGetOrientationShapeKey(int[,] schema, out string key)
        {
            key = null;
            if (schema == null || schema.Length == 0) return false;
            var cells = new List<TilingCell>();
            for (var row = 0; row < schema.GetLength(0); row++)
            for (var column = 0; column < schema.GetLength(1); column++)
            {
                if (schema[row, column] == 0) continue;
                if (schema[row, column] != 1) return false;
                cells.Add(new TilingCell(row, column));
            }
            if (cells.Count == 0) return false;

            var orientation = cells.ToArray();
            Normalize(orientation);
            Array.Sort(orientation);
            key = ShapeKey(orientation);
            return true;
        }

        private static void Normalize(TilingCell[] cells)
        {
            var minRow = int.MaxValue;
            var minColumn = int.MaxValue;
            for (var index = 0; index < cells.Length; index++)
            {
                minRow = Math.Min(minRow, cells[index].Row);
                minColumn = Math.Min(minColumn, cells[index].Column);
            }
            for (var index = 0; index < cells.Length; index++)
                cells[index] = new TilingCell(cells[index].Row - minRow, cells[index].Column - minColumn);
        }

        private static string ShapeKey(IReadOnlyList<TilingCell> cells)
        {
            var builder = new StringBuilder();
            for (var index = 0; index < cells.Count; index++)
                builder.Append(cells[index].Row).Append(',').Append(cells[index].Column).Append(';');
            return builder.ToString();
        }
    }
}
