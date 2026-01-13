using System;
using Cysharp.Threading.Tasks;
using R3;
using Time = UnityEngine.Time;

namespace QuackUp.Utils
{
    public static class ObservableUtils
    {
        /// <summary>
        /// Starts updating every frame when the predicate becomes true, stops when it becomes false
        /// </summary>
        /// <typeparam name="T">The type of the source observable</typeparam>
        /// <param name="source">The source observable to watch</param>
        /// <param name="predicate">The condition that determines when to start updating</param>
        /// <param name="frameProvider">Optional frame provider for custom update timing</param>
        /// <returns>An observable that emits every frame while the predicate is true</returns>
        public static Observable<Unit> EveryUpdateWhen<T>(this Observable<T> source, Func<T, bool> predicate, FrameProvider frameProvider = null)
        {
            var ticker = frameProvider == null 
                ? Observable.EveryUpdate() 
                : Observable.EveryUpdate(frameProvider);
    
            return source
                .SelectMany(t => predicate(t) 
                    ? ticker.TakeUntil(source.Where(x => !predicate(x)))
                    : Observable.Empty<Unit>());
        }

        /// <summary>
        /// Ignores the first value emitted when subscribing to the observable. Useful for input handling to avoid immediate triggers.
        /// </summary>
        /// <param name="source"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static Observable<T> IgnoreFirstValueWhenSubscribe<T>(this Observable<T> source)
        {
            return source.Skip(1);
        }
    }

    public class PausableTimer : IDisposable
    {
        private readonly TimeSpan _duration;
        private readonly Action _callback;
        private readonly FrameProvider _frameProvider;
        private bool IgnoreTimeScale { get; set; }
        private readonly IDisposable _updateSubscription;
        
        private bool _isRunning;
        private TimeSpan _elapsedTime;
        private UniTaskCompletionSource _tcs;
        
        public PausableTimer(TimeSpan duration, Action callback = null, FrameProvider frameProvider = null, bool ignoreTimeScale = false)
        {
            _duration = duration;
            _elapsedTime = TimeSpan.Zero;
            _isRunning = true;
            _callback = callback;
            IgnoreTimeScale = ignoreTimeScale;
            _frameProvider = frameProvider ?? UnityFrameProvider.Update;
            _updateSubscription = Observable.EveryUpdate(_frameProvider) 
                .Where(_ => _isRunning)
                .Subscribe(_ => Update());
            _tcs = new UniTaskCompletionSource();
        }
        
        public void Dispose()
        {
            _isRunning = false;
            _tcs.TrySetCanceled();
            _updateSubscription.Dispose();
        }
        
        public void Start()
        {
            _isRunning = true;
        }
        
        public void Pause()
        {
            _isRunning = false;
        }
        
        public void Reset()
        {
            _elapsedTime = TimeSpan.Zero;
            _isRunning = false;
            _tcs = new UniTaskCompletionSource();
        }
        
        public UniTask ToUniTask()
        {
            return _tcs.Task;
        }
        
        private void Update()
        {
            var deltaTime = IgnoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
            if (_frameProvider.Equals(UnityFrameProvider.FixedUpdate))
            {
                deltaTime = IgnoreTimeScale ? Time.fixedUnscaledDeltaTime : Time.fixedDeltaTime;
            }
            _elapsedTime += TimeSpan.FromSeconds(deltaTime);
            if (_elapsedTime < _duration) return;
            _isRunning = false;
            _callback?.Invoke();
            _tcs.TrySetResult();
        }
    }
}