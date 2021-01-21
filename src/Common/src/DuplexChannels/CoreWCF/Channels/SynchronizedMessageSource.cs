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
        private IMessageSource source;
        private SemaphoreSlim sourceLock;

        public SynchronizedMessageSource(IMessageSource source)
        {
            this.source = source;
            sourceLock = new SemaphoreSlim(1);
        }

        public async Task<bool> WaitForMessageAsync(CancellationToken token)
        {
            bool lockAcquired = false;
            try
            {
                await sourceLock.WaitAsync(token);
                lockAcquired = true;
                return await source.WaitForMessageAsync(token);
            }
            catch (OperationCanceledException)
            {
                // TODO: Fix the timeout value reported
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new TimeoutException(
                                                SR.Format(SR.WaitForMessageTimedOut, TimeSpan.Zero),
                                                TimeoutHelper.CreateEnterTimedOutException(TimeSpan.Zero)));
            }
            finally
            {
                if (lockAcquired)
                    sourceLock.Release();
            }
        }

        public async Task<Message> ReceiveAsync(CancellationToken token)
        {
            bool lockAcquired = false;
            try
            {
                await sourceLock.WaitAsync(token);
                lockAcquired = true;
                return await source.ReceiveAsync(token);
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
                    sourceLock.Release();
            }
        }
    }
}
