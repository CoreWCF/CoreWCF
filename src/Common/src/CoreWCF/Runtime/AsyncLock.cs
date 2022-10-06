// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.ObjectPool;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Runtime
{
    internal class AsyncLock : IAsyncDisposable
    {
        private static readonly ObjectPool<SemaphoreSlim> s_semaphorePool = (new DefaultObjectPoolProvider { MaximumRetained = 100 })
            .Create(new SemaphoreSlimPooledObjectPolicy());

        private AsyncLocal<SemaphoreSlim> _currentSemaphore;
        private SemaphoreSlim _topLevelSemaphore;
        private bool _isDisposed;

        public AsyncLock()
        {
            _topLevelSemaphore = s_semaphorePool.Get();
            _currentSemaphore = new AsyncLocal<SemaphoreSlim>();
        }

        public Task<IAsyncDisposable> TakeLockAsync()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(AsyncLock));

            _currentSemaphore.Value = _currentSemaphore.Value ?? _topLevelSemaphore;
            SemaphoreSlim currentSem = _currentSemaphore.Value;
            var nextSem = s_semaphorePool.Get();
            _currentSemaphore.Value = nextSem;
            var safeRelease = new SafeSemaphoreRelease(currentSem, nextSem, this);
            return TakeLockCoreAsync(currentSem, safeRelease);
        }

        private async Task<IAsyncDisposable> TakeLockCoreAsync(SemaphoreSlim currentSemaphore, SafeSemaphoreRelease safeSemaphoreRelease)
        {
            await currentSemaphore.WaitAsync();
            return safeSemaphoreRelease;
        }

        public IDisposable TakeLock()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(AsyncLock));

            _currentSemaphore.Value = _currentSemaphore.Value ?? _topLevelSemaphore;
            SemaphoreSlim currentSem = _currentSemaphore.Value;
            currentSem.Wait();
            var nextSem = s_semaphorePool.Get();
            _currentSemaphore.Value = nextSem;
            return new SafeSemaphoreRelease(currentSem, nextSem, this);
        }

        public async ValueTask DisposeAsync()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            // Ensure the lock isn't held. If it is, wait for it to be released
            // before completing the dispose.
            await _topLevelSemaphore.WaitAsync();
            _topLevelSemaphore.Release();
            s_semaphorePool.Return(_topLevelSemaphore);
            _topLevelSemaphore = null;
        }

        private struct SafeSemaphoreRelease : IAsyncDisposable, IDisposable
        {
            private SemaphoreSlim _currentSemaphore;
            private SemaphoreSlim _nextSemaphore;
            private AsyncLock _asyncLock;

            public SafeSemaphoreRelease(SemaphoreSlim currentSemaphore, SemaphoreSlim nextSemaphore, AsyncLock asyncLock)
            {
                _currentSemaphore = currentSemaphore;
                _nextSemaphore = nextSemaphore;
                _asyncLock = asyncLock;
            }

            public ValueTask DisposeAsync()
            {
                Debug.Assert(_nextSemaphore == _asyncLock._currentSemaphore.Value, "_nextSemaphore was expected to by the current semaphore");
                // Update _asyncLock._currentSemaphore in the calling ExecutionContext
                // and defer any awaits to DisposeCoreAsync(). If this isn't done, the
                // update will happen in a copy of the ExecutionContext and the caller
                // won't see the changes.
                if (_currentSemaphore == _asyncLock._topLevelSemaphore)
                {
                    _asyncLock._currentSemaphore.Value = null;
                }
                else
                {
                    _asyncLock._currentSemaphore.Value = _currentSemaphore;
                }

                return DisposeCoreAsync();
            }

            private async ValueTask DisposeCoreAsync()
            {
                await _nextSemaphore.WaitAsync();
                _currentSemaphore.Release();
                _nextSemaphore.Release();
                s_semaphorePool.Return(_nextSemaphore);
            }

            public void Dispose()
            {
                Debug.Assert(_nextSemaphore == _asyncLock._currentSemaphore.Value, "_nextSemaphore was expected to by the current semaphore");
                if (_currentSemaphore == _asyncLock._topLevelSemaphore)
                {
                    _asyncLock._currentSemaphore.Value = null;
                }
                else
                {
                    _asyncLock._currentSemaphore.Value = _currentSemaphore;
                }

                _nextSemaphore.Wait();
                _currentSemaphore.Release();
                _nextSemaphore.Release();
                s_semaphorePool.Return(_nextSemaphore);
            }
        }

        private class SemaphoreSlimPooledObjectPolicy : PooledObjectPolicy<SemaphoreSlim>
        {
            public override SemaphoreSlim Create()
            {
                return new SemaphoreSlim(1);
            }

            public override bool Return(SemaphoreSlim obj)
            {
                if (obj.CurrentCount != 1)
                {
                    Debug.Fail("Shouldn't be returning semaphore with a count != 1");
                    return false;
                }

                return true;
            }
        }
    }
}
