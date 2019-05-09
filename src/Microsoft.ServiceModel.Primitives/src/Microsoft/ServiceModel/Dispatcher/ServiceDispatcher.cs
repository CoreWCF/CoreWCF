using Microsoft.ServiceModel.Channels;
using Microsoft.ServiceModel.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.ServiceModel.Dispatcher
{
    internal class ServiceDispatcher : IServiceDispatcher
    {
        private EndpointDispatcherTable _endpointDispatcherTable;
        private IRequestReplyCorrelator _requestReplyCorrelator;

        public ServiceDispatcher(Uri baseAddress, Binding binding, EndpointDispatcherTable endpointDispatcherTable)
        {
            BaseAddress = baseAddress;
            Binding = binding;
            _endpointDispatcherTable = endpointDispatcherTable;
            // TODO: Maybe make lazy
            _requestReplyCorrelator = new RequestReplyCorrelator();
        }

        public Uri BaseAddress { get; }

        public Binding Binding { get; }

        public Task DispatchAsync(RequestContext request, IChannel channel, CancellationToken token)
        {
            bool dummy;
            var endpointDispatcher = _endpointDispatcherTable.Lookup(request.RequestMessage, out dummy);
            return DispatchAsyncCore(request, channel, endpointDispatcher, token);
        }

        private Task DispatchAsyncCore(RequestContext request, IChannel channel, EndpointDispatcher endpointDispatcher, CancellationToken token)
        {
            var dispatchRuntime = endpointDispatcher.DispatchRuntime;
            //EndpointDispatcher endpoint = dispatchRuntime.EndpointDispatcher;
            //bool releasedPump = false;

            ServiceChannel serviceChannel = null;
            var sessionIdleManager = channel.GetProperty<ServiceChannel.SessionIdleManager>();
            IChannelBinder binder = null;
            if (channel is IReplyChannel)
            {
                var rcbinder = channel.GetProperty<ReplyChannelBinder>();
                rcbinder.Init(channel as IReplyChannel, BaseAddress);
                binder = rcbinder;
            }
            else if (channel is IDuplexSessionChannel)
            {
                var dcbinder = channel.GetProperty<DuplexChannelBinder>();
                dcbinder.Init(channel as IDuplexSessionChannel, _requestReplyCorrelator, BaseAddress);
                binder = dcbinder;
            }

            serviceChannel = new ServiceChannel(
                binder,
                endpointDispatcher,
                Binding,
                sessionIdleManager.UseIfNeeded(binder, Binding.ReceiveTimeout));

            Message message = request.RequestMessage;
            DispatchOperationRuntime operation = dispatchRuntime.GetOperation(ref message);
            if (operation == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "No DispatchOperationRuntime found to process message.")));
            }

            // TODO: Wire in session open notification
            //if (shouldRejectMessageWithOnOpenActionHeader && message.Headers.Action == OperationDescription.SessionOpenedAction)
            //{
            //    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxNoEndpointMatchingAddressForConnectionOpeningMessage, message.Headers.Action, "Open")));
            //}

            // TODO: Session lifetime
            //if (operation.IsTerminating && requestInfo.HasSession)
            //{
            //    isChannelTerminated = true;
            //}

            // TODO: Fix up whatever semantics OperationContext places on a host being passed
            var currentOperationContext = new OperationContext(request, message, serviceChannel, /*host*/ null);

            currentOperationContext.EndpointDispatcher = endpointDispatcher;

            var existingInstanceContext = dispatchRuntime.InstanceContextProvider.GetExistingInstanceContext(request.RequestMessage, serviceChannel.Proxy as IContextChannel);
            // TODO: Investigate consequences of cleanThread parameter
            MessageRpc rpc = new MessageRpc(request, message, operation, serviceChannel, /*host*/ null,
                /*cleanThread*/ true, currentOperationContext, existingInstanceContext /*, eventTraceActivity*/);

            return operation.Parent.DispatchAsync(ref rpc, /*hasOperationContextBeenSet*/ false);
            // TODO : Fix error handling
            //catch (Exception e)
            //{
            //    if (Fx.IsFatal(e))
            //    {
            //        throw;
            //    }
            //    return HandleError(e, request, channel);
            //}
        }

    }
}
