using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Runtime;

namespace Microsoft.ServiceModel.Dispatcher
{
    internal class ErrorHandlingAcceptor
    {
        readonly ChannelDispatcher dispatcher;
        readonly IListenerBinder binder;

        internal ErrorHandlingAcceptor(IListenerBinder binder, ChannelDispatcher dispatcher)
        {
            if (binder == null)
            {
                Fx.Assert("binder is null");
            }
            if (dispatcher == null)
            {
                Fx.Assert("dispatcher is null");
            }

            this.binder = binder;
            this.dispatcher = dispatcher;
        }

        internal async Task CloseAsync()
        {
            try
            {
                await binder.Listener.CloseAsync();
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
                // We only stop if the listener faults.  It is a bug
                // if the listener is in an invalid state and does not
                // fault.  So there are no cases today where this aborts.
            }
        }

        internal async Task<TryAsyncResult<IChannelBinder>> TryAcceptAsync(CancellationToken token)
        {
            IChannelBinder channelBinder;
            try
            {
                channelBinder = await binder.AcceptAsync(token);
                if (channelBinder != null)
                {
                    //dispatcher.PendingChannels.Add(channelBinder.Channel);
                }
                return TryAsyncResult.FromResult(channelBinder);
            }
            catch (CommunicationObjectAbortedException)
            {
                channelBinder = null;
                return TryAsyncResult.FromResult(channelBinder);
            }
            catch (CommunicationObjectFaultedException)
            {
                channelBinder = null;
                return TryAsyncResult.FromResult(channelBinder);
            }
            catch (OperationCanceledException)
            {
                return TryAsyncResult<IChannelBinder>.FailedResult;
            }
            catch (CommunicationException e)
            {
                HandleError(e);
                return TryAsyncResult<IChannelBinder>.FailedResult;
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }
                HandleErrorOrAbort(e);
                return TryAsyncResult<IChannelBinder>.FailedResult;
            }
        }

        //internal async Task WaitForChannelAsync()
        //{
        //    try
        //    {
        //        await this.binder.Listener.WaitForChannelAsync(CancellationToken.None);
        //    }
        //    catch (CommunicationObjectAbortedException) { }
        //    catch (CommunicationObjectFaultedException) { }
        //    catch (CommunicationException e)
        //    {
        //        this.HandleError(e);
        //    }
        //    catch (Exception e)
        //    {
        //        if (Fx.IsFatal(e))
        //        {
        //            throw;
        //        }
        //        this.HandleErrorOrAbort(e);
        //    }
        //}
    }
}