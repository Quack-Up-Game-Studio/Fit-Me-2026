namespace FitMe.Grid
{
    internal static class VacantSchemaSolver
    {
        internal static bool Create(
            CellInstance[,] cellArray,
            int rows,
            int columns,
            out int[,] vacantSchema,
            out int vacantCount)
        {
            vacantCount = 0;
            vacantSchema = new int[rows, columns];
            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    var cell = cellArray[row, column];
                    if (cell == null) continue;

                    var currentAtom = cell.Model.CurrentAtom.Value;
                    if (currentAtom != null &&
                        currentAtom.Model.ParentBlock.Value.Model.BlockState.CurrentValue is not BlockState.Exploding)
                        continue;

                    vacantSchema[row, column] = 1;
                    vacantCount++;
                }
            }

            return vacantCount > 0;
        }
    }
}
