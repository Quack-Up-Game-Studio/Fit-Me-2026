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
    
        public UniTask<T> Task => _completionSource.Task;
        public CancellationToken CancellationToken => _cancellationTokenSource.Token;
    
        public Promise()
        {
            _cancellationRegistration = CancellationToken.Register(() => 
                TrySetCanceled());
        }
    
        public bool TrySetResult(T result) => _completionSource.TrySetResult(result);
        public bool TrySetException(Exception exception) => _completionSource.TrySetException(exception);
        public bool TrySetCanceled() => _completionSource.TrySetCanceled(CancellationToken);
    
        public void Cancel() => _cancellationTokenSource.Cancel();
        public void CancelAfter(TimeSpan delay) => _cancellationTokenSource.CancelAfter(delay);
    
        public void Dispose()
        {
            _cancellationRegistration?.Dispose();
            _cancellationTokenSource?.Dispose();
            _completionSource?.Task.Forget();
        }
    }
}