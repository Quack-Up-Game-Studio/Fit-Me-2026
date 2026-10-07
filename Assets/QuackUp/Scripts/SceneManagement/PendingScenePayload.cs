using System;

namespace QuackUp.SceneManagement
{
    public interface IPendingScenePayload
    {
        void Set<T>(T payload);
        bool TryTake<T>(out T payload);
        void SetActive<T>(T payload);
        bool TryGetActive<T>(out T payload);
        bool TryRequeueActive();
    }

    public sealed class PendingScenePayload : IPendingScenePayload
    {
        private object _payload;
        private object _activePayload;

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

        public void SetActive<T>(T payload)
        {
            _activePayload = payload;
        }

        public bool TryGetActive<T>(out T payload)
        {
            if (_activePayload is T typedPayload)
            {
                payload = typedPayload;
                return true;
            }

            payload = default;
            return false;
        }

        public bool TryRequeueActive()
        {
            if (_activePayload == null) return false;
            _payload = _activePayload;
            return true;
        }
    }
}
