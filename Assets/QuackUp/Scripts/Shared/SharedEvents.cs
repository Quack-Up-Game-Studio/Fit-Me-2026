namespace FitMe.Shared
{
    public struct ContinueEvent
    {
        public readonly bool ShouldClearGrid;
        public readonly bool ShouldDestroyObstacles;

        public ContinueEvent(bool shouldClearGrid, bool shouldDestroyObstacles)
        {
            ShouldClearGrid = shouldClearGrid;
            ShouldDestroyObstacles = shouldDestroyObstacles;
        }
    }
}