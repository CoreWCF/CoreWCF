using System;
using Microsoft.Runtime;
using Microsoft.ServiceModel;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.ServiceModel.Channels
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
            bool lockAquired = false;
            try
            {
                await sourceLock.WaitAsync(token);
                lockAquired = true;
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
                if(lockAquired)
                    sourceLock.Release();
            }
        }

        public async Task<Message> ReceiveAsync(CancellationToken token)
        {
            bool lockAquired = false;
            try
            {
                await sourceLock.WaitAsync(token);
                lockAquired = true;
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
                if(lockAquired)
                    sourceLock.Release();
            }
        }
    }
}
