// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    internal class SynchronizedMessageSource
    {
        private readonly IMessageSource _source;
        private readonly SemaphoreSlim _sourceLock;

        public SynchronizedMessageSource(IMessageSource source)
        {
            _source = source;
            _sourceLock = new SemaphoreSlim(1);
        }

        public async Task<bool> WaitForMessageAsync(CancellationToken token)
        {
            bool lockAcquired = false;
            try
            {
                await _sourceLock.WaitAsync(token);
                lockAcquired = true;
                return await _source.WaitForMessageAsync(token);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            finally
            {
                if (lockAcquired)
                {
                    _sourceLock.Release();
                }
            }
        }

        public async Task<Message> ReceiveAsync(CancellationToken token)
        {
            bool lockAcquired = false;
            try
            {
                await _sourceLock.WaitAsync(token);
                lockAcquired = true;
                return await _source.ReceiveAsync(token);
            }
            catch (OperationCanceledException)
            {
                // TODO: Fix the timeout value reported
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new TimeoutException(
                                                SR.Format(SR.ReceiveTimedOut2, TimeSpan.Zero),
                                                TimeoutHelper.CreateEnterTimedOutException(TimeSpan.Zero)));
            }
            finally
            {
                if (lockAcquired)
                {
                    _sourceLock.Release();
                }
            }
        }
    }
}
