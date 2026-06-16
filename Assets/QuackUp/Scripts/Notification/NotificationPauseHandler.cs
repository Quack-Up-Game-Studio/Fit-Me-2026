using UnityEngine;

namespace QuackUp.Notification
{
    public class NotificationPauseHandler : MonoBehaviour
    {
        private NotificationLifecycleManager _lifecycleManager;

        public void Construct(NotificationLifecycleManager lifecycleManager)
        {
            _lifecycleManager = lifecycleManager;
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            _lifecycleManager?.OnApplicationPauseChanged(pauseStatus);
        }
    }
}
