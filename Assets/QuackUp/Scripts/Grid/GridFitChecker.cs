using System.Collections.Generic;

namespace FitMe.Grid
{
    internal static class GridFitChecker
    {
        internal static bool CheckAvailableBlocks(
            int[,] vacantSchema,
            IReadOnlyList<BlockModel> blocksToCheck,
            out List<BlockModel> availableBlocks)
        {
            availableBlocks = new List<BlockModel>();
            foreach (var block in blocksToCheck)
            {
                var schemas = block.BlockPreset.BlockSchemas;
                for (var i = 0; i < schemas.Count; i++)
                {
                    if (!CanFit(vacantSchema, schemas[i].schema)) continue;
                    availableBlocks.Add(block);
                    break;
                }
            }

            return availableBlocks.Count > 0;
        }

        internal static bool CanFit(int[,] vacantSchema, int[,] blockSchema)
        {
            return ArrayHelper.CanBFitInA(vacantSchema, blockSchema, out _);
        }
    }
}
