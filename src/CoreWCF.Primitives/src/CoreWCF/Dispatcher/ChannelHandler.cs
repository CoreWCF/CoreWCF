﻿using System;
using System.Globalization;
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
        private ServiceChannel _channel;
        private bool _doneReceiving;
        private ServiceDispatcher _serviceDispatcher;
        private MessageVersion _messageVersion;
        private bool _isManualAddressing;
        private IChannelBinder _binder;
        private ServiceThrottle _throttle;
        private bool _wasChannelThrottled;
        private DuplexChannelBinder _duplexBinder;
        private readonly ServiceHostBase _host;
        private bool _hasSession;
        private bool _isConcurrent;
        private SessionIdleManager _idleManager;
        private SessionOpenNotification _sessionOpenNotification;
        private bool _needToCreateSessionOpenNotificationMessage;
        //private RequestInfo _requestInfo;
        private bool _isChannelTerminated;
        private bool _shouldRejectMessageWithOnOpenActionHeader;
        private RequestContext _replied;
        private bool _isCallback;
        private bool _incrementedActivityCountInConstructor;
        private ResettableAsyncWaitable _resettableAsyncWaitable;
        private bool _openCalled;

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
            _host = channelDispatcher.Host;
            _duplexBinder = binder as DuplexChannelBinder;
            _hasSession = binder.HasSession;
            _isConcurrent = ConcurrencyBehavior.IsConcurrent(channelDispatcher, _hasSession);

            // TODO: Work out if MultipleReceiveBinder is necessary
            //if (channelDispatcher.MaxPendingReceives > 1)
            //{
            //    // We need to preserve order if the ChannelHandler is not concurrent.
            //    this.binder = new MultipleReceiveBinder(
            //        this.binder,
            //        channelDispatcher.MaxPendingReceives,
            //        !this.isConcurrent);
            //}

            _idleManager = idleManager;

            if (_binder.HasSession)
            {
                _sessionOpenNotification = _binder.Channel.GetProperty<SessionOpenNotification>();
                _needToCreateSessionOpenNotificationMessage = _sessionOpenNotification != null && _sessionOpenNotification.IsEnabled;
            }

            //_requestInfo = new RequestInfo(this);

            // TODO: Wire up lifetime management in place of listener state
            //if (this.listener.State == CommunicationState.Opened)
            //{
            _serviceDispatcher.ChannelDispatcher.Channels.IncrementActivityCount();
            _incrementedActivityCountInConstructor = true;
            //}
            _resettableAsyncWaitable = new ResettableAsyncWaitable();
        }

        internal IServiceChannelDispatcher GetDispatcher()
        {
            return _binder;
        }

        internal async Task OpenAsync()
        {
            _binder.SetNextDispatcher(this);
            Exception exception = null;
            try
            {
                await _binder.Channel.OpenAsync();
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }
                exception = e;
            }

            if (exception != null)
            {
                //if (DiagnosticUtility.ShouldTraceWarning)
                //{
                //    TraceUtility.TraceEvent(System.Diagnostics.TraceEventType.Warning,
                //        TraceCode.FailedToOpenIncomingChannel,
                //        SR.GetString(SR.TraceCodeFailedToOpenIncomingChannel));
                //}
                if ((_throttle != null) && _hasSession)
                {
                    _throttle.DeactivateChannel();
                }

                bool errorHandled = HandleError(exception);

                //if (this.incrementedActivityCountInConstructor)
                //{
                //    this.listener.ChannelDispatcher.Channels.DecrementActivityCount();
                //}

                if (!errorHandled)
                {
                    _binder.Channel.Abort();
                }
            }
            else
            {
                ReleasePump();
                _shouldRejectMessageWithOnOpenActionHeader = !_needToCreateSessionOpenNotificationMessage;
                if (_needToCreateSessionOpenNotificationMessage)
                {
                    _needToCreateSessionOpenNotificationMessage = false;
                    var requestContext = GetSessionOpenNotificationRequestContext();
                    await HandleReceiveCompleteAsync(requestContext);
                    HandleRequestAsync(requestContext);
                }
                ReleasePump();
            }

            _openCalled = true;
        }

        internal bool HasRegisterBeenCalled { get; set; }

        internal InstanceContext InstanceContext
        {
            get { return (_channel != null) ? _channel.InstanceContext : null; }
        }

        internal ServiceThrottle InstanceContextServiceThrottle { get; set; }

        bool IsOpen
        {
            get { return _binder.Channel.State == CommunicationState.Opened; }
        }

        EndpointAddress LocalAddress
        {
            get
            {
                if (_binder != null)
                {
                    IInputChannel input = _binder.Channel as IInputChannel;
                    if (input != null)
                    {
                        return input.LocalAddress;
                    }

                    IReplyChannel reply = _binder.Channel as IReplyChannel;
                    if (reply != null)
                    {
                        return reply.LocalAddress;
                    }
                }

                return null;
            }
        }

        object ThisLock
        {
            get { return this; }
        }

        public async Task CloseBinderAsync()
        {
            try
            {
                await _binder.Channel.CloseAsync();
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

        public void EnsureReceive()
        {
            _resettableAsyncWaitable.Set();
        }

        public Task DispatchAsync(Message message)
        {
            var requestContext = _binder.CreateRequestContext(message);
            return DispatchAsync(requestContext);
        }

        public async Task DispatchAsync(RequestContext requestContext)
        {
            if (!_openCalled)
            {
                throw TraceUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.CommunicationObjectCannotBeUsed, GetType().ToString(), CommunicationState.Created)), Guid.Empty, this);
            }

            await TryAcquirePumpAsync();
            await HandleReceiveCompleteAsync(requestContext);
            if (requestContext == null)
            {
                return;
            }

            // Don't await handling the request to allow caller to start receving the next incoming message
            HandleRequestAsync(requestContext);
        }

        RequestContext GetSessionOpenNotificationRequestContext()
        {
            Fx.Assert(_sessionOpenNotification != null, "this.sessionOpenNotification should not be null.");
            Message message = Message.CreateMessage(_binder.Channel.GetProperty<MessageVersion>(), OperationDescription.SessionOpenedAction);
            Fx.Assert(LocalAddress != null, "this.LocalAddress should not be null.");
            message.Headers.To = LocalAddress.Uri;
            _sessionOpenNotification.UpdateMessageProperties(message.Properties);
            return _binder.CreateRequestContext(message);
        }

        private async void HandleRequestAsync(RequestContext request)
        {
            if (request == null)
            {
                // channel EOF, stop receiving
                return;
            }

            var requestInfo = new RequestInfo(this);

            //ServiceModelActivity activity = DiagnosticUtility.ShouldUseActivity ? TraceUtility.ExtractActivity(request) : null;

            //using (ServiceModelActivity.BoundOperation(activity))
            //{
            if (HandleRequestAsReply(request))
            {
                ReleasePump();
                return;
            }

            if (_isChannelTerminated)
            {
                ReleasePump();
                await ReplyChannelTerminatedAsync(request, requestInfo);
                return;
            }

            requestInfo.RequestContext = request;

            await TryAcquireCallThrottleAsync(request);

            Fx.Assert(!requestInfo.ChannelHandlerOwnsCallThrottle, "ChannelHandler.HandleRequest: this.requestInfo.ChannelHandlerOwnsCallThrottle");

            requestInfo.ChannelHandlerOwnsCallThrottle = true;

            if (!await TryRetrievingInstanceContextAsync(request, requestInfo))
            {
                //Would have replied and close the request.
                return;
            }

            requestInfo.Channel.CompletedIOOperation();

            //Only acquire InstanceContext throttle if one doesnt already exist.
            await TryAcquireThrottleAsync(request, requestInfo.ExistingInstanceContext == null);
            Fx.Assert(!requestInfo.ChannelHandlerOwnsInstanceContextThrottle, "ChannelHandler.HandleRequest: this.requestInfo.ChannelHandlerOwnsInstanceContextThrottle");
            requestInfo.ChannelHandlerOwnsInstanceContextThrottle = (requestInfo.ExistingInstanceContext == null);

            await DispatchAndReleasePumpAsync(request, true, requestInfo);
            //}
        }

        private async Task DispatchAndReleasePumpAsync(RequestContext request, bool cleanThread, RequestInfo requestInfo)
        {
            OperationContext currentOperationContext = null;
            ServiceChannel channel = requestInfo.Channel;
            EndpointDispatcher endpoint = requestInfo.Endpoint;
            bool releasedPump = false;

            try
            {
                DispatchRuntime dispatchBehavior = requestInfo.DispatchRuntime;

                if (channel == null || dispatchBehavior == null)
                {
                    Fx.Assert("System.ServiceModel.Dispatcher.ChannelHandler.Dispatch(): (channel == null || dispatchBehavior == null)");
                    return;
                }

                MessageBuffer buffer = null;
                Message message;

                //EventTraceActivity eventTraceActivity = TraceDispatchMessageStart(request.RequestMessage);
                message = request.RequestMessage;

                DispatchOperationRuntime operation = dispatchBehavior.GetOperation(ref message);
                if (operation == null)
                {
                    Fx.Assert("ChannelHandler.Dispatch (operation == null)");
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(String.Format(CultureInfo.InvariantCulture, "No DispatchOperationRuntime found to process message.")));
                }

                if (_shouldRejectMessageWithOnOpenActionHeader && message.Headers.Action == OperationDescription.SessionOpenedAction)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxNoEndpointMatchingAddressForConnectionOpeningMessage, message.Headers.Action, "Open")));
                }

                //if (MessageLogger.LoggingEnabled)
                //{
                //    MessageLogger.LogMessage(ref message, (operation.IsOneWay ? MessageLoggingSource.ServiceLevelReceiveDatagram : MessageLoggingSource.ServiceLevelReceiveRequest) | MessageLoggingSource.LastChance);
                //}

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
                    currentOperationContext = new OperationContext(request, message, channel, _host);
                }

                if (dispatchBehavior.PreserveMessage)
                {
                    currentOperationContext.IncomingMessageProperties.Add(MessageBufferPropertyName, buffer);
                }

                if (currentOperationContext.EndpointDispatcher == null && _serviceDispatcher != null)
                {
                    currentOperationContext.EndpointDispatcher = endpoint;
                }

                var rpc = new MessageRpc(request, message, operation, channel, _host,
                    this, cleanThread, currentOperationContext, requestInfo.ExistingInstanceContext);

                //TraceUtility.MessageFlowAtMessageReceived(message, currentOperationContext, eventTraceActivity, true);

                // passing responsibility for call throttle to MessageRpc
                // (MessageRpc implicitly owns this throttle once it's created)
                requestInfo.ChannelHandlerOwnsCallThrottle = false;
                // explicitly passing responsibility for instance throttle to MessageRpc
                rpc.MessageRpcOwnsInstanceContextThrottle = requestInfo.ChannelHandlerOwnsInstanceContextThrottle;
                requestInfo.ChannelHandlerOwnsInstanceContextThrottle = false;

                // These need to happen before Dispatch but after accessing any ChannelHandler
                // state, because we go multi-threaded after this
                ReleasePump();
                releasedPump = true;

                await operation.Parent.DispatchAsync(rpc, hasOperationContextBeenSet);
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }

                await HandleErrorAsync(e, request, requestInfo, channel);
            }
            finally
            {
                if (!releasedPump)
                {
                    ReleasePump();
                }
            }
        }

        private async Task HandleReceiveCompleteAsync(RequestContext request)
        {
            try
            {
                if (_channel != null)
                {
                    _channel.HandleReceiveComplete(request);
                }
                else
                {
                    if (request == null && _hasSession)
                    {
                        bool close;
                        lock (ThisLock)
                        {
                            close = !_doneReceiving;
                            _doneReceiving = true;
                        }

                        if (close)
                        {
                            await CloseBinderAsync();

                            if (_idleManager != null)
                            {
                                _idleManager.CancelTimer();
                            }

                            ServiceThrottle throttle = _throttle;
                            if (throttle != null)
                            {
                                throttle.DeactivateChannel();
                            }
                        }
                    }
                }
            }
            finally
            {
                if ((request == null) && _incrementedActivityCountInConstructor)
                {
                    _serviceDispatcher.ChannelDispatcher.Channels.DecrementActivityCount();
                }
            }
        }

        private bool HandleRequestAsReply(RequestContext request)
        {
            if (_duplexBinder != null)
            {
                return _duplexBinder.HandleRequestAsReply(request.RequestMessage);
            }

            return false;
        }

        private async Task EnsureChannelAndEndpointAsync(RequestContext request, RequestInfo requestInfo)
        {
            requestInfo.Channel = _channel;

            if (requestInfo.Channel == null)
            {
                bool addressMatched;
                if (_hasSession)
                {
                    requestInfo.Channel = GetSessionChannel(request.RequestMessage, out requestInfo.Endpoint, out addressMatched);
                }
                else
                {
                    requestInfo.Channel = GetDatagramChannel(request.RequestMessage, out requestInfo.Endpoint, out addressMatched);
                }

                if (requestInfo.Channel == null)
                {
                    // TODO: Enable UnknownMessageReceived handler
                    //this.host.RaiseUnknownMessageReceived(request.RequestMessage);
                    if (addressMatched)
                    {
                        await ReplyContractFilterDidNotMatchAsync(request, requestInfo);
                    }
                    else
                    {
                        await ReplyAddressFilterDidNotMatchAsync(request, requestInfo);
                    }
                }
            }
            else
            {
                requestInfo.Endpoint = requestInfo.Channel.EndpointDispatcher;

                //For sessionful contracts, the InstanceContext throttle is not copied over to the channel
                //as we create the channel before acquiring the lock
                if (InstanceContextServiceThrottle != null && requestInfo.Channel.InstanceContextServiceThrottle == null)
                {
                    requestInfo.Channel.InstanceContextServiceThrottle = InstanceContextServiceThrottle;
                }
            }

            requestInfo.EndpointLookupDone = true;

            if (requestInfo.Channel == null)
            {
                // SFx drops a message here
                TraceUtility.TraceDroppedMessage(request.RequestMessage, requestInfo.Endpoint);
                await request.CloseAsync();
                return;
            }

            if (requestInfo.Channel.HasSession || _isCallback)
            {
                requestInfo.DispatchRuntime = requestInfo.Channel.DispatchRuntime;
            }
            else
            {
                requestInfo.DispatchRuntime = requestInfo.Endpoint.DispatchRuntime;
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
                        endpoint.DatagramChannel = new ServiceChannel(_binder, endpoint, _serviceDispatcher, 
                            _idleManager.UseIfNeeded(_binder, _serviceDispatcher.Binding.ReceiveTimeout));
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
                            _channel = new ServiceChannel(_binder, endpoint, _serviceDispatcher, 
                                _idleManager.UseIfNeeded(_binder, _serviceDispatcher.Binding.ReceiveTimeout));
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


        void ProvideFault(Exception e, RequestInfo requestInfo, ref ErrorHandlerFaultInfo faultInfo)
        {
            if (_serviceDispatcher != null)
            {
                _serviceDispatcher.ChannelDispatcher.ProvideFault(e, requestInfo.Channel == null ? _binder.Channel.GetProperty<FaultConverter>() : requestInfo.Channel.GetProperty<FaultConverter>(), ref faultInfo);
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

        private Task HandleErrorAsync(Exception e, RequestContext request, RequestInfo requestInfo, ServiceChannel channel)
        {
            var faultInfo = new ErrorHandlerFaultInfo(_messageVersion.Addressing.DefaultFaultAction);
            return ProvideFaultAndReplyFailureAsync(request, requestInfo, e, faultInfo);
        }

        Task ReplyAddressFilterDidNotMatchAsync(RequestContext request, RequestInfo requestInfo)
        {
            FaultCode code = FaultCode.CreateSenderFaultCode(AddressingStrings.DestinationUnreachable,
                _messageVersion.Addressing.Namespace);
            string reason = SR.Format(SR.SFxNoEndpointMatchingAddress, request.RequestMessage.Headers.To);

            return ReplyFailureAsync(request, requestInfo, code, reason);
        }

        Task ReplyContractFilterDidNotMatchAsync(RequestContext request, RequestInfo requestInfo)
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
                return ReplyFailureAsync(request, requestInfo, code, reason, _messageVersion.Addressing.FaultAction);
            }
        }

        private Task ReplyChannelTerminatedAsync(RequestContext request, RequestInfo requestInfo)
        {
            FaultCode code = FaultCode.CreateSenderFaultCode(FaultCodeConstants.Codes.SessionTerminated,
                FaultCodeConstants.Namespaces.NetDispatch);
            string reason = SR.SFxChannelTerminated0;
            string action = FaultCodeConstants.Actions.NetDispatcher;
            Message fault = Message.CreateMessage(_messageVersion, code, reason, action);
            return ReplyFailureAsync(request, requestInfo, fault, action, reason, code);
        }

        Task ReplyFailureAsync(RequestContext request, RequestInfo requestInfo, FaultCode code, string reason)
        {
            string action = _messageVersion.Addressing.DefaultFaultAction;
            return ReplyFailureAsync(request, requestInfo, code, reason, action);
        }

        Task ReplyFailureAsync(RequestContext request, RequestInfo requestInfo, FaultCode code, string reason, string action)
        {
            Message fault = Message.CreateMessage(_messageVersion, code, reason, action);
            return ReplyFailureAsync(request, requestInfo, fault, action, reason, code);
        }

        private async Task ReplyFailureAsync(RequestContext request, RequestInfo requestInfo, Message fault, string action, string reason, FaultCode code)
        {
            FaultException exception = new FaultException(reason, code);
            ErrorBehavior.ThrowAndCatch(exception);
            ErrorHandlerFaultInfo faultInfo = new ErrorHandlerFaultInfo(action);
            faultInfo.Fault = fault;
            faultInfo = await ProvideFaultAndReplyFailureAsync(request, requestInfo, exception, faultInfo);
            HandleError(exception, ref faultInfo);
        }

        private async Task<ErrorHandlerFaultInfo> ProvideFaultAndReplyFailureAsync(RequestContext request, RequestInfo requestInfo, Exception exception, ErrorHandlerFaultInfo faultInfo)
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
                ProvideFault(exception, requestInfo, ref faultInfo);
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

        private async Task<bool> TryRetrievingInstanceContextAsync(RequestContext request, RequestInfo requestInfo)
        {
            try
            {
                return await TryRetrievingInstanceContextCoreAsync(request, requestInfo);
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
        private async Task<bool> TryRetrievingInstanceContextCoreAsync(RequestContext request, RequestInfo requestInfo)
        {
            bool releasePump = true;
            try
            {
                if (!requestInfo.EndpointLookupDone)
                {
                    await EnsureChannelAndEndpointAsync(request, requestInfo);
                }

                if (requestInfo.Channel == null)
                {
                    return false;
                }

                if (requestInfo.DispatchRuntime != null)
                {
                    IContextChannel transparentProxy = requestInfo.Channel.Proxy as IContextChannel;
                    try
                    {
                        requestInfo.ExistingInstanceContext = requestInfo.DispatchRuntime.InstanceContextProvider.GetExistingInstanceContext(request.RequestMessage, transparentProxy);
                        releasePump = false;
                    }
                    catch (Exception e)
                    {
                        if (Fx.IsFatal(e))
                        {
                            throw;
                        }
                        requestInfo.Channel = null;
                        await HandleErrorAsync(e, request, requestInfo, _channel);
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

                await HandleErrorAsync(e, request, requestInfo, _channel);

                return false;
            }
            finally
            {
                if (releasePump)
                {
                    ReleasePump();
                }
            }

            return true;
        }

        private void ReleasePump()
        {
            if (_isConcurrent)
            {
                _resettableAsyncWaitable.Set();
            }
        }

        private async Task TryAcquirePumpAsync()
        {
            if (_isConcurrent)
            {
                await _resettableAsyncWaitable;
                _resettableAsyncWaitable.Reset();
            }
        }

        // TODO: Revert back to struct or pool objects.
        internal class RequestInfo
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