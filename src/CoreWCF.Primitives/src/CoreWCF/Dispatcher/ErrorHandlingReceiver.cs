using System;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Runtime;
using CoreWCF.Channels;

namespace CoreWCF.Dispatcher
{
    internal class ErrorHandlingReceiver
    {
        ChannelDispatcher dispatcher;
        IChannelBinder binder;

        internal ErrorHandlingReceiver(IChannelBinder binder, ChannelDispatcher dispatcher)
        {
            this.binder = binder;
            this.dispatcher = dispatcher;
        }

        internal async Task CloseAsync()
        {
            try
            {
                await binder.Channel.CloseAsync();
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }
                HandleError(e);
            }
        }

        void HandleError(Exception e)
        {
            if (dispatcher != null)
            {
                dispatcher.HandleError(e);
            }
        }

        void HandleErrorOrAbort(Exception e)
        {
            if ((dispatcher == null) || !dispatcher.HandleError(e))
            {
                if (binder.HasSession)
                {
                    binder.Abort();
                }
            }
        }

        internal async Task<TryAsyncResult<RequestContext>> TryReceiveAsync(CancellationToken token)
        {
            try
            {
                return await binder.TryReceiveAsync(token);
            }
            catch (CommunicationObjectAbortedException)
            {
                return TryAsyncResult.FromResult((RequestContext)null);
            }
            catch (CommunicationObjectFaultedException)
            {
                return TryAsyncResult.FromResult((RequestContext)null);
            }
            catch (CommunicationException e)
            {
                HandleError(e);
                return TryAsyncResult<RequestContext>.FailedResult;
            }
            catch (TimeoutException e)
            {
                HandleError(e);
                return TryAsyncResult<RequestContext>.FailedResult;
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }
                HandleErrorOrAbort(e);
                return TryAsyncResult<RequestContext>.FailedResult;
            }
        }

        internal async Task WaitForMessageAsync()
        {
            try
            {
                await binder.WaitForMessageAsync(CancellationToken.None);
            }
            catch (CommunicationObjectAbortedException) { }
            catch (CommunicationObjectFaultedException) { }
            catch (CommunicationException e)
            {
                HandleError(e);
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }
                HandleErrorOrAbort(e);
            }
        }
    }
}