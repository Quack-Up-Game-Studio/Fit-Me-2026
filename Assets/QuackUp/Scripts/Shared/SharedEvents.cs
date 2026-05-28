namespace FitMe.Shared
{
    public struct ClearGridEvent
    {
        public readonly bool ShouldClearGrid;
        public readonly bool ShouldDestroyObstacles;

        public ClearGridEvent(bool shouldClearGrid, bool shouldDestroyObstacles)
        {
            ShouldClearGrid = shouldClearGrid;
            ShouldDestroyObstacles = shouldDestroyObstacles;
        }
    }
    
    public struct ContinueEvent{}
}