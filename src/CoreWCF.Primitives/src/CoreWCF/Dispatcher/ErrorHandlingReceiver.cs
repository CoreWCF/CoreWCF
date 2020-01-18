using CoreWCF.Channels;
using CoreWCF.Runtime;
using System;
using System.Threading;
using System.Threading.Tasks;

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

        internal async Task<(RequestContext requestContext, bool success)> TryReceiveAsync(CancellationToken token)
        {
            try
            {
                return await binder.TryReceiveAsync(token);
            }
            catch (CommunicationObjectAbortedException)
            {
                return (null, true);
            }
            catch (CommunicationObjectFaultedException)
            {
                return (null, true);
            }
            catch (CommunicationException e)
            {
                HandleError(e);
                return (null, false);
            }
            catch (TimeoutException e)
            {
                HandleError(e);
                return (null, false);
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }
                HandleErrorOrAbort(e);
                return (null, false);
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