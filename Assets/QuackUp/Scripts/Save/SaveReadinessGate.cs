namespace QuackUp.Save
{
    public sealed class SaveReadinessGate
    {
        public bool IsReady { get; private set; }
        private bool _explicitRemoteLoadAccepted;
        private long _generation;

        public bool CompleteReadiness()
        {
            if (IsReady) return false;
            IsReady = true;
            return true;
        }

        public bool TryBeginRemoteApply() => TryBeginRemoteApply(out _);

        public bool TryBeginRemoteApply(out long generation)
        {
            generation = _generation;
            return !IsReady && !_explicitRemoteLoadAccepted;
        }

        public long BeginExplicitRemoteApply() => ++_generation;

        public bool CompleteExplicitRemoteApply(long generation)
        {
            if (!IsCurrent(generation)) return false;
            _explicitRemoteLoadAccepted = true;
            return true;
        }

        public bool IsCurrent(long generation) => generation == _generation;
    }
}
