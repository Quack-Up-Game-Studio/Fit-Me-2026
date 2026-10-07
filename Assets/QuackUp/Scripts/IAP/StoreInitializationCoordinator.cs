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
        private UniTaskCompletionSource<bool> _workerCompletion;
        private UniTask<bool> _worker;

        public bool IsRunning => _isRunning;
        public int Generation => Volatile.Read(ref _generation);

        public StoreInitializationCoordinator(Func<int, CancellationToken, UniTask<bool>> initializeAttempt)
        {
            _initializeAttempt = initializeAttempt ?? throw new ArgumentNullException(nameof(initializeAttempt));
        }

        public UniTask<bool> Run()
        {
            if (_isDisposed) return UniTask.FromResult(false);
            if (_isRunning) return _worker;

            _isRunning = true;
            _workerCompletion = new UniTaskCompletionSource<bool>();
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

        private async UniTaskVoid RunOwnedWorker(UniTaskCompletionSource<bool> workerCompletion)
        {
            var attemptSucceeded = false;
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
                        attemptSucceeded = await _initializeAttempt(generation, attemptCancellation.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        attemptSucceeded = false;
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
                workerCompletion.TrySetResult(attemptSucceeded && !_isDisposed);
            }
        }
    }
}
