using System;
using R3;

namespace QuackUp.Input
{
    public static class ObservableInputUtils
    {
        private record InputLockState
        {
            public InputType? ActiveType { get; set; }
            public DateTimeOffset LastActiveTime { get; set; } = DateTimeOffset.UtcNow;
        }
        
        /// <summary>
        /// Resolves input types with a locking mechanism to prevent simultaneous inputs.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="lockTime">Lock time in milliseconds. Default is 16ms (approx. one frame at 60fps).</param>
        /// <returns></returns>
        public static Observable<InputType> ResolveInputType(this Observable<InputType> source, float lockTime = 16f)
        {
            return source
                .Scan(
                    new InputLockState(),
                    (currentState, newType) =>
                    {
                        var now = DateTimeOffset.UtcNow;

                        // RULE 1: If nothing is locked, the new type becomes the locked type.
                        if (currentState.ActiveType == null)
                        {
                            return currentState with
                            {
                                ActiveType = newType,
                                LastActiveTime = now
                            };
                        }

                        // RULE 2: If the new type matches the currently locked type, update its timestamp.
                        if (currentState.ActiveType == newType)
                        {
                            return currentState with
                            {
                                LastActiveTime = now // Reset the timer!
                            };
                        }

                        // RULE 3: If the new type is a different type AND the lock has expired, switch the lock.
                        bool lockHasExpired = (now - currentState.LastActiveTime) >
                                              TimeSpan.FromMilliseconds(lockTime);
                        if (lockHasExpired)
                        {
                            return currentState with
                            {
                                ActiveType = newType, // Switch the lock to the new type
                                LastActiveTime = now
                            };
                        }

                        // RULE 4: If it's a different type but the lock hasn't expired, ignore the event and keep the old state.
                        return currentState;
                    }
                )
                .Select(state => state.ActiveType ?? default) // Emit the locked type, or default if null.
                .DistinctUntilChanged();
        }
        
    }
}