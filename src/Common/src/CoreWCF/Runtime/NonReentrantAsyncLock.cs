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
    /// Unlike the <see cref="ReentrantAsyncLock"/> it does not provide a reentrant (aka. recursive) locking
    /// and can therefore only be used in scenarios where we have clear control over the execution path.
    /// </remarks>
    internal sealed class NonReentrantAsyncLock
    {
        // based of the ideas in https://devblogs.microsoft.com/pfxteam/building-async-coordination-primitives-part-6-asynclock/
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1);
        private readonly Task<IDisposable> _releaser;
#if DEBUG
        private static readonly AsyncLocal<bool> s_reentranceFlag = new AsyncLocal<bool>();
#endif

        public NonReentrantAsyncLock()
        {
            _releaser = Task.FromResult<IDisposable>(new ReusableReleaser(this));
        }

        public Task<IDisposable> TakeLockAsync()
        {
#if DEBUG
            Fx.Assert(!s_reentranceFlag.Value, "Usage of lock is reentrant but this class does not support it!");
            s_reentranceFlag.Value = true;
#endif
            // we avoid allocation of new releaser instances if task does not need awaiting
            // SemaphoreSlim might return a completed task if no actual waiting needed to be done.
            var wait = _semaphore.WaitAsync();
            return wait.IsCompleted
                ? _releaser
                : wait.ContinueWith(
                    (_, state) => (IDisposable)new OneTimeReleaser((NonReentrantAsyncLock)state),
                    this,
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
        }

        /// <summary>
        /// This releaser does a release on every dispose.
        /// </summary>
        private readonly struct ReusableReleaser : IDisposable
        {
            private readonly NonReentrantAsyncLock _asyncLock;

            public ReusableReleaser(NonReentrantAsyncLock asyncLock)
            {
                _asyncLock = asyncLock;
            }

            public void Dispose()
            {
                _asyncLock._semaphore.Dispose();
#if DEBUG
                s_reentranceFlag.Value = false;
#endif
            }
        }

        /// <summary>
        /// This releaser avoid multiple release calls against the parent lock.
        /// </summary>
        private struct OneTimeReleaser : IDisposable
        {
            private NonReentrantAsyncLock _asyncLock;

            public OneTimeReleaser(NonReentrantAsyncLock asyncLock)
            {
                _asyncLock = asyncLock;
            }

            public void Dispose()
            {
                var asyncLock = Interlocked.Exchange(ref _asyncLock, null);
                Fx.Assert(asyncLock == null, "Double unlock, this must be avoided!");
                asyncLock?._semaphore.Release();
#if DEBUG
                s_reentranceFlag.Value = false;
#endif
            }
        }
    }
}
