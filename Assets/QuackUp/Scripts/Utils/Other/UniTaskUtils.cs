using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace QuackUp.Utils
{
    /// <summary>
    /// A promise-like class that can be used to represent the future result of an asynchronous operation.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    public class Promise<T> : IDisposable
    {
        private readonly UniTaskCompletionSource<T> _completionSource = new();
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly IDisposable _cancellationRegistration;
        private bool _isDisposed;
    
        public UniTask<T> Task => _completionSource.Task;
        public CancellationToken CancellationToken => _cancellationTokenSource.Token;
    
        public Promise()
        {
            _cancellationRegistration = CancellationToken.Register(() => 
                TrySetCanceled());
        }
    
        public bool TrySetResult(T result) => _completionSource.TrySetResult(result);
        public bool TrySetException(Exception exception) => _completionSource.TrySetException(exception);
        public bool TrySetCanceled()
        {
            if (_isDisposed) return false;
            return _completionSource.TrySetCanceled(CancellationToken);
        }

        public void Cancel()
        {
            if (_isDisposed) return;
            _cancellationTokenSource.Cancel();
        }

        public void CancelAfter(TimeSpan delay)
        {
            if (_isDisposed) return;
            _cancellationTokenSource.CancelAfter(delay);
        }
    
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _cancellationRegistration?.Dispose();
            _cancellationTokenSource?.Dispose();
        }
    }
}