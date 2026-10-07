namespace QuackUp.Save
{
    public sealed class SaveReadinessGate
    {
        public bool IsReady { get; private set; }

        public bool CompleteReadiness()
        {
            if (IsReady) return false;
            IsReady = true;
            return true;
        }

        public bool TryBeginRemoteApply() => !IsReady;
    }
}
