using R3;
using UnityEngine;

namespace FitMe.Notification
{
    public class NotificationPauseHandler : MonoBehaviour
    {
        public readonly Subject<bool> OnPauseChanged = new();

        private void OnApplicationPause(bool pauseStatus)
        {
            OnPauseChanged.OnNext(pauseStatus);
        }

        private void OnDestroy()
        {
            OnPauseChanged.OnCompleted();
        }
    }
}
