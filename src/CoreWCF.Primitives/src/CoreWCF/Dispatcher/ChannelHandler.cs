using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.Runtime;
using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.Diagnostics;
using SessionIdleManager = CoreWCF.Channels.ServiceChannel.SessionIdleManager;
using System.Diagnostics;
using CoreWCF.Configuration;

namespace CoreWCF.Dispatcher
{
    internal class ChannelHandler : IServiceChannelDispatcher
    {
        public static readonly TimeSpan CloseAfterFaultTimeout = TimeSpan.FromSeconds(10);
        public const string MessageBufferPropertyName = "_RequestMessageBuffer_";
        ServiceChannel _channel;
        private ServiceDispatcher _serviceDispatcher;
        private MessageVersion _messageVersion;
        private bool _isManualAddressing;
        private IChannelBinder _binder;
        private ServiceThrottle _throttle;
        private bool _wasChannelThrottled;
        private DuplexChannelBinder _duplexBinder;
        private bool _hasSession;
        private bool _isConcurrent;
        private ErrorHandlingReceiver _receiver;
        private SessionIdleManager _idleManager;
        private SessionOpenNotification _sessionOpenNotification;
        private bool _needToCreateSessionOpenNotificationMessage;
        private RequestInfo _requestInfo;
        private bool _hasRegisterBeenCalled;
        private bool _isChannelTerminated;
        private bool _shouldRejectMessageWithOnOpenActionHeader;
        private RequestContext _replied;
        private bool _isCallback;

        internal ChannelHandler(MessageVersion messageVersion, IChannelBinder binder, ServiceThrottle throttle,
             ServiceDispatcher serviceDispatcher, bool wasChannelThrottled, SessionIdleManager idleManager)
        {
            ChannelDispatcher channelDispatcher = serviceDispatcher.ChannelDispatcher;
            _serviceDispatcher = serviceDispatcher;
            _messageVersion = messageVersion;
            _isManualAddressing = channelDispatcher.ManualAddressing;
            _binder = binder;
            _throttle = throttle;
            _wasChannelThrottled = wasChannelThrottled;
            _duplexBinder = binder as DuplexChannelBinder;
            _hasSession = binder.HasSession;
            _isConcurrent = ConcurrencyBehavior.IsConcurrent(channelDispatcher, _hasSession);

            //if (channelDispatcher.MaxPendingReceives > 1)
            //{
            //    // We need to preserve order if the ChannelHandler is not concurrent.
            //    this.binder = new MultipleReceiveBinder(
            //        this.binder,
            //        channelDispatcher.MaxPendingReceives,
            //        !this.isConcurrent);
            //}

            if (channelDispatcher.BufferedReceiveEnabled)
            {
                _binder = new BufferedReceiveBinder(_binder);
            }

            // TODO: Change this to ErrorHandlingDispatcher?
            _receiver = new ErrorHandlingReceiver(_binder, channelDispatcher);
            _idleManager = idleManager;
            //Fx.Assert((_idleManager != null) == (_binder.HasSession && channelDispatcher.DefaultCommunicationTimeouts.ReceiveTimeout != TimeSpan.MaxValue), "idle manager is present only when there is a session with a finite receive timeout");

             if (_binder.HasSession)
            {
                _sessionOpenNotification = _binder.Channel.GetProperty<SessionOpenNotification>();
                _needToCreateSessionOpenNotificationMessage = _sessionOpenNotification != null && _sessionOpenNotification.IsEnabled;
            }

            _requestInfo = new RequestInfo(this);

            // TODO: Wire up lifetime management in place of listener state
            //if (this.listener.State == CommunicationState.Opened)
            //{
            //    this.listener.ChannelDispatcher.Channels.IncrementActivityCount();
            //    this.incrementedActivityCountInConstructor = true;
            //}
        }

        internal bool HasRegisterBeenCalled
        {
            get { return _hasRegisterBeenCalled; }
        }

        internal InstanceContext InstanceContext
        {
            get { return (_channel != null) ? _channel.InstanceContext : null; }
        }

        internal ServiceThrottle InstanceContextServiceThrottle { get; set; }

        bool IsOpen
        {
            get { return _binder.Channel.State == CommunicationState.Opened; }
        }

        object ThisLock
        {
            get { return this; }
        }

        // Similar to HandleRequest on Desktop
        public async Task DispatchAsync(RequestContext request, CancellationToken token)
        {
            if (OperationContext.Current == null)
            {
                OperationContext.Current = new OperationContext((ServiceHostBase)null);
            }
            else
            {
                OperationContext.Current.Recycle(); 
            }

            if (HandleRequestAsReply(request))
            {
                return;
            }

            if (_isChannelTerminated)
            {
                await ReplyChannelTerminatedAsync(request);
                return;
            }

            if (_requestInfo.RequestContext != null)
            {
                Fx.Assert("ChannelHandler.HandleRequest: _requestInfo.RequestContext != null");
            }

            _requestInfo.RequestContext = request;
            await TryAcquireCallThrottleAsync(request);

            if (_requestInfo.ChannelHandlerOwnsCallThrottle)
            {
                Fx.Assert("ChannelHandler.HandleRequest: _requestInfo.ChannelHandlerOwnsCallThrottle");
            }

            _requestInfo.ChannelHandlerOwnsCallThrottle = true;
            if (! await TryRetrievingInstanceContextAsync(request))
            {
                //Would have replied and close the request.
                return;
            }

            _requestInfo.Channel.CompletedIOOperation();

            //Only acquire InstanceContext throttle if one doesnt already exist.
            await TryAcquireThrottleAsync(request, (_requestInfo.ExistingInstanceContext == null));
            if (_requestInfo.ChannelHandlerOwnsInstanceContextThrottle)
            {
                Fx.Assert("ChannelHandler.HandleRequest: _requestInfo.ChannelHandlerOwnsInstanceContextThrottle");
            }

            _requestInfo.ChannelHandlerOwnsInstanceContextThrottle = (_requestInfo.ExistingInstanceContext == null);

            await DispatchAsyncCore(request, true, OperationContext.Current);
        }

        private bool HandleRequestAsReply(RequestContext request)
        {
            if (_duplexBinder != null)
            {
                return _duplexBinder.HandleRequestAsReply(request.RequestMessage);
            }

            return false;
        }

        // Similar to DispatchAndReleasePump on Desktop
        internal async Task DispatchAsyncCore(RequestContext request, bool cleanThread, OperationContext currentOperationContext)
        {
            ServiceChannel channel = _requestInfo.Channel;
            EndpointDispatcher endpoint = _requestInfo.Endpoint;

            try
            {
                DispatchRuntime dispatchBehavior = _requestInfo.DispatchRuntime;

                if (channel == null || dispatchBehavior == null)
                {
                    Fx.Assert("System.ServiceModel.Dispatcher.ChannelHandler.Dispatch(): (channel == null || dispatchBehavior == null)");
                }

                MessageBuffer buffer = null;
                Message message = request.RequestMessage;
                DispatchOperationRuntime operation = dispatchBehavior.GetOperation(ref message);
                if (operation == null)
                {
                    Fx.Assert("ChannelHandler.Dispatch (operation == null)");
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException("No DispatchOperationRuntime found to process message."));
                }

                if (_shouldRejectMessageWithOnOpenActionHeader && message.Headers.Action == OperationDescription.SessionOpenedAction)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxNoEndpointMatchingAddressForConnectionOpeningMessage, message.Headers.Action, "Open")));
                }

                if (operation.IsTerminating && _hasSession)
                {
                    _isChannelTerminated = true;
                }

                bool hasOperationContextBeenSet;
                if (currentOperationContext != null)
                {
                    hasOperationContextBeenSet = true;
                    currentOperationContext.ReInit(request, message, channel);
                }
                else
                {
                    hasOperationContextBeenSet = false;
                    currentOperationContext = new OperationContext(request, message, channel, null);
                }

                if (currentOperationContext.EndpointDispatcher == null && _serviceDispatcher != null)
                {
                    currentOperationContext.EndpointDispatcher = endpoint;
                }

                MessageRpc rpc = new MessageRpc(request, message, operation, channel, /*host*/ null,
                    this, cleanThread, currentOperationContext, _requestInfo.ExistingInstanceContext);

                // passing responsibility for call throttle to MessageRpc
                // (MessageRpc implicitly owns this throttle once it's created)
                _requestInfo.ChannelHandlerOwnsCallThrottle = false;
                // explicitly passing responsibility for instance throttle to MessageRpc
                rpc.MessageRpcOwnsInstanceContextThrottle = _requestInfo.ChannelHandlerOwnsInstanceContextThrottle;
                _requestInfo.ChannelHandlerOwnsInstanceContextThrottle = false;

                // These need to happen before Dispatch but after accessing any ChannelHandler
                // state, because we go multi-threaded after this until we reacquire pump mutex.

                await operation.Parent.DispatchAsync(rpc, hasOperationContextBeenSet);
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }

                await HandleErrorAsync(e, request, channel);
            }
        }

        private async Task EnsureChannelAndEndpointAsync(RequestContext request)
        {
            _requestInfo.Channel = _channel;

            if (_requestInfo.Channel == null)
            {
                bool addressMatched;
                if (_hasSession)
                {
                    _requestInfo.Channel = GetSessionChannel(request.RequestMessage, out _requestInfo.Endpoint, out addressMatched);
                }
                else
                {
                    _requestInfo.Channel = GetDatagramChannel(request.RequestMessage, out _requestInfo.Endpoint, out addressMatched);
                }

                if (_requestInfo.Channel == null)
                {
                    // TODO: Enable UnknownMessageReceived handler
                    //this.host.RaiseUnknownMessageReceived(request.RequestMessage);
                    if (addressMatched)
                    {
                        await ReplyContractFilterDidNotMatchAsync(request);
                    }
                    else
                    {
                        await ReplyAddressFilterDidNotMatchAsync(request);
                    }
                }
            }
            else
            {
                _requestInfo.Endpoint = _requestInfo.Channel.EndpointDispatcher;

                //For sessionful contracts, the InstanceContext throttle is not copied over to the channel
                //as we create the channel before acquiring the lock
                if (InstanceContextServiceThrottle != null && _requestInfo.Channel.InstanceContextServiceThrottle == null)
                {
                    _requestInfo.Channel.InstanceContextServiceThrottle = InstanceContextServiceThrottle;
                }
            }

            _requestInfo.EndpointLookupDone = true;

            if (_requestInfo.Channel == null)
            {
                // SFx drops a message here
                TraceUtility.TraceDroppedMessage(request.RequestMessage, _requestInfo.Endpoint);
                await request.CloseAsync();
                return;
            }

            if (_requestInfo.Channel.HasSession || _isCallback)
            {
                _requestInfo.DispatchRuntime = _requestInfo.Channel.DispatchRuntime;
            }
            else
            {
                _requestInfo.DispatchRuntime = _requestInfo.Endpoint.DispatchRuntime;
            }
        }

        ServiceChannel GetDatagramChannel(Message message, out EndpointDispatcher endpoint, out bool addressMatched)
        {
            addressMatched = false;
            endpoint = GetEndpointDispatcher(message, out addressMatched);

            if (endpoint == null)
            {
                return null;
            }

            if (endpoint.DatagramChannel == null)
            {
                lock (_serviceDispatcher.ThisLock)
                {
                    if (endpoint.DatagramChannel == null)
                    {
                        endpoint.DatagramChannel = new ServiceChannel(_binder, endpoint, _serviceDispatcher.Binding, _idleManager);
                        InitializeServiceChannel(endpoint.DatagramChannel);
                    }
                }
            }

            return endpoint.DatagramChannel;
        }

        ServiceChannel GetSessionChannel(Message message, out EndpointDispatcher endpoint, out bool addressMatched)
        {
            addressMatched = false;

            if (_channel == null)
            {
                lock (ThisLock)
                {
                    if (_channel == null)
                    {
                        endpoint = GetEndpointDispatcher(message, out addressMatched);
                        if (endpoint != null)
                        {
                            _channel = new ServiceChannel(_binder, endpoint, _serviceDispatcher.Binding, _idleManager);
                            InitializeServiceChannel(_channel);
                        }
                    }
                }
            }

            if (_channel == null)
            {
                endpoint = null;
            }
            else
            {
                endpoint = _channel.EndpointDispatcher;
            }
            return _channel;
        }

        Task InitializeServiceChannel(ServiceChannel channel)
        {
            if (_wasChannelThrottled)
            {
                // Comment preserved from .NET Framework:
                // When the idle timeout was hit, the constructor of ServiceChannel will abort itself directly. So
                // the session throttle will not be released and thus lead to a service unavailablity.
                // Note that if the channel is already aborted, the next line "channel.ServiceThrottle = this.throttle;" will throw an exception,
                // so we are not going to do any more work inside this method. 
                // Ideally we should do a thorough refactoring work for this throttling issue. However, it's too risky. We should consider
                // this in a whole release.
                // Note that the "wasChannelThrottled" boolean will only be true if we aquired the session throttle. So we don't have to check HasSession
                // again here.
                if (channel.Aborted && _throttle != null)
                {
                    // This line will release the "session" throttle.
                    _throttle.DeactivateChannel();
                }

                channel.ServiceThrottle = _throttle;
            }

            if (InstanceContextServiceThrottle != null)
            {
                channel.InstanceContextServiceThrottle = InstanceContextServiceThrottle;
            }

            ClientRuntime clientRuntime = channel.ClientRuntime;
            if (clientRuntime != null)
            {
                Type contractType = clientRuntime.ContractClientType;
                Type callbackType = clientRuntime.CallbackClientType;

                if (contractType != null)
                {
                    channel.Proxy = ServiceChannelFactory.CreateProxy(contractType, callbackType, MessageDirection.Output, channel);
                }
            }

            if (_serviceDispatcher != null)
            {
                _serviceDispatcher.ChannelDispatcher.InitializeChannel((IClientChannel)channel.Proxy);
            }

            return ((IChannel)channel).OpenAsync();
        }


        void ProvideFault(Exception e, ref ErrorHandlerFaultInfo faultInfo)
        {
            if (_serviceDispatcher != null)
            {
                _serviceDispatcher.ChannelDispatcher.ProvideFault(e, _requestInfo.Channel == null ? _binder.Channel.GetProperty<FaultConverter>() : _requestInfo.Channel.GetProperty<FaultConverter>(), ref faultInfo);
            }
            // No client yet
            //else if (_channel != null)
            //{
            //    DispatchRuntime dispatchBehavior = _channel.ClientRuntime.CallbackDispatchRuntime;
            //    dispatchBehavior.ChannelDispatcher.ProvideFault(e, this.channel.GetProperty<FaultConverter>(), ref faultInfo);
            //}
        }

        internal bool HandleError(Exception e)
        {
            ErrorHandlerFaultInfo dummy = new ErrorHandlerFaultInfo();
            return HandleError(e, ref dummy);
        }

        bool HandleError(Exception e, ref ErrorHandlerFaultInfo faultInfo)
        {
            if (e == null)
            {
                Fx.Assert(SR.SFxNonExceptionThrown);
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SFxNonExceptionThrown));
            }
            if (_serviceDispatcher != null)
            {
                return _serviceDispatcher.ChannelDispatcher.HandleError(e, ref faultInfo);
            }
            // No client yet.
            //else if (this.channel != null)
            //{
            //    return this.channel.ClientRuntime.CallbackDispatchRuntime.ChannelDispatcher.HandleError(e, ref faultInfo);
            //}
            else
            {
                return false;
            }
        }

        private Task HandleErrorAsync(Exception e, RequestContext request, ServiceChannel channel)
        {
            var faultInfo = new ErrorHandlerFaultInfo(_messageVersion.Addressing.DefaultFaultAction);
            return ProvideFaultAndReplyFailureAsync(request, e, faultInfo);
        }

        Task ReplyAddressFilterDidNotMatchAsync(RequestContext request)
        {
            FaultCode code = FaultCode.CreateSenderFaultCode(AddressingStrings.DestinationUnreachable,
                _messageVersion.Addressing.Namespace);
            string reason = SR.Format(SR.SFxNoEndpointMatchingAddress, request.RequestMessage.Headers.To);

            return ReplyFailureAsync(request, code, reason);
        }

        Task ReplyContractFilterDidNotMatchAsync(RequestContext request)
        {
            // By default, the contract filter is just a filter over the set of initiating actions in 
            // the contract, so we do error messages accordingly
            AddressingVersion addressingVersion = _messageVersion.Addressing;
            if (addressingVersion != AddressingVersion.None && request.RequestMessage.Headers.Action == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new MessageHeaderException(
                    SR.Format(SR.SFxMissingActionHeader, addressingVersion.Namespace), AddressingStrings.Action, addressingVersion.Namespace));
            }
            else
            {
                // some of this code is duplicated in DispatchRuntime.UnhandledActionInvoker
                // ideally both places would use FaultConverter and ActionNotSupportedException
                FaultCode code = FaultCode.CreateSenderFaultCode(AddressingStrings.ActionNotSupported,
                    _messageVersion.Addressing.Namespace);
                string reason = SR.Format(SR.SFxNoEndpointMatchingContract, request.RequestMessage.Headers.Action);
                return ReplyFailureAsync(request, code, reason, _messageVersion.Addressing.FaultAction);
            }
        }

        private Task ReplyChannelTerminatedAsync(RequestContext request)
        {
            FaultCode code = FaultCode.CreateSenderFaultCode(FaultCodeConstants.Codes.SessionTerminated,
                FaultCodeConstants.Namespaces.NetDispatch);
            string reason = SR.SFxChannelTerminated0;
            string action = FaultCodeConstants.Actions.NetDispatcher;
            Message fault = Message.CreateMessage(_messageVersion, code, reason, action);
            return ReplyFailureAsync(request, fault, action, reason, code);
        }

        Task ReplyFailureAsync(RequestContext request, FaultCode code, string reason)
        {
            string action = _messageVersion.Addressing.DefaultFaultAction;
            return ReplyFailureAsync(request, code, reason, action);
        }

        Task ReplyFailureAsync(RequestContext request, FaultCode code, string reason, string action)
        {
            Message fault = Message.CreateMessage(_messageVersion, code, reason, action);
            return ReplyFailureAsync(request, fault, action, reason, code);
        }

        private async Task ReplyFailureAsync(RequestContext request, Message fault, string action, string reason, FaultCode code)
        {
            FaultException exception = new FaultException(reason, code);
            ErrorBehavior.ThrowAndCatch(exception);
            ErrorHandlerFaultInfo faultInfo = new ErrorHandlerFaultInfo(action);
            faultInfo.Fault = fault;
            bool replied, replySentAsync;
            faultInfo = await ProvideFaultAndReplyFailureAsync(request, exception, faultInfo);
            HandleError(exception, ref faultInfo);
        }

        private async Task<ErrorHandlerFaultInfo> ProvideFaultAndReplyFailureAsync(RequestContext request, Exception exception, ErrorHandlerFaultInfo faultInfo)
        {
            bool requestMessageIsFault = false;
            try
            {
                requestMessageIsFault = request.RequestMessage.IsFault;
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }
                // swallow it
            }

            bool enableFaults = false;
            if (_serviceDispatcher != null)
            {
                enableFaults = _serviceDispatcher.ChannelDispatcher.EnableFaults;
            }
            // No client yet.
            //else if (this._channel != null && this._channel.IsClient)
            //{
            //    enableFaults = this._channel.ClientRuntime.EnableFaults;
            //}

            if ((!requestMessageIsFault) && enableFaults)
            {
                ProvideFault(exception, ref faultInfo);
                if (faultInfo.Fault != null)
                {
                    Message reply = faultInfo.Fault;
                    try
                    {
                        try
                        {
                            if (PrepareReply(request, reply))
                            {
                                await request.ReplyAsync(reply);
                            }
                        }
                        finally
                        {
                            reply.Close();
                        }
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
            }

            return faultInfo;
        }

        /// <summary>
        /// Prepares a reply
        /// </summary>
        /// <param name="request">The request context to prepare</param>
        /// <param name="reply">The reply to prepare</param>
        /// <returns>True if channel is open and prepared reply should be sent; otherwise false.</returns>
        bool PrepareReply(RequestContext request, Message reply)
        {
            // Ensure we only reply once (we may hit the same error multiple times)
            if (_replied == request)
            {
                return false;
            }

            _replied = request;

            bool canSendReply = true;

            Message requestMessage = null;
            try
            {
                requestMessage = request.RequestMessage;
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }
                // swallow it
            }
            if (!object.ReferenceEquals(requestMessage, null))
            {
                UniqueId requestID = null;
                try
                {
                    requestID = requestMessage.Headers.MessageId;
                }
                catch (MessageHeaderException)
                {
                    // swallow it - we don't need to correlate the reply if the MessageId header is bad
                }
                if (!object.ReferenceEquals(requestID, null) && !_isManualAddressing)
                {
                    RequestReplyCorrelator.PrepareReply(reply, requestID);
                }
                if (!_hasSession && !_isManualAddressing)
                {
                    try
                    {
                        canSendReply = RequestReplyCorrelator.AddressReply(reply, requestMessage);
                    }
                    catch (MessageHeaderException)
                    {
                        // swallow it - we don't need to address the reply if the FaultTo header is bad
                    }
                }
            }

            // ObjectDisposeException can happen
            // if the channel is closed in a different
            // thread. 99% this check will avoid false
            // exceptions.
            return IsOpen && canSendReply;
        }

        EndpointDispatcher GetEndpointDispatcher(Message message, out bool addressMatched)
        {
            return _serviceDispatcher.Endpoints.Lookup(message, out addressMatched);
        }

        Task TryAcquireThrottleAsync(RequestContext request, bool acquireInstanceContextThrottle)
        {
            ServiceThrottle throttle = _throttle;
            if ((throttle != null) && (throttle.IsActive))
            {
                return throttle.AcquireInstanceContextAndDynamicAsync(this, acquireInstanceContextThrottle);
            }

            return Task.CompletedTask;
        }

        Task TryAcquireCallThrottleAsync(RequestContext request)
        {
            ServiceThrottle throttle = _throttle;
            if ((throttle != null) && (throttle.IsActive))
            {
                return throttle.AcquireCallAsync();
            }

            return Task.CompletedTask;
        }

        private async Task<bool> TryRetrievingInstanceContextAsync(RequestContext request)
        {
            try
            {
                return await TryRetrievingInstanceContextCoreAsync(request);
            }
            catch (Exception ex)
            {
                if (Fx.IsFatal(ex))
                {
                    throw;
                }

                DiagnosticUtility.TraceHandledException(ex, TraceEventType.Error);

                try
                {
                    await request.CloseAsync();
                }
                catch (Exception e)
                {
                    if (Fx.IsFatal(e))
                    {
                        throw;
                    }

                    request.Abort();
                }

                return false;
            }
        }

        //Return: False denotes failure, Caller should discard the request.
        //      : True denotes operation is sucessful.
        private async Task<bool> TryRetrievingInstanceContextCoreAsync(RequestContext request)
        {
            bool releasePump = true;
            try
            {
                if (!_requestInfo.EndpointLookupDone)
                {
                    EnsureChannelAndEndpointAsync(request);
                }

                if (_requestInfo.Channel == null)
                {
                    return false;
                }

                if (_requestInfo.DispatchRuntime != null)
                {
                    IContextChannel transparentProxy = _requestInfo.Channel.Proxy as IContextChannel;
                    try
                    {
                        _requestInfo.ExistingInstanceContext = _requestInfo.DispatchRuntime.InstanceContextProvider.GetExistingInstanceContext(request.RequestMessage, transparentProxy);
                        releasePump = false;
                    }
                    catch (Exception e)
                    {
                        if (Fx.IsFatal(e))
                        {
                            throw;
                        }
                        _requestInfo.Channel = null;
                        await HandleErrorAsync(e, request, _channel);
                        return false;
                    }
                }
                else
                {
                    // This can happen if we are pumping for an async client,
                    // and we receive a bogus reply.  In that case, there is no
                    // DispatchRuntime, because we are only expecting replies.
                    //
                    // One possible fix for this would be in DuplexChannelBinder
                    // to drop all messages with a RelatesTo that do not match a
                    // pending request.
                    //
                    // However, that would not fix:
                    // (a) we could get a valid request message with a
                    // RelatesTo that we should try to process.
                    // (b) we could get a reply message that does not have
                    // a RelatesTo.
                    //
                    // So we do the null check here.
                    //
                    // SFx drops a message here
                    await request.CloseAsync();
                    return false;
                }
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }

                await HandleErrorAsync(e, request, _channel);

                return false;
            }

            return true;
        }


        struct RequestInfo
        {
            public EndpointDispatcher Endpoint;
            public InstanceContext ExistingInstanceContext;
            public ServiceChannel Channel;
            public bool EndpointLookupDone;
            public DispatchRuntime DispatchRuntime;
            public RequestContext RequestContext;
            public ChannelHandler ChannelHandler;
            public bool ChannelHandlerOwnsCallThrottle; // if true, we are responsible for call throttle
            public bool ChannelHandlerOwnsInstanceContextThrottle; // if true, we are responsible for instance/dynamic throttle

            public RequestInfo(ChannelHandler channelHandler)
            {
                Endpoint = null;
                ExistingInstanceContext = null;
                Channel = null;
                EndpointLookupDone = false;
                DispatchRuntime = null;
                RequestContext = null;
                ChannelHandler = channelHandler;
                ChannelHandlerOwnsCallThrottle = false;
                ChannelHandlerOwnsInstanceContextThrottle = false;
            }

            public void Cleanup()
            {
                if (ChannelHandlerOwnsInstanceContextThrottle)
                {
                    ChannelHandler._throttle?.DeactivateInstanceContext();
                    ChannelHandlerOwnsInstanceContextThrottle = false;
                }

                Endpoint = null;
                ExistingInstanceContext = null;
                Channel = null;
                EndpointLookupDone = false;
                RequestContext = null;
                if (ChannelHandlerOwnsCallThrottle)
                {
                    ChannelHandler._throttle?.DeactivateCall();
                    ChannelHandlerOwnsCallThrottle = false;
                }
            }
        }
    }
}