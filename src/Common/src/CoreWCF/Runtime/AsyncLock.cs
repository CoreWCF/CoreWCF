using System;
using System.Threading;
using System.Threading.Tasks;
#if DEBUG
using System.Diagnostics;
#endif

namespace CoreWCF.Runtime
{
    internal class AsyncLock
    {
#if DEBUG
        private StackTrace _lockTakenCallStack;
        private string _lockTakenCallStackString;
#endif
        private readonly SemaphoreSlim _semaphore;
        private readonly SafeSemaphoreRelease _semaphoreRelease;
        private AsyncLocal<bool> _lockTaken;

        public AsyncLock()
        {
            _semaphore = new SemaphoreSlim(1);
            _semaphoreRelease = new SafeSemaphoreRelease(this);
            _lockTaken = new AsyncLocal<bool>(LockTakenValueChanged);
            _lockTaken.Value = false;
        }

        private void LockTakenValueChanged(AsyncLocalValueChangedArgs<bool> obj)
        {
            // Without this fixup, when completing the call to await TakeLockAsync there is
            // a switch of Context and _localTaken will be reset to false. This is because
            // of leaving the task.

            if (obj.ThreadContextChanged)
            {
                _lockTaken.Value = obj.PreviousValue;
            }
        }

        public async Task<IDisposable> TakeLockAsync()
        {
            if (_lockTaken.Value)
                return null;

            await _semaphore.WaitAsync();
            _lockTaken.Value = true;
#if DEBUG
            _lockTakenCallStack = new StackTrace();
            _lockTakenCallStackString = _lockTakenCallStack.ToString();
#endif
            return _semaphoreRelease;
        }

        public Task<IDisposable> TakeLockAsync(TimeSpan timeout)
        {
            if (timeout == Timeout.InfiniteTimeSpan || timeout < TimeSpan.Zero || timeout == TimeSpan.MaxValue)
            {
                return TakeLockAsync(CancellationToken.None);
            }
            else
            {
                var cts = new CancellationTokenSource(timeout);
                var task = TakeLockAsync(cts.Token);
                _ = task.ContinueWith((antecedant) => cts.Dispose());
                return task;
            }
        }

        public async Task<IDisposable> TakeLockAsync(CancellationToken token)
        {
            if (_lockTaken.Value)
                return null;

            await _semaphore.WaitAsync(token);
            _lockTaken.Value = true;
#if DEBUG
            _lockTakenCallStack = new StackTrace();
            _lockTakenCallStackString = _lockTakenCallStack.ToString();
#endif
            return _semaphoreRelease;
        }

        public IDisposable TakeLock()
        {
            if (_lockTaken.Value)
                return null;

            _semaphore.Wait();
            _lockTaken.Value = true;
#if DEBUG
            _lockTakenCallStack = new StackTrace();
            _lockTakenCallStackString = _lockTakenCallStack.ToString();
#endif
            return _semaphoreRelease;
        }

        public IDisposable TakeLock(TimeSpan timeout)
        {
            if (_lockTaken.Value)
                return null;

            _semaphore.Wait(timeout);
            _lockTaken.Value = true;
#if DEBUG
            _lockTakenCallStack = new StackTrace();
            _lockTakenCallStackString = _lockTakenCallStack.ToString();
#endif
            return _semaphoreRelease;
        }

        public IDisposable TakeLock(int timeout)
        {
            if (_lockTaken.Value)
                return null;

            _semaphore.Wait(timeout);
            _lockTaken.Value = true;
#if DEBUG
            _lockTakenCallStack = new StackTrace();
            _lockTakenCallStackString = _lockTakenCallStack.ToString();
#endif
            return _semaphoreRelease;
        }

        public struct SafeSemaphoreRelease : IDisposable
        {
            private readonly AsyncLock _asyncLock;

            public SafeSemaphoreRelease(AsyncLock asyncLock)
            {
                _asyncLock = asyncLock;
            }

            public void Dispose()
            {
#if DEBUG
                _asyncLock._lockTakenCallStack = null;
                _asyncLock._lockTakenCallStackString = null;
#endif
                _asyncLock._lockTaken.Value = false;
                _asyncLock._semaphore.Release();
            }
        }
    }
}