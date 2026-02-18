namespace FitMe.Shared
{
    public struct ClearGridEvent
    {
        public bool ShouldClearGrid;

        public ClearGridEvent(bool shouldClearGrid)
        {
            ShouldClearGrid = shouldClearGrid;
        }
    }
}