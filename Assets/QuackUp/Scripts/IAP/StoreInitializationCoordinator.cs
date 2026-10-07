using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace QuackUp.IAP
{
    public sealed class StoreInitializationCoordinator : IDisposable
    {
        private readonly Func<int, CancellationToken, UniTask<bool>> _initializeAttempt;
        private bool _isRunning;
        private bool _retryRequested;
        private bool _isDisposed;
        private int _generation;
        private CancellationTokenSource _activeCancellation;
        private UniTaskCompletionSource _workerCompletion;
        private UniTask _worker;

        public bool IsRunning => _isRunning;
        public int Generation => Volatile.Read(ref _generation);

        public StoreInitializationCoordinator(Func<int, CancellationToken, UniTask<bool>> initializeAttempt)
        {
            _initializeAttempt = initializeAttempt ?? throw new ArgumentNullException(nameof(initializeAttempt));
        }

        public UniTask Run()
        {
            if (_isDisposed) return UniTask.CompletedTask;
            if (_isRunning)
            {
                RequestRetry();
                return _worker;
            }

            _isRunning = true;
            _workerCompletion = new UniTaskCompletionSource();
            var worker = _workerCompletion.Task;
            _worker = worker;
            RunOwnedWorker(_workerCompletion).Forget();
            return worker;
        }

        public void RequestRetry()
        {
            if (_isDisposed) return;
            Interlocked.Increment(ref _generation);
            _retryRequested = true;
            _activeCancellation?.Cancel();
        }

        public void CancelCurrentAttempt()
        {
            if (_isDisposed) return;
            Interlocked.Increment(ref _generation);
            _activeCancellation?.Cancel();
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _retryRequested = false;
            _activeCancellation?.Cancel();
        }

        private async UniTaskVoid RunOwnedWorker(UniTaskCompletionSource workerCompletion)
        {
            try
            {
                while (!_isDisposed)
                {
                    _retryRequested = false;
                    var generation = Interlocked.Increment(ref _generation);
                    var attemptCancellation = new CancellationTokenSource();
                    _activeCancellation = attemptCancellation;
                    try
                    {
                        await _initializeAttempt(generation, attemptCancellation.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        if (!_retryRequested) break;
                    }
                    finally
                    {
                        if (ReferenceEquals(_activeCancellation, attemptCancellation))
                            _activeCancellation = null;
                        attemptCancellation.Dispose();
                    }

                    if (!_retryRequested) break;
                    await UniTask.Yield();
                }
            }
            catch (Exception exception)
            {
                workerCompletion.TrySetException(exception);
            }
            finally
            {
                _isRunning = false;
                if (ReferenceEquals(_workerCompletion, workerCompletion))
                {
                    _workerCompletion = null;
                    _worker = default;
                }
                workerCompletion.TrySetResult();
            }
        }
    }
}
