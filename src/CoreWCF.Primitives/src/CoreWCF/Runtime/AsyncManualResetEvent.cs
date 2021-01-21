// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Runtime
{
    // This class is based on the blog post https://blogs.msdn.microsoft.com/pfxteam/2012/02/11/building-async-coordination-primitives-part-1-asyncmanualresetevent/
    internal class AsyncManualResetEvent : IDisposable
    {
        private TaskCompletionSource<bool> _tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task WaitAsync()
        {
            CheckDisposed();
            return _tcs.Task;
        }

        public async Task<bool> WaitAsync(CancellationToken token)
        {
            CheckDisposed();

            if (!token.CanBeCanceled)
            {
                await WaitAsync();
                return true;
            }

            if (token.IsCancellationRequested)
            {
                return false;
            }

            var localTcs = new TaskCompletionSource<bool>();
            using (token.Register(TokenCancelledCallback, localTcs))
            {
                var tcs = _tcs;
                CheckDisposed();
                return await await Task.WhenAny(localTcs.Task, tcs.Task);
            }

        }

        private static void TokenCancelledCallback(object obj)
        {
            var localTcs = obj as TaskCompletionSource<bool>;
            localTcs?.TrySetResult(false);
        }

        public void Set()
        {
            CheckDisposed();
            _tcs?.TrySetResult(true);
        }

        public void Reset()
        {
            CheckDisposed();
            while (true)
            {
                var tcs = _tcs;
                if (tcs == null)
                {
                    return; // Disposed
                }

                if (!tcs.Task.IsCompleted ||
                Interlocked.CompareExchange(ref _tcs, new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously), tcs) == tcs)
                {
                    return;
                }
            }
        }

        private void CheckDisposed()
        {
            if (_tcs == null)
            {
                throw new ObjectDisposedException(nameof(AsyncManualResetEvent));
            }
        }

        public void Dispose()
        {
            var tcs = Interlocked.Exchange(ref _tcs, null);
            tcs?.TrySetResult(false);
        }
    }
}