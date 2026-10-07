using System;

namespace QuackUp.SceneManagement
{
    public interface IPendingScenePayload
    {
        void Set<T>(T payload);
        bool TryTake<T>(out T payload);
    }

    public sealed class PendingScenePayload : IPendingScenePayload
    {
        private object _payload;

        public void Set<T>(T payload)
        {
            _payload = payload;
        }

        public bool TryTake<T>(out T payload)
        {
            if (_payload is T typedPayload)
            {
                _payload = null;
                payload = typedPayload;
                return true;
            }

            payload = default;
            return false;
        }
    }
}
