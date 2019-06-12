using System;
using CoreWCF.Runtime;
using CoreWCF;
using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Channels
{
    class SynchronizedMessageSource
    {
        IMessageSource source;
        SemaphoreSlim sourceLock;

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
                if(lockAcquired)
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
                if(lockAcquired)
                    sourceLock.Release();
            }
        }
    }
}
