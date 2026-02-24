using System;
using UnityEngine;

namespace FitMe.GameData
{
    public class PlaytimePauseHandler : MonoBehaviour
    {
        private PlaytimeTracker _playtimeTracker;
        
        public void Construct(PlaytimeTracker playtimeTracker)
        {
            _playtimeTracker = playtimeTracker;
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                _playtimeTracker?.StopTimer();
            }
            else
            {
                _playtimeTracker?.StartTimer();
            }
        }
    }
}