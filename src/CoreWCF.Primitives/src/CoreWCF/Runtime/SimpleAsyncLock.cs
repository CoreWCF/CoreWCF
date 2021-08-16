// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Runtime
{
    /// <summary>
    /// This class provides a locking mechanism compatible with task async patterns.
    /// </summary>
    /// <remarks>
    /// Unlike the <see cref="AsyncLock"/> it does not have the complexity of lock
    /// tracking and it ensures SemaphoreSlim does not leak memory or handles by hiding
    /// the access to certain members which would create resources that require disposal.
    /// </remarks>
    internal sealed class SimpleAsyncLock
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1);

        public async Task<IDisposable> TakeLockAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(timeout, cancellationToken);
            return new SafeSemaphoreRelease(this);
        }

        public async Task<IDisposable> TakeLockAsync(CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            return new SafeSemaphoreRelease(this);
        }

        public IDisposable TakeLock(CancellationToken cancellationToken = default)
        {
            _semaphore.Wait(cancellationToken);
            return new SafeSemaphoreRelease(this);
        }

        public IDisposable TakeLock(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            _semaphore.Wait(timeout, cancellationToken);
            return new SafeSemaphoreRelease(this);
        }

        private readonly struct SafeSemaphoreRelease : IDisposable
        {
            private readonly SimpleAsyncLock _asyncLock;

            public SafeSemaphoreRelease(SimpleAsyncLock asyncLock)
            {
                _asyncLock = asyncLock;
            }

            public void Dispose()
            {
                _asyncLock._semaphore.Release();
            }
        }
    }
}
