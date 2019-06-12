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

namespace CoreWCF.Dispatcher
{
    internal class ChannelHandler
    {
        public static readonly TimeSpan CloseAfterFaultTimeout = TimeSpan.FromSeconds(10);
        public const string MessageBufferPropertyName = "_RequestMessageBuffer_";

        readonly IChannelBinder binder;
        readonly DuplexChannelBinder duplexBinder;
        readonly ServiceHostBase host;
        readonly bool incrementedActivityCountInConstructor;
        readonly bool isCallback;
        readonly ListenerHandler listener;
        //readonly ServiceThrottle throttle;
        //readonly bool wasChannelThrottled;
        readonly ServiceChannel.SessionIdleManager idleManager;
        readonly bool sendAsynchronously;

        //static AsyncCallback onAsyncReplyComplete = Fx.ThunkCallback(new AsyncCallback(ChannelHandler.OnAsyncReplyComplete));
        //static AsyncCallback onAsyncReceiveComplete = Fx.ThunkCallback(new AsyncCallback(ChannelHandler.OnAsyncReceiveComplete));
        //static Action<object> onContinueAsyncReceive = OnContinueAsyncReceive;
        //static Action<object> onStartSyncMessagePump = OnStartSyncMessagePump;
        //static Action<object> onStartAsyncMessagePump = OnStartAsyncMessagePump;
        //static Action<object> onStartSingleTransactedBatch = OnStartSingleTransactedBatch;
        static Action<object> openAndEnsurePump = OpenAndEnsurePump;

        RequestInfo requestInfo;
        ServiceChannel channel;
        bool doneReceiving;
        bool hasRegisterBeenCalled;
        bool hasSession;
        int isPumpAcquired;
        bool isChannelTerminated;
        bool isConcurrent;
        bool isManualAddressing;
        MessageVersion messageVersion;
        ErrorHandlingReceiver receiver;
        bool receiveSynchronously;
        //bool receiveWithTransaction;
        RequestContext replied;
        //WrappedTransaction acceptTransaction;
        //ServiceThrottle instanceContextThrottle;
        //SharedTransactedBatchContext sharedTransactedBatchContext;
        //TransactedBatchContext transactedBatchContext;
        //bool isMainTransactedBatchHandler;
        //EventTraceActivity eventTraceActivity;
        SessionOpenNotification sessionOpenNotification;
        bool needToCreateSessionOpenNotificationMessage;
        bool shouldRejectMessageWithOnOpenActionHeader;

        internal ChannelHandler(MessageVersion messageVersion, IChannelBinder binder, ServiceChannel channel)
        {
            ClientRuntime clientRuntime = channel.ClientRuntime;

            this.messageVersion = messageVersion;
            isManualAddressing = clientRuntime.ManualAddressing;
            this.binder = binder;
            this.channel = channel;

            isConcurrent = true;
            duplexBinder = binder as DuplexChannelBinder;
            hasSession = binder.HasSession;
            isCallback = true;

            DispatchRuntime dispatchRuntime = clientRuntime.DispatchRuntime;
            if (dispatchRuntime == null)
            {
                receiver = new ErrorHandlingReceiver(binder, null);
            }
            else
            {
                receiver = new ErrorHandlingReceiver(binder, dispatchRuntime.ChannelDispatcher);
            }
            requestInfo = new RequestInfo(this);

        }

        internal ChannelHandler(MessageVersion messageVersion, IChannelBinder binder, /*ServiceThrottle throttle,*/
            ListenerHandler listener, /*bool wasChannelThrottled,*/ /*WrappedTransaction acceptTransaction,*/ ServiceChannel.SessionIdleManager idleManager)
        {
            ChannelDispatcher channelDispatcher = listener.ChannelDispatcher;

            this.messageVersion = messageVersion;
            isManualAddressing = channelDispatcher.ManualAddressing;
            this.binder = binder;
            //this.throttle = throttle;
            this.listener = listener;
            //this.wasChannelThrottled = wasChannelThrottled;

            host = listener.Host;
            receiveSynchronously = channelDispatcher.ReceiveSynchronously;
            sendAsynchronously = channelDispatcher.SendAsynchronously;
            duplexBinder = binder as DuplexChannelBinder;
            hasSession = binder.HasSession;
            isConcurrent = ConcurrencyBehavior.IsConcurrent(channelDispatcher, hasSession);

            if (channelDispatcher.MaxPendingReceives > 1)
            {
                // We need to preserve order if the ChannelHandler is not concurrent.
                this.binder = new MultipleReceiveBinder(
                    this.binder,
                    channelDispatcher.MaxPendingReceives,
                    !isConcurrent);
            }

            //if (channelDispatcher.BufferedReceiveEnabled)
            //{
            //    this.binder = new BufferedReceiveBinder(this.binder);
            //}

            receiver = new ErrorHandlingReceiver(this.binder, channelDispatcher);
            this.idleManager = idleManager;
            //Fx.Assert((this.idleManager != null) == (this.binder.HasSession && this.listener.ChannelDispatcher.DefaultCommunicationTimeouts.ReceiveTimeout != TimeSpan.MaxValue), "idle manager is present only when there is a session with a finite receive timeout");

            //if (channelDispatcher.IsTransactedReceive && !channelDispatcher.ReceiveContextEnabled)
            //{
            //    receiveSynchronously = true;
            //    receiveWithTransaction = true;

            //    if (channelDispatcher.MaxTransactedBatchSize > 0)
            //    {
            //        int maxConcurrentBatches = 1;
            //        if (null != throttle && throttle.MaxConcurrentCalls > 1)
            //        {
            //            maxConcurrentBatches = throttle.MaxConcurrentCalls;
            //            foreach (EndpointDispatcher endpointDispatcher in channelDispatcher.Endpoints)
            //            {
            //                if (ConcurrencyMode.Multiple != endpointDispatcher.DispatchRuntime.ConcurrencyMode)
            //                {
            //                    maxConcurrentBatches = 1;
            //                    break;
            //                }
            //            }
            //        }

            //        this.sharedTransactedBatchContext = new SharedTransactedBatchContext(this, channelDispatcher, maxConcurrentBatches);
            //        this.isMainTransactedBatchHandler = true;
            //        this.throttle = null;
            //    }
            //}
            //else if (channelDispatcher.IsTransactedReceive && channelDispatcher.ReceiveContextEnabled && channelDispatcher.MaxTransactedBatchSize > 0)
            //{
            //    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.IncompatibleBehaviors));
            //}

            if (this.binder.HasSession)
            {
                sessionOpenNotification = this.binder.Channel.GetProperty<SessionOpenNotification>();
                needToCreateSessionOpenNotificationMessage = sessionOpenNotification != null && sessionOpenNotification.IsEnabled;
            }

            //this.acceptTransaction = acceptTransaction;
            requestInfo = new RequestInfo(this);

            if (this.listener.State == CommunicationState.Opened)
            {
                //this.listener.ChannelDispatcher.Channels.IncrementActivityCount();
                incrementedActivityCountInConstructor = true;
            }
        }


        //internal ChannelHandler(ChannelHandler handler, TransactedBatchContext context)
        //{
        //    this.messageVersion = handler.messageVersion;
        //    this.isManualAddressing = handler.isManualAddressing;
        //    this.binder = handler.binder;
        //    this.listener = handler.listener;
        //    this.wasChannelThrottled = handler.wasChannelThrottled;

        //    this.host = handler.host;
        //    this.receiveSynchronously = true;
        //    this.receiveWithTransaction = true;
        //    this.duplexBinder = handler.duplexBinder;
        //    this.hasSession = handler.hasSession;
        //    this.isConcurrent = handler.isConcurrent;
        //    this.receiver = handler.receiver;

        //    this.sharedTransactedBatchContext = context.Shared;
        //    this.transactedBatchContext = context;
        //    this.requestInfo = new RequestInfo(this);

        //    this.sendAsynchronously = handler.sendAsynchronously;
        //    this.sessionOpenNotification = handler.sessionOpenNotification;
        //    this.needToCreateSessionOpenNotificationMessage = handler.needToCreateSessionOpenNotificationMessage;
        //    this.shouldRejectMessageWithOnOpenActionHeader = handler.shouldRejectMessageWithOnOpenActionHeader;
        //}

        internal IChannelBinder Binder
        {
            get { return binder; }
        }

        internal ServiceChannel Channel
        {
            get { return channel; }
        }

        internal bool HasRegisterBeenCalled
        {
            get { return hasRegisterBeenCalled; }
        }

        internal InstanceContext InstanceContext
        {
            get { return (channel != null) ? channel.InstanceContext : null; }
        }

        //internal ServiceThrottle InstanceContextServiceThrottle
        //{
        //    get
        //    {
        //        return this.instanceContextThrottle;
        //    }
        //    set
        //    {
        //        this.instanceContextThrottle = value;
        //    }
        //}

        bool IsOpen
        {
            get { return binder.Channel.State == CommunicationState.Opened; }
        }

        EndpointAddress LocalAddress
        {
            get
            {
                if (binder != null)
                {
                    IInputChannel input = binder.Channel as IInputChannel;
                    if (input != null)
                    {
                        return input.LocalAddress;
                    }

                    IReplyChannel reply = binder.Channel as IReplyChannel;
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

        //EventTraceActivity EventTraceActivity
        //{
        //    get
        //    {
        //        if (this.eventTraceActivity == null)
        //        {
        //            this.eventTraceActivity = new EventTraceActivity();
        //        }
        //        return this.eventTraceActivity;
        //    }
        //}

        internal static void Register(ChannelHandler handler)
        {
            handler.Register();
        }

        internal static void Register(ChannelHandler handler, RequestContext request)
        {
            BufferedReceiveBinder bufferedBinder = handler.Binder as BufferedReceiveBinder;
            Fx.Assert(bufferedBinder != null, "ChannelHandler.Binder is not a BufferedReceiveBinder");

            bufferedBinder.InjectRequest(request);
            handler.Register();
        }

        void Register()
        {
            hasRegisterBeenCalled = true;
            if (binder.Channel.State == CommunicationState.Created)
            {
                ActionItem.Schedule(openAndEnsurePump, this);
            }
            else
            {
                EnsurePump();
            }
        }

        async void AsyncMessagePump()
        {
            for (;;)
            {
                requestInfo.Cleanup();

                TryAsyncResult<RequestContext> result;
                do
                {
                    result = await TryReceiveAsync(CancellationToken.None);
                } while (!result.Success);

                var request = result.Result;

                if (!HandleRequest(request, null))
                {
                    break;
                }

                if (!TryAcquirePump())
                {
                    break;
                }
            }
        }

        //void AsyncMessagePump()
        //{
        //    IAsyncResult result = this.BeginTryReceive();

        //    if ((result != null) && result.CompletedSynchronously)
        //    {
        //        this.AsyncMessagePump(result);
        //    }
        //}

        //void AsyncMessagePump(IAsyncResult result)
        //{
        //    //if (TD.ChannelReceiveStopIsEnabled())
        //    //{
        //    //    TD.ChannelReceiveStop(this.EventTraceActivity, this.GetHashCode());
        //    //}

        //    for (;;)
        //    {
        //        RequestContext request;

        //        while (!this.EndTryReceive(result, out request))
        //        {
        //            result = this.BeginTryReceive();

        //            if ((result == null) || !result.CompletedSynchronously)
        //            {
        //                return;
        //            }
        //        }

        //        if (!HandleRequest(request, null))
        //        {
        //            break;
        //        }

        //        if (!TryAcquirePump())
        //        {
        //            break;
        //        }

        //        result = this.BeginTryReceive();

        //        if (result == null || !result.CompletedSynchronously)
        //        {
        //            break;
        //        }
        //    }
        //}

        bool DispatchAndReleasePump(RequestContext request, bool cleanThread, OperationContext currentOperationContext)
        {
            ServiceChannel channel = requestInfo.Channel;
            EndpointDispatcher endpoint = requestInfo.Endpoint;
            bool releasedPump = false;

            try
            {
                DispatchRuntime dispatchBehavior = requestInfo.DispatchRuntime;

                if (channel == null || dispatchBehavior == null)
                {
                    Fx.Assert("CoreWCF.Dispatcher.ChannelHandler.Dispatch(): (channel == null || dispatchBehavior == null)");
                    return true;
                }

                MessageBuffer buffer = null;
                Message message;

                //EventTraceActivity eventTraceActivity = TraceDispatchMessageStart(request.RequestMessage);
                if (dispatchBehavior.PreserveMessage)
                {
                    object previousBuffer = null;
                    if (request.RequestMessage.Properties.TryGetValue(MessageBufferPropertyName, out previousBuffer))
                    {
                        buffer = (MessageBuffer)previousBuffer;
                        message = buffer.CreateMessage();
                    }
                    else
                    {
                        // TODO, 34064, Need to fix this in clean way.
                        buffer = request.RequestMessage.CreateBufferedCopy(int.MaxValue);
                        message = buffer.CreateMessage();
                    }
                }
                else
                {
                    message = request.RequestMessage;
                }

                DispatchOperationRuntime operation = dispatchBehavior.GetOperation(ref message);
                if (operation == null)
                {
                    Fx.Assert("ChannelHandler.Dispatch (operation == null)");
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "No DispatchOperationRuntime found to process message.")));
                }

                if (shouldRejectMessageWithOnOpenActionHeader && message.Headers.Action == OperationDescription.SessionOpenedAction)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxNoEndpointMatchingAddressForConnectionOpeningMessage, message.Headers.Action, "Open")));
                }

                //if (MessageLogger.LoggingEnabled)
                //{
                //    MessageLogger.LogMessage(ref message, (operation.IsOneWay ? MessageLoggingSource.ServiceLevelReceiveDatagram : MessageLoggingSource.ServiceLevelReceiveRequest) | MessageLoggingSource.LastChance);
                //}

                if (operation.IsTerminating && hasSession)
                {
                    isChannelTerminated = true;
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
                    currentOperationContext = new OperationContext(request, message, channel, host);
                }

                if (dispatchBehavior.PreserveMessage)
                {
                    currentOperationContext.IncomingMessageProperties.Add(MessageBufferPropertyName, buffer);
                }

                if (currentOperationContext.EndpointDispatcher == null && listener != null)
                {
                    currentOperationContext.EndpointDispatcher = endpoint;
                }

                MessageRpc rpc = new MessageRpc(request, message, operation, channel, host,
                    /*this,*/ cleanThread, currentOperationContext, requestInfo.ExistingInstanceContext/*, eventTraceActivity*/);

                //TraceUtility.MessageFlowAtMessageReceived(message, currentOperationContext, eventTraceActivity, true);

                //rpc.TransactedBatchContext = this.transactedBatchContext;

                // passing responsibility for call throttle to MessageRpc
                // (MessageRpc implicitly owns this throttle once it's created)
                // explicitly passing responsibility for instance throttle to MessageRpc

                // These need to happen before Dispatch but after accessing any ChannelHandler
                // state, because we go multi-threaded after this until we reacquire pump mutex.
                ReleasePump();
                releasedPump = true;

                // Code here will be completely broken as I've removed the pause and resume mechanism.
                operation.Parent.DispatchAsync(ref rpc, hasOperationContextBeenSet);
                return true;
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }
                return HandleError(e, request, channel);
            }
            finally
            {
                if (!releasedPump)
                {
                    ReleasePump();
                }
            }
        }

        internal void DispatchDone()
        {
            //if (this.throttle != null)
            //{
            //    this.throttle.DeactivateCall();
            //}
        }

        RequestContext GetSessionOpenNotificationRequestContext()
        {
            Fx.Assert(sessionOpenNotification != null, "this.sessionOpenNotification should not be null.");
            Message message = Message.CreateMessage(Binder.Channel.GetProperty<MessageVersion>(), OperationDescription.SessionOpenedAction);
            Fx.Assert(LocalAddress != null, "this.LocalAddress should not be null.");
            message.Headers.To = LocalAddress.Uri;
            sessionOpenNotification.UpdateMessageProperties(message.Properties);
            return Binder.CreateRequestContext(message);
        }

        void EnsureChannelAndEndpoint(RequestContext request)
        {
            requestInfo.Channel = channel;

            if (requestInfo.Channel == null)
            {
                bool addressMatched;
                if (hasSession)
                {
                    requestInfo.Channel = GetSessionChannel(request.RequestMessage, out requestInfo.Endpoint, out addressMatched);
                }
                else
                {
                    requestInfo.Channel = GetDatagramChannel(request.RequestMessage, out requestInfo.Endpoint, out addressMatched);
                }

                if (requestInfo.Channel == null)
                {
                    // TODO: Move the UnknownMessageReceived handler elsewhere and plumb in to here. 
                    // Maybe define interface that user can add to DI and query for here. If it doesn't exist, do the default behavior.
                    //host.RaiseUnknownMessageReceived(request.RequestMessage);
                    if (addressMatched)
                    {
                        ReplyContractFilterDidNotMatch(request);
                    }
                    else
                    {
                        ReplyAddressFilterDidNotMatch(request);
                    }
                }
            }
            else
            {
                requestInfo.Endpoint = requestInfo.Channel.EndpointDispatcher;

                //For sessionful contracts, the InstanceContext throttle is not copied over to the channel
                //as we create the channel before acquiring the lock
                //if (this.InstanceContextServiceThrottle != null && this.requestInfo.Channel.InstanceContextServiceThrottle == null)
                //{
                //    this.requestInfo.Channel.InstanceContextServiceThrottle = this.InstanceContextServiceThrottle;
                //}
            }

            requestInfo.EndpointLookupDone = true;

            if (requestInfo.Channel == null)
            {
                // SFx drops a message here
                //TraceUtility.TraceDroppedMessage(request.RequestMessage, this.requestInfo.Endpoint);
                request.CloseAsync().GetAwaiter().GetResult();
                return;
            }

            if (requestInfo.Channel.HasSession || isCallback)
            {
                requestInfo.DispatchRuntime = requestInfo.Channel.DispatchRuntime;
            }
            else
            {
                requestInfo.DispatchRuntime = requestInfo.Endpoint.DispatchRuntime;
            }
        }

        void EnsurePump()
        {
            //if (null == this.sharedTransactedBatchContext || this.isMainTransactedBatchHandler)
            //{
                if (TryAcquirePump())
                {
                    if (receiveSynchronously)
                    {
                        throw new PlatformNotSupportedException("No more receive sync");
                        //ActionItem.Schedule(ChannelHandler.onStartSyncMessagePump, this);
                    }
                    else
                    {
                    //if (Thread.CurrentThread.IsThreadPoolThread)
                    //{
                        using (TaskHelpers.RunTaskContinuationsOnOurThreads())
                        {
                            AsyncMessagePump();
                        }
                        //}
                        //else
                        //{
                        //    // Since this is not a threadpool thread, we don't know if this thread will exit 
                        //    // while the IO is still pending (which would cancel the IO), so we have to get 
                        //    // over to a threadpool thread which we know will not exit while there is pending IO.
                        //    ActionItem.Schedule(ChannelHandler.onStartAsyncMessagePump, this);
                        //}
                    }
                }
            //}
            //else
            //{
            //    ActionItem.Schedule(ChannelHandler.onStartSingleTransactedBatch, this);
            //}
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
                lock (listener.ThisLock)
                {
                    if (endpoint.DatagramChannel == null)
                    {
                        endpoint.DatagramChannel = null; //new ServiceChannel(binder, endpoint, listener.ChannelDispatcher, idleManager);
                        InitializeServiceChannel(endpoint.DatagramChannel);
                    }
                }
            }

            return endpoint.DatagramChannel;
        }

        EndpointDispatcher GetEndpointDispatcher(Message message, out bool addressMatched)
        {
            return listener.Endpoints.Lookup(message, out addressMatched);
        }

        ServiceChannel GetSessionChannel(Message message, out EndpointDispatcher endpoint, out bool addressMatched)
        {
            addressMatched = false;

            if (channel == null)
            {
                lock (ThisLock)
                {
                    if (channel == null)
                    {
                        endpoint = GetEndpointDispatcher(message, out addressMatched);
                        if (endpoint != null)
                        {
                            channel = null; //new ServiceChannel(binder, endpoint, listener.ChannelDispatcher, idleManager);
                            InitializeServiceChannel(channel);
                        }
                    }
                }
            }

            if (channel == null)
            {
                endpoint = null;
            }
            else
            {
                endpoint = channel.EndpointDispatcher;
            }
            return channel;
        }

        void InitializeServiceChannel(ServiceChannel channel)
        {
            //if (this.wasChannelThrottled)
            //{
            //    // TFS#500703, when the idle timeout was hit, the constructor of ServiceChannel will abort itself directly. So
            //    // the session throttle will not be released and thus lead to a service unavailablity.
            //    // Note that if the channel is already aborted, the next line "channel.ServiceThrottle = this.throttle;" will throw an exception,
            //    // so we are not going to do any more work inside this method. 
            //    // Ideally we should do a thorough refactoring work for this throttling issue. However, it's too risky as a QFE. We should consider
            //    // this in a whole release.
            //    // Note that the "wasChannelThrottled" boolean will only be true if we aquired the session throttle. So we don't have to check HasSession
            //    // again here.
            //    if (channel.Aborted && this.throttle != null)
            //    {
            //        // This line will release the "session" throttle.
            //        this.throttle.DeactivateChannel();
            //    }

            //    channel.ServiceThrottle = this.throttle;
            //}

            //if (this.InstanceContextServiceThrottle != null)
            //{
            //    channel.InstanceContextServiceThrottle = this.InstanceContextServiceThrottle;
            //}

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

            if (listener != null)
            {
                //listener.ChannelDispatcher.InitializeChannel((IClientChannel)channel.Proxy);
            }

            ((IChannel)channel).OpenAsync().GetAwaiter().GetResult();
        }

        void ProvideFault(Exception e, ref ErrorHandlerFaultInfo faultInfo)
        {
            if (listener != null)
            {
                listener.ChannelDispatcher.ProvideFault(e, requestInfo.Channel == null ? binder.Channel.GetProperty<FaultConverter>() : requestInfo.Channel.GetProperty<FaultConverter>(), ref faultInfo);
            }
            else if (channel != null)
            {
                DispatchRuntime dispatchBehavior = channel.ClientRuntime.CallbackDispatchRuntime;
                dispatchBehavior.ChannelDispatcher.ProvideFault(e, channel.GetProperty<FaultConverter>(), ref faultInfo);
            }
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
            if (listener != null)
            {
                return listener.ChannelDispatcher.HandleError(e, ref faultInfo);
            }
            else if (channel != null)
            {
                return channel.ClientRuntime.CallbackDispatchRuntime.ChannelDispatcher.HandleError(e, ref faultInfo);
            }
            else
            {
                return false;
            }
        }

        bool HandleError(Exception e, RequestContext request, ServiceChannel channel)
        {
            ErrorHandlerFaultInfo faultInfo = new ErrorHandlerFaultInfo(messageVersion.Addressing.DefaultFaultAction);
            bool replied, replySentAsync;
            ProvideFaultAndReplyFailure(request, e, ref faultInfo, out replied, out replySentAsync);

            if (!replySentAsync)
            {
                return HandleErrorContinuation(e, request, channel, ref faultInfo, replied);
            }
            else
            {
                return false;
            }
        }

        bool HandleErrorContinuation(Exception e, RequestContext request, ServiceChannel channel, ref ErrorHandlerFaultInfo faultInfo, bool replied)
        {
            if (replied)
            {
                try
                {
                    request.CloseAsync().GetAwaiter().GetResult();
                }
                catch (Exception e1)
                {
                    if (Fx.IsFatal(e1))
                    {
                        throw;
                    }
                    HandleError(e1);
                }
            }
            else
            {
                request.Abort();
            }
            if (!HandleError(e, ref faultInfo) && hasSession)
            {
                if (channel != null)
                {
                    if (replied)
                    {
                        TimeoutHelper timeoutHelper = new TimeoutHelper(CloseAfterFaultTimeout);
                        try
                        {
                            channel.CloseAsync(timeoutHelper.GetCancellationToken()).GetAwaiter().GetResult();
                        }
                        catch (Exception e2)
                        {
                            if (Fx.IsFatal(e2))
                            {
                                throw;
                            }
                            HandleError(e2);
                        }
                        try
                        {
                            binder.CloseAfterFault(timeoutHelper.RemainingTime());
                        }
                        catch (Exception e3)
                        {
                            if (Fx.IsFatal(e3))
                            {
                                throw;
                            }
                            HandleError(e3);
                        }
                    }
                    else
                    {
                        channel.Abort();
                        binder.Abort();
                    }
                }
                else
                {
                    if (replied)
                    {
                        try
                        {
                            binder.CloseAfterFault(CloseAfterFaultTimeout);
                        }
                        catch (Exception e4)
                        {
                            if (Fx.IsFatal(e4))
                            {
                                throw;
                            }
                            HandleError(e4);
                        }
                    }
                    else
                    {
                        binder.Abort();
                    }
                }
            }

            return true;
        }

        async Task HandleReceiveCompleteAsync(RequestContext context)
        {
            try
            {
                if (channel != null)
                {
                    channel.HandleReceiveComplete(context);
                }
                else
                {
                    if (context == null && hasSession)
                    {
                        bool close;
                        lock (ThisLock)
                        {
                            close = !doneReceiving;
                            doneReceiving = true;
                        }

                        if (close)
                        {
                            await receiver.CloseAsync();

                            if (idleManager != null)
                            {
                                idleManager.CancelTimer();
                            }

                            //ServiceThrottle throttle = this.throttle;
                            //if (throttle != null)
                            //{
                            //    throttle.DeactivateChannel();
                            //}
                        }
                    }
                }
            }
            finally
            {
                if ((context == null) && incrementedActivityCountInConstructor)
                {
                    //listener.ChannelDispatcher.Channels.DecrementActivityCount();
                }
            }
        }

        bool HandleRequest(RequestContext request, OperationContext currentOperationContext)
        {
            if (request == null)
            {
                // channel EOF, stop receiving
                return false;
            }

            //ServiceModelActivity activity = DiagnosticUtility.ShouldUseActivity ? TraceUtility.ExtractActivity(request) : null;

            //using (ServiceModelActivity.BoundOperation(activity))
            //{
                if (HandleRequestAsReply(request))
                {
                    ReleasePump();
                    return true;
                }

                if (isChannelTerminated)
                {
                    ReleasePump();
                    ReplyChannelTerminated(request);
                    return true;
                }

                if (requestInfo.RequestContext != null)
                {
                    Fx.Assert("ChannelHandler.HandleRequest: this.requestInfo.RequestContext != null");
                }

                requestInfo.RequestContext = request;


                if (!TryRetrievingInstanceContext(request))
                {
                    //Would have replied and close the request.
                    return true;
                }

                requestInfo.Channel.CompletedIOOperation();

                if (!DispatchAndReleasePump(request, true, currentOperationContext))
                {
                    // this.DispatchDone will be called to continue
                    return false;
                }
            //}
            return true;
        }

        bool HandleRequestAsReply(RequestContext request)
        {
            if (duplexBinder != null)
            {
                if (duplexBinder.HandleRequestAsReply(request.RequestMessage))
                {
                    return true;
                }
            }
            return false;
        }

        static void OnStartAsyncMessagePump(object state)
        {
            ((ChannelHandler)state).AsyncMessagePump();
        }

        static void OpenAndEnsurePump(object state)
        {
            ((ChannelHandler)state).OpenAndEnsurePump();
        }

        void OpenAndEnsurePump()
        {
            Exception exception = null;
            try
            {
                binder.Channel.OpenAsync().GetAwaiter().GetResult();
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
                //    TraceUtility.TraceEvent(TraceEventType.Warning,
                //        TraceCode.FailedToOpenIncomingChannel,
                //        SR.TraceCodeFailedToOpenIncomingChannel);
                //}
                ServiceChannel.SessionIdleManager idleManager = this.idleManager;
                if (idleManager != null)
                {
                    idleManager.CancelTimer();
                }
                //if ((this.throttle != null) && this.hasSession)
                //{
                //    this.throttle.DeactivateChannel();
                //}

                bool errorHandled = HandleError(exception);

                if (incrementedActivityCountInConstructor)
                {
                    //listener.ChannelDispatcher.Channels.DecrementActivityCount();
                }

                if (!errorHandled)
                {
                    binder.Channel.Abort();
                }
            }
            else
            {
                EnsurePump();
            }
        }

        private async Task<TryAsyncResult<RequestContext>> TryReceiveAsync(CancellationToken token)
        {
            TryAsyncResult<RequestContext> result;
            shouldRejectMessageWithOnOpenActionHeader = !needToCreateSessionOpenNotificationMessage;

            if (needToCreateSessionOpenNotificationMessage)
            {
                needToCreateSessionOpenNotificationMessage = false;
                result = TryAsyncResult.FromResult(GetSessionOpenNotificationRequestContext());
            }
            else
            {
                result = await receiver.TryReceiveAsync(token);
            }

            if (result.Success)
            {
                await HandleReceiveCompleteAsync(result.Result);
            }

            return result;
        }

        void ReplyAddressFilterDidNotMatch(RequestContext request)
        {
            FaultCode code = FaultCode.CreateSenderFaultCode(AddressingStrings.DestinationUnreachable,
                messageVersion.Addressing.Namespace);
            string reason = SR.Format(SR.SFxNoEndpointMatchingAddress, request.RequestMessage.Headers.To);

            ReplyFailure(request, code, reason);
        }

        void ReplyContractFilterDidNotMatch(RequestContext request)
        {
            // By default, the contract filter is just a filter over the set of initiating actions in 
            // the contract, so we do error messages accordingly
            AddressingVersion addressingVersion = messageVersion.Addressing;
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
                    messageVersion.Addressing.Namespace);
                string reason = SR.Format(SR.SFxNoEndpointMatchingContract, request.RequestMessage.Headers.Action);
                ReplyFailure(request, code, reason, messageVersion.Addressing.FaultAction);
            }
        }

        void ReplyChannelTerminated(RequestContext request)
        {
            FaultCode code = FaultCode.CreateSenderFaultCode(FaultCodeConstants.Codes.SessionTerminated,
                FaultCodeConstants.Namespaces.NetDispatch);
            string reason = SR.SFxChannelTerminated0;
            string action = FaultCodeConstants.Actions.NetDispatcher;
            Message fault = Message.CreateMessage(messageVersion, code, reason, action);
            ReplyFailure(request, fault, action, reason, code);
        }

        void ReplyFailure(RequestContext request, FaultCode code, string reason)
        {
            string action = messageVersion.Addressing.DefaultFaultAction;
            ReplyFailure(request, code, reason, action);
        }

        void ReplyFailure(RequestContext request, FaultCode code, string reason, string action)
        {
            Message fault = Message.CreateMessage(messageVersion, code, reason, action);
            ReplyFailure(request, fault, action, reason, code);
        }

        void ReplyFailure(RequestContext request, Message fault, string action, string reason, FaultCode code)
        {
            FaultException exception = new FaultException(reason, code);
            ErrorBehavior.ThrowAndCatch(exception);
            ErrorHandlerFaultInfo faultInfo = new ErrorHandlerFaultInfo(action);
            faultInfo.Fault = fault;
            bool replied, replySentAsync;
            ProvideFaultAndReplyFailure(request, exception, ref faultInfo, out replied, out replySentAsync);
            HandleError(exception, ref faultInfo);
        }

        void ProvideFaultAndReplyFailure(RequestContext request, Exception exception, ref ErrorHandlerFaultInfo faultInfo, out bool replied, out bool replySentAsync)
        {
            replied = false;
            replySentAsync = false;
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
            if (listener != null)
            {
                enableFaults = listener.ChannelDispatcher.EnableFaults;
            }

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
                                if (sendAsynchronously)
                                {
                                    var state = new ContinuationState { ChannelHandler = this, Channel = channel, Exception = exception, FaultInfo = faultInfo, Request = request, Reply = reply };
                                    var result = request.ReplyAsync(reply);
                                    result.ContinueWith(AsyncReplyComplete, state);
                                    replied = result.IsCompleted;
                                    replySentAsync = !replied;
                                }
                                else
                                {
                                    request.ReplyAsync(reply).GetAwaiter().GetResult();
                                    replied = true;
                                }
                            }
                        }
                        finally
                        {
                            if (!replySentAsync)
                            {
                                reply.Close();
                            }
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
        }

        /// <summary>
        /// Prepares a reply that can either be sent asynchronously or synchronously depending on the value of 
        /// sendAsynchronously
        /// </summary>
        /// <param name="request">The request context to prepare</param>
        /// <param name="reply">The reply to prepare</param>
        /// <returns>True if channel is open and prepared reply should be sent; otherwise false.</returns>
        bool PrepareReply(RequestContext request, Message reply)
        {
            // Ensure we only reply once (we may hit the same error multiple times)
            if (replied == request)
            {
                return false;
            }
            replied = request;

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
                if (!object.ReferenceEquals(requestID, null) && !isManualAddressing)
                {
                    RequestReplyCorrelator.PrepareReply(reply, requestID);
                }
                if (!hasSession && !isManualAddressing)
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

        static void AsyncReplyComplete(Task result, object obj)
        {
            ContinuationState state = (ContinuationState)obj;
            try
            {
                result.GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                DiagnosticUtility.TraceHandledException(e, TraceEventType.Error);

                if (Fx.IsFatal(e))
                {
                    throw;
                }

                state.ChannelHandler.HandleError(e);
            }

            try
            {
                state.Reply.Close();
            }
            catch (Exception e)
            {
                DiagnosticUtility.TraceHandledException(e, TraceEventType.Error);

                if (Fx.IsFatal(e))
                {
                    throw;
                }

                state.ChannelHandler.HandleError(e);
            }

            try
            {
                state.ChannelHandler.HandleErrorContinuation(state.Exception, state.Request, state.Channel, ref state.FaultInfo, true);
            }
            catch (Exception e)
            {
                DiagnosticUtility.TraceHandledException(e, TraceEventType.Error);

                if (Fx.IsFatal(e))
                {
                    throw;
                }

                state.ChannelHandler.HandleError(e);
            }

            state.ChannelHandler.EnsurePump();
        }

        void ReleasePump()
        {
            if (isConcurrent)
            {
                Interlocked.Exchange(ref isPumpAcquired, 0);
            }
        }

        // TODO: Convert to async for close code path
        bool TryRetrievingInstanceContext(RequestContext request)
        {
            try
            {
                return TryRetrievingInstanceContextCore(request);
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
                    request.CloseAsync().GetAwaiter().GetResult();
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
        //      : True denotes operation is successful.
        bool TryRetrievingInstanceContextCore(RequestContext request)
        {
            bool releasePump = true;
            try
            {
                if (!requestInfo.EndpointLookupDone)
                {
                    EnsureChannelAndEndpoint(request);
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
                        HandleError(e, request, channel);
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
                    TraceUtility.TraceDroppedMessage(request.RequestMessage, requestInfo.Endpoint);
                    request.CloseAsync().GetAwaiter().GetResult();
                    return false;
                }
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }

                HandleError(e, request, channel);

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

        bool TryAcquirePump()
        {
            if (isConcurrent)
            {
                return Interlocked.CompareExchange(ref isPumpAcquired, 1, 0) == 0;
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

            public RequestInfo(ChannelHandler channelHandler)
            {
                Endpoint = null;
                ExistingInstanceContext = null;
                Channel = null;
                EndpointLookupDone = false;
                DispatchRuntime = null;
                RequestContext = null;
                ChannelHandler = channelHandler;
            }

            public void Cleanup()
            {
                Endpoint = null;
                ExistingInstanceContext = null;
                Channel = null;
                EndpointLookupDone = false;
                RequestContext = null;
            }
        }

        //EventTraceActivity TraceDispatchMessageStart(Message message)
        //{
        //    if (FxTrace.Trace.IsEnd2EndActivityTracingEnabled && message != null)
        //    {
        //        EventTraceActivity eventTraceActivity = EventTraceActivityHelper.TryExtractActivity(message);
        //        if (TD.DispatchMessageStartIsEnabled())
        //        {
        //            TD.DispatchMessageStart(eventTraceActivity);
        //        }
        //        return eventTraceActivity;
        //    }

        //    return null;
        //}

        /// <summary>
        /// Data structure used to carry state for asynchronous replies
        /// </summary>
        struct ContinuationState
        {
            public ChannelHandler ChannelHandler;
            public Exception Exception;
            public RequestContext Request;
            public Message Reply;
            public ServiceChannel Channel;
            public ErrorHandlerFaultInfo FaultInfo;
        }
    }

}