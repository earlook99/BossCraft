using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace GameSystem.Utils
{
    public static class AsyncUtilities
    {
        private static readonly System.Random _random = new System.Random();
        
        public static async Task WaitForSecondsAsync(float seconds, CancellationToken cancellationToken = default)
        {
            if (seconds <= 0) return;
            
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(seconds), cancellationToken);
            }
            catch (TaskCanceledException)
            {
                // Cancellation is expected, don't throw
            }
        }
        
        public static async Task NextFrameAsync(CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);
        }
        
        public static async Task WaitUntilAsync(Func<bool> predicate, CancellationToken cancellationToken = default)
        {
            while (!predicate() && !cancellationToken.IsCancellationRequested)
            {
                await Task.Yield();
            }
            cancellationToken.ThrowIfCancellationRequested();
        }
        
        public static async Task WaitWhileAsync(Func<bool> predicate, CancellationToken cancellationToken = default)
        {
            while (predicate() && !cancellationToken.IsCancellationRequested)
            {
                await Task.Yield();
            }
            cancellationToken.ThrowIfCancellationRequested();
        }
        
        public static async Task<T> WithTimeout<T>(Task<T> task, float timeoutSeconds)
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds)))
            {
                var completedTask = await Task.WhenAny(task, Task.Delay(Timeout.Infinite, cts.Token));
                if (completedTask == task)
                {
                    return await task;
                }
                throw new TimeoutException($"Operation timed out after {timeoutSeconds} seconds");
            }
        }
        
        public static async Task RunOnMainThreadAsync(Action action)
        {
            if (SynchronizationContext.Current != null)
            {
                action();
            }
            else
            {
                await Task.Factory.StartNew(action, CancellationToken.None, 
                    TaskCreationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
            }
        }
    }
    
    public class CancellationTokenManager : MonoBehaviour
    {
        private CancellationTokenSource _cts;
        
        public CancellationToken Token => _cts?.Token ?? CancellationToken.None;
        
        private void Awake()
        {
            _cts = new CancellationTokenSource();
        }
        
        private void OnDestroy()
        {
            Cancel();
            _cts?.Dispose();
        }
        
        public void Cancel()
        {
            _cts?.Cancel();
        }
        
        public void Reset()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
        }
    }
}