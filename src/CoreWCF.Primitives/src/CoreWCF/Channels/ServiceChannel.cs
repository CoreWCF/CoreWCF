using CoreWCF.Configuration;
using CoreWCF.Description;
using CoreWCF.Dispatcher;
using CoreWCF.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Channels
{
    // This class is sealed because the constructor could call Abort, which is virtual
    sealed class ServiceChannel : CommunicationObject, IChannel, IClientChannel, IDuplexContextChannel, IOutputChannel, IRequestChannel, IServiceChannel
    {

        int activityCount = 0;
        readonly bool allowOutputBatching = false;
        bool autoClose = true;
        //CallOnceManager autoDisplayUIManager;
        CallOnceManager autoOpenManager;
        readonly IChannelBinder binder;
        readonly ChannelDispatcher channelDispatcher;
        ClientRuntime clientRuntime;
        readonly bool closeBinder = true;
        bool closeFactory;
        bool doneReceiving;
        EndpointDispatcher endpointDispatcher;
        bool explicitlyOpened;
        ExtensionCollection<IContextChannel> extensions;
        readonly bool hasSession;
        readonly SessionIdleManager idleManager;
        InstanceContext instanceContext;
        ServiceThrottle instanceContextServiceThrottle;
        bool isPending;
        readonly bool isReplyChannel;
        EndpointAddress localAddress;
        readonly MessageVersion messageVersion;
        readonly bool openBinder = false;
        TimeSpan operationTimeout;
        object proxy;
        ServiceThrottle serviceThrottle;
        string terminatingOperationName;
        bool hasCleanedUpChannelCollections;
        //EventTraceActivity eventActivity;
        IDefaultCommunicationTimeouts timeouts;

        EventHandler<UnknownMessageReceivedEventArgs> unknownMessageReceived;

        ServiceChannel(IChannelBinder binder, Binding binding)
        {
            if (binder == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(binder));
            }

            this.messageVersion = binding.MessageVersion;
            this.binder = binder;
            isReplyChannel = this.binder.Channel is IReplyChannel;

            IChannel innerChannel = binder.Channel;
            hasSession = (innerChannel is ISessionChannel<IDuplexSession>) ||
                        (innerChannel is ISessionChannel<IInputSession>) ||
                        (innerChannel is ISessionChannel<IOutputSession>);

            IncrementActivity();
            openBinder = (binder.Channel.State == CommunicationState.Created);

            operationTimeout = binding.SendTimeout;
            this.timeouts = binding;
        }

        // Only used by ServiceChannelFactory
        //internal ServiceChannel(ServiceChannelFactory factory, IChannelBinder binder)
        //    : this(binder, factory.MessageVersion, factory)
        //{
        //    this.factory = factory;
        //    this.clientRuntime = factory.ClientRuntime;

        //    this.SetupInnerChannelFaultHandler();

        //    DispatchRuntime dispatch = factory.ClientRuntime.DispatchRuntime;
        //    if (dispatch != null)
        //    {
        //        this.autoClose = dispatch.AutomaticInputSessionShutdown;
        //    }

        //    factory.ChannelCreated(this);
        //}

        internal ServiceChannel(IChannelBinder binder,
                                EndpointDispatcher endpointDispatcher,
                                ServiceDispatcher serviceDispatcher,
                                SessionIdleManager idleManager)
            : this(binder, serviceDispatcher.Binding)
        {
            if (endpointDispatcher == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(endpointDispatcher));
            }

            channelDispatcher = serviceDispatcher.ChannelDispatcher;
            this.endpointDispatcher = endpointDispatcher;
            clientRuntime = endpointDispatcher.DispatchRuntime.CallbackClientRuntime;

            SetupInnerChannelFaultHandler();

            autoClose = endpointDispatcher.DispatchRuntime.AutomaticInputSessionShutdown;
            isPending = true;

            this.idleManager = idleManager;

            if (!binder.HasSession)
                closeBinder = false;

            if (this.idleManager != null)
            {
                bool didIdleAbort;
                this.idleManager.RegisterChannel(this, out didIdleAbort);
                if (didIdleAbort)
                {
                    Abort();
                }
            }
        }

        CallOnceManager AutoOpenManager
        {
            get
            {
                if (!explicitlyOpened && (autoOpenManager == null))
                {
                    EnsureAutoOpenManagers();
                }
                return autoOpenManager;
            }
        }

        //CallOnceManager AutoDisplayUIManager
        //{
        //    get
        //    {
        //        if (!this.explicitlyOpened && (this.autoDisplayUIManager == null))
        //        {
        //            this.EnsureAutoOpenManagers();
        //        }
        //        return this.autoDisplayUIManager;
        //    }
        //}


        //internal EventTraceActivity EventActivity
        //{
        //    get
        //    {
        //        if (this.eventActivity == null)
        //        {
        //            //Take the id on the thread so that we know the initiating operation.
        //            this.eventActivity = EventTraceActivity.GetFromThreadOrCreate();
        //        }
        //        return this.eventActivity;
        //    }
        //}

        internal bool CloseFactory
        {
            get { return closeFactory; }
            set { closeFactory = value; }
        }

        protected override TimeSpan DefaultCloseTimeout
        {
            get { return CloseTimeout; }
        }

        protected override TimeSpan DefaultOpenTimeout
        {
            get { return OpenTimeout; }
        }

        internal DispatchRuntime DispatchRuntime
        {
            get
            {
                if (endpointDispatcher != null)
                {
                    return endpointDispatcher.DispatchRuntime;
                }
                if (clientRuntime != null)
                {
                    return clientRuntime.DispatchRuntime;
                }
                return null;
            }
        }

        internal MessageVersion MessageVersion
        {
            get { return messageVersion; }
        }

        internal IChannelBinder Binder
        {
            get { return binder; }
        }

        internal TimeSpan CloseTimeout
        {
            get
            {
                //if (this.IsClient)
                //{
                //    return factory.InternalCloseTimeout;
                //}
                //else
                //{
                return timeouts.CloseTimeout;
                //}
            }
        }

        internal ChannelDispatcher ChannelDispatcher
        {
            get { return channelDispatcher; }
        }

        internal EndpointDispatcher EndpointDispatcher
        {
            get { return endpointDispatcher; }
            set
            {
                lock (ThisLock)
                {
                    endpointDispatcher = value;
                    clientRuntime = value.DispatchRuntime.CallbackClientRuntime;
                }
            }
        }

        //internal ServiceChannelFactory Factory
        //{
        //    get { return this.factory; }
        //}

        internal IChannel InnerChannel
        {
            get { return binder.Channel; }
        }

        internal bool IsPending
        {
            get { return isPending; }
            set { isPending = value; }
        }

        internal bool HasSession
        {
            get { return hasSession; }
        }

        internal bool IsReplyChannel
        {
            get { return isReplyChannel; }
        }

        public Uri ListenUri
        {
            get
            {
                return binder.ListenUri;
            }
        }

        public EndpointAddress LocalAddress
        {
            get
            {
                if (localAddress == null)
                {
                    if (endpointDispatcher != null)
                    {
                        localAddress = endpointDispatcher.EndpointAddress;
                    }
                    else
                    {
                        localAddress = binder.LocalAddress;
                    }
                }
                return localAddress;
            }
        }

        internal TimeSpan OpenTimeout
        {
            get
            {
                //if (this.IsClient)
                //{
                //    return factory.InternalOpenTimeout;
                //}
                //else
                //{
                return ChannelDispatcher.InternalOpenTimeout;
                //}
            }
        }

        public TimeSpan OperationTimeout
        {
            get { return operationTimeout; }
            set
            {
                if (value < TimeSpan.Zero)
                {
                    string message = SR.SFxTimeoutOutOfRange0;
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value, message));
                }
                if (TimeoutHelper.IsTooLarge(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value, SR.SFxTimeoutOutOfRangeTooBig));
                }


                operationTimeout = value;
            }
        }

        internal object Proxy
        {
            get
            {
                object proxy = this.proxy;
                if (proxy != null)
                    return proxy;
                else
                    return this;
            }
            set
            {
                proxy = value;
                base.EventSender = value;   // need to use "proxy" as open/close event source
            }
        }

        internal ClientRuntime ClientRuntime
        {
            get { return clientRuntime; }
        }

        public EndpointAddress RemoteAddress
        {
            get
            {
                IOutputChannel outputChannel = InnerChannel as IOutputChannel;
                if (outputChannel != null)
                    return outputChannel.RemoteAddress;

                IRequestChannel requestChannel = InnerChannel as IRequestChannel;
                if (requestChannel != null)
                    return requestChannel.RemoteAddress;

                return null;
            }
        }

        ProxyOperationRuntime UnhandledProxyOperation
        {
            get { return ClientRuntime.GetRuntime().UnhandledProxyOperation; }
        }

        public Uri Via
        {
            get
            {
                IOutputChannel outputChannel = InnerChannel as IOutputChannel;
                if (outputChannel != null)
                    return outputChannel.Via;

                IRequestChannel requestChannel = InnerChannel as IRequestChannel;
                if (requestChannel != null)
                    return requestChannel.Via;

                return null;
            }
        }

        internal InstanceContext InstanceContext
        {
            get { return instanceContext; }
            set { instanceContext = value; }
        }

        internal ServiceThrottle InstanceContextServiceThrottle
        {
            get { return this.instanceContextServiceThrottle; }
            set { this.instanceContextServiceThrottle = value; }
        }

        internal ServiceThrottle ServiceThrottle
        {
            get { return this.serviceThrottle; }
            set
            {
                this.ThrowIfDisposed();
                this.serviceThrottle = value;
            }
        }

        void SetupInnerChannelFaultHandler()
        {
            // need to call this method after this.binder and this.clientRuntime are set to prevent a potential 
            // NullReferenceException in this method or in the OnInnerChannelFaulted method; 
            // because this method accesses this.binder and OnInnerChannelFaulted accesses this.clientRuntime.
            binder.Channel.Faulted += OnInnerChannelFaulted;
        }

        //void BindDuplexCallbacks()
        //{
        //    IDuplexChannel duplexChannel = this.InnerChannel as IDuplexChannel;
        //    if ((duplexChannel != null) && (this.factory != null) && (this.instanceContext != null))
        //    {
        //        if (this.binder is DuplexChannelBinder)
        //            ((DuplexChannelBinder)this.binder).EnsurePumping();
        //    }
        //}

        internal bool CanCastTo(Type t)
        {
            if (t.IsAssignableFrom(typeof(IClientChannel)))
                return true;

            if (t.IsAssignableFrom(typeof(IDuplexContextChannel)))
                return InnerChannel is IDuplexChannel;

            if (t.IsAssignableFrom(typeof(IServiceChannel)))
                return true;

            return false;
        }

        internal void CompletedIOOperation()
        {
            if (idleManager != null)
            {
                idleManager.CompletedActivity();
            }
        }

        void EnsureAutoOpenManagers()
        {
            lock (ThisLock)
            {
                if (!explicitlyOpened)
                {
                    if (autoOpenManager == null)
                    {
                        autoOpenManager = new CallOnceManager(this, CallOpenOnce.Instance);
                    }
                }
            }
        }

        async Task EnsureOpenedAsync(CancellationToken token)
        {
            ///// TASKS ******
            CallOnceManager manager = AutoOpenManager;
            if (manager != null)
            {
                await manager.CallOnceAsync(token);
            }

            ThrowIfOpening();
            ThrowIfDisposedOrNotOpen();
        }

        public T GetProperty<T>() where T : class
        {
            IChannel innerChannel = InnerChannel;
            if (innerChannel != null)
                return innerChannel.GetProperty<T>();
            return null;
        }

        void PrepareCall(ProxyOperationRuntime operation, bool oneway, ref ProxyRpc rpc)
        {
            OperationContext context = OperationContext.Current;
            // Doing a request reply callback when dispatching in-order deadlocks.
            // We never receive the reply until we finish processing the current message.
            if (!oneway)
            {
                DispatchRuntime dispatchBehavior = ClientRuntime.DispatchRuntime;
                if ((dispatchBehavior != null) && (dispatchBehavior.ConcurrencyMode == ConcurrencyMode.Single))
                {
                    if ((context != null) && (!context.IsUserContext) && (context.InternalServiceChannel == this))
                    {
                        if (dispatchBehavior.IsOnServer)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxCallbackRequestReplyInOrder1, typeof(ServiceBehaviorAttribute).Name)));
                        }
                        else
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxCallbackRequestReplyInOrder1, typeof(CallbackBehaviorAttribute).Name)));
                        }
                    }
                }
            }

            if ((State == CommunicationState.Created) && !operation.IsInitiating)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxNonInitiatingOperation1, operation.Name)));
            }

            if (terminatingOperationName != null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxTerminatingOperationAlreadyCalled1, terminatingOperationName)));
            }

            operation.BeforeRequest(ref rpc);
            AddMessageProperties(rpc.Request, context);
            if (!oneway && !ClientRuntime.ManualAddressing && rpc.Request.Version.Addressing != AddressingVersion.None)
            {
                RequestReplyCorrelator.PrepareRequest(rpc.Request);

                MessageHeaders headers = rpc.Request.Headers;
                EndpointAddress localAddress = LocalAddress;
                EndpointAddress replyTo = headers.ReplyTo;

                if (replyTo == null)
                {
                    headers.ReplyTo = localAddress ?? EndpointAddress.AnonymousAddress;
                }
            }

            //if (TraceUtility.MessageFlowTracingOnly)
            //{
            //    //always set a new ID if none provided
            //    if (Trace.CorrelationManager.ActivityId == Guid.Empty)
            //    {
            //        rpc.ActivityId = Guid.NewGuid();
            //        FxTrace.Trace.SetAndTraceTransfer(rpc.ActivityId, true);
            //    }
            //}

            //if (rpc.Activity != null)
            //{
            //    TraceUtility.SetActivity(rpc.Request, rpc.Activity);
            //    if (TraceUtility.ShouldPropagateActivity)
            //    {
            //        TraceUtility.AddActivityHeader(rpc.Request);
            //    }
            //}
            //else if (TraceUtility.PropagateUserActivity || TraceUtility.ShouldPropagateActivity)
            //{
            //    TraceUtility.AddAmbientActivityToMessage(rpc.Request);
            //}
            operation.Parent.BeforeSendRequest(ref rpc);


            //Attach and transfer Activity
            //if (FxTrace.Trace.IsEnd2EndActivityTracingEnabled)
            //{
            //    TraceClientOperationPrepared(ref rpc);
            //}

            //TraceUtility.MessageFlowAtMessageSent(rpc.Request, rpc.EventTraceActivity);

            //if (MessageLogger.LogMessagesAtServiceLevel)
            //{
            //    MessageLogger.LogMessage(ref rpc.Request, (oneway ? MessageLoggingSource.ServiceLevelSendDatagram : MessageLoggingSource.ServiceLevelSendRequest) | MessageLoggingSource.LastChance);
            //}
        }

        //private void TraceClientOperationPrepared(ref ProxyRpc rpc)
        //{
        //    //Retrieve the old id on the RPC and attach the id on the message since we have a message id now.
        //    Guid previousId = rpc.EventTraceActivity != null ? rpc.EventTraceActivity.ActivityId : Guid.Empty;
        //    EventTraceActivity requestActivity = EventTraceActivityHelper.TryExtractActivity(rpc.Request);
        //    if (requestActivity == null)
        //    {
        //        requestActivity = EventTraceActivity.GetFromThreadOrCreate();
        //        EventTraceActivityHelper.TryAttachActivity(rpc.Request, requestActivity);
        //    }
        //    rpc.EventTraceActivity = requestActivity;

        //    if (TD.ClientOperationPreparedIsEnabled())
        //    {
        //        string remoteAddress = string.Empty;
        //        if (this.RemoteAddress != null && this.RemoteAddress.Uri != null)
        //        {
        //            remoteAddress = this.RemoteAddress.Uri.AbsoluteUri;
        //        }
        //        TD.ClientOperationPrepared(rpc.EventTraceActivity,
        //                                    rpc.Action,
        //                                    this.clientRuntime.ContractName,
        //                                    remoteAddress,
        //                                    previousId);
        //    }

        //}

        internal IAsyncResult BeginCall(string action, bool oneway, ProxyOperationRuntime operation, object[] ins, AsyncCallback callback, object asyncState)
        {
            return BeginCall(action, oneway, operation, ins, operationTimeout, callback, asyncState);
        }

        internal IAsyncResult BeginCall(string action, bool oneway, ProxyOperationRuntime operation, object[] ins, TimeSpan timeout, AsyncCallback callback, object asyncState)
        {
            var helper = new TimeoutHelper(operationTimeout);
            return BeginCallAsync(action, oneway, operation, ins, helper.GetCancellationToken()).ToApm(callback, asyncState);
        }

        internal async Task<ProxyRpc> BeginCallAsync(string action, bool oneway, ProxyOperationRuntime operation, object[] ins, CancellationToken token)
        {
            ThrowIfIdleAborted(operation);
            ThrowIfIsConnectionOpened(operation);

            ProxyRpc rpc = new ProxyRpc(this, operation, action, ins, token);

            PrepareCall(operation, oneway, ref rpc);

            if (!explicitlyOpened)
            {
                await EnsureOpenedAsync(token);
            }
            else
            {
                ThrowIfOpening();
                ThrowIfDisposedOrNotOpen();
            }

            try
            {
                ConcurrencyBehavior.UnlockInstanceBeforeCallout(OperationContext.Current);

                if (oneway)
                {
                    await binder.SendAsync(rpc.Request, rpc.CancellationToken);
                }
                else
                {
                    rpc.Reply = await binder.RequestAsync(rpc.Request, rpc.CancellationToken);

                    if (rpc.Reply == null)
                    {
                        ThrowIfFaulted();
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CommunicationException(SR.SFxServerDidNotReply));
                    }
                }
            }
            finally
            {
                CompletedIOOperation();
                CallOnceManager.SignalNextIfNonNull(autoOpenManager);
                await ConcurrencyBehavior.LockInstanceAfterCalloutAsync(OperationContext.Current);
            }

            return rpc;
        }


        internal Task<object> CallAsync(string action, bool oneway, ProxyOperationRuntime operation, object[] ins, object[] outs)
        {
            var helper = new TimeoutHelper(operationTimeout);
            return CallAsync(action, oneway, operation, ins, outs, helper.GetCancellationToken());
        }

        internal async Task<object> CallAsync(string action, bool oneway, ProxyOperationRuntime operation, object[] ins, object[] outs, CancellationToken token)
        {
            ThrowIfIdleAborted(operation);
            ThrowIfIsConnectionOpened(operation);

            ProxyRpc rpc = new ProxyRpc(this, operation, action, ins, token);

            //TraceServiceChannelCallStart(rpc.EventTraceActivity, true);

            //using (rpc.Activity = DiagnosticUtility.ShouldUseActivity ? ServiceModelActivity.CreateBoundedActivity() : null)
            //{
            //    if (DiagnosticUtility.ShouldUseActivity)
            //    {
            //        ServiceModelActivity.Start(rpc.Activity, SR.Format(SR.ActivityProcessAction, action), ActivityType.ProcessAction);
            //    }

            PrepareCall(operation, oneway, ref rpc);

            if (!explicitlyOpened)
            {
                await EnsureOpenedAsync(token);
            }
            else
            {
                ThrowIfOpening();
                ThrowIfDisposedOrNotOpen();
            }

            try
            {
                ConcurrencyBehavior.UnlockInstanceBeforeCallout(OperationContext.Current);

                if (oneway)
                {
                    await binder.SendAsync(rpc.Request, rpc.CancellationToken);
                }
                else
                {
                    rpc.Reply = await binder.RequestAsync(rpc.Request, rpc.CancellationToken);

                    if (rpc.Reply == null)
                    {
                        ThrowIfFaulted();
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CommunicationException(SR.SFxServerDidNotReply));
                    }
                }
            }
            finally
            {
                CompletedIOOperation();
                CallOnceManager.SignalNextIfNonNull(autoOpenManager);
                await ConcurrencyBehavior.LockInstanceAfterCalloutAsync(OperationContext.Current);
            }

            rpc.OutputParameters = outs;
            HandleReply(operation, ref rpc);
            //}
            return rpc.ReturnValue;
        }

        internal object EndCall(string action, object[] outs, IAsyncResult result)
        {
            var rpc = result.ToApmEnd<ProxyRpc>();
            rpc.OutputParameters = outs;
            HandleReply(rpc.Operation, ref rpc);
            return rpc.ReturnValue;
        }

        internal void DecrementActivity()
        {
            int updatedActivityCount = Interlocked.Decrement(ref activityCount);

            if (!((updatedActivityCount >= 0)))
            {
                throw Fx.AssertAndThrowFatal("ServiceChannel.DecrementActivity: (updatedActivityCount >= 0)");
            }

            if (updatedActivityCount == 0 && autoClose)
            {
                try
                {
                    if (State == CommunicationState.Opened)
                    {
                        // TODO: Async
                        var helper = new TimeoutHelper(CloseTimeout);
                        CloseAsync(helper.GetCancellationToken()).GetAwaiter().GetResult();
                    }
                }
                catch (CommunicationException e)
                {
                    DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                }
                catch (TimeoutException e)
                {
                    //if (TD.CloseTimeoutIsEnabled())
                    //{
                    //    TD.CloseTimeout(e.Message);
                    //}
                    DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                }
                catch (ObjectDisposedException e)
                {
                    DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                }
                catch (InvalidOperationException e)
                {
                    DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                }
            }
        }

        internal void FireUnknownMessageReceived(Message message)
        {
            EventHandler<UnknownMessageReceivedEventArgs> handler = unknownMessageReceived;
            if (handler != null)
                handler(proxy, new UnknownMessageReceivedEventArgs(message));
        }

        TimeoutException GetOpenTimeoutException(TimeSpan timeout)
        {
            EndpointAddress address = RemoteAddress ?? LocalAddress;
            if (address != null)
            {
                return new TimeoutException(SR.Format(SR.TimeoutServiceChannelConcurrentOpen2, address, timeout));
            }
            else
            {
                return new TimeoutException(SR.Format(SR.TimeoutServiceChannelConcurrentOpen1, timeout));
            }
        }

        internal void HandleReceiveComplete(RequestContext context)
        {
            if (context == null && HasSession)
            {
                bool first;
                lock (ThisLock)
                {
                    first = !doneReceiving;
                    doneReceiving = true;
                }

                if (first)
                {
                    DispatchRuntime dispatchBehavior = ClientRuntime.DispatchRuntime;
                    if (dispatchBehavior != null)
                        dispatchBehavior.GetRuntime().InputSessionDoneReceiving(this);

                    DecrementActivity();
                }
            }
        }

        void HandleReply(ProxyOperationRuntime operation, ref ProxyRpc rpc)
        {
            try
            {
                //set the ID after response
                //if (TraceUtility.MessageFlowTracingOnly && rpc.ActivityId != Guid.Empty)
                //{
                //    System.Runtime.Diagnostics.DiagnosticTraceBase.ActivityId = rpc.ActivityId;
                //}

                if (rpc.Reply != null)
                {
                    //TraceUtility.MessageFlowAtMessageReceived(rpc.Reply, null, rpc.EventTraceActivity, false);

                    //if (MessageLogger.LogMessagesAtServiceLevel)
                    //{
                    //    MessageLogger.LogMessage(ref rpc.Reply, MessageLoggingSource.ServiceLevelReceiveReply | MessageLoggingSource.LastChance);
                    //}
                    operation.Parent.AfterReceiveReply(ref rpc);

                    if ((operation.ReplyAction != MessageHeaders.WildcardAction) && !rpc.Reply.IsFault && rpc.Reply.Headers.Action != null)
                    {
                        if (string.CompareOrdinal(operation.ReplyAction, rpc.Reply.Headers.Action) != 0)
                        {
                            Exception error = new ProtocolException(SR.Format(SR.SFxReplyActionMismatch3, operation.Name,
                                                                                  rpc.Reply.Headers.Action,
                                                                                  operation.ReplyAction));
                            TerminateIfNecessary(ref rpc);
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(error);
                        }
                    }
                    if (operation.DeserializeReply && clientRuntime.IsFault(ref rpc.Reply))
                    {
                        MessageFault fault = MessageFault.CreateFault(rpc.Reply, clientRuntime.MaxFaultSize);
                        string action = rpc.Reply.Headers.Action;
                        if (action == rpc.Reply.Version.Addressing.DefaultFaultAction)
                        {
                            action = null;
                        }
                        ThrowIfFaultUnderstood(rpc.Reply, fault, action, rpc.Reply.Version, rpc.Channel.GetProperty<FaultConverter>());
                        FaultException fe = rpc.Operation.FaultFormatter.Deserialize(fault, action);
                        TerminateIfNecessary(ref rpc);
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(fe);
                    }

                    operation.AfterReply(ref rpc);
                }
            }
            finally
            {
                if (operation.SerializeRequest)
                {
                    rpc.Request.Close();
                }

                OperationContext operationContext = OperationContext.Current;
                bool consumed = ((rpc.Reply != null) && (rpc.Reply.State != MessageState.Created));

                if ((operationContext != null) && operationContext.IsUserContext)
                {
                    operationContext.SetClientReply(rpc.Reply, consumed);
                }
                else if (consumed)
                {
                    rpc.Reply.Close();
                }

                //if (TraceUtility.MessageFlowTracingOnly)
                //{
                //    if (rpc.ActivityId != Guid.Empty)
                //    {
                //        //reset the ID as it was created internally - ensures each call is uniquely correlatable
                //        System.Runtime.Diagnostics.DiagnosticTraceBase.ActivityId = Guid.Empty;
                //        rpc.ActivityId = Guid.Empty;
                //    }
                //}
            }
            TerminateIfNecessary(ref rpc);

            //if (TD.ServiceChannelCallStopIsEnabled())
            //{
            //    string remoteAddress = string.Empty;
            //    if (this.RemoteAddress != null && this.RemoteAddress.Uri != null)
            //    {
            //        remoteAddress = this.RemoteAddress.Uri.AbsoluteUri;
            //    }
            //    TD.ServiceChannelCallStop(rpc.EventTraceActivity, rpc.Action,
            //                                this.clientRuntime.ContractName,
            //                                remoteAddress);
            //}

        }

        void TerminateIfNecessary(ref ProxyRpc rpc)
        {
            if (rpc.Operation.IsTerminating)
            {
                terminatingOperationName = rpc.Operation.Name;
                TerminatingOperationBehavior.AfterReply(ref rpc);
            }
        }

        void ThrowIfFaultUnderstood(Message reply, MessageFault fault, string action, MessageVersion version, FaultConverter faultConverter)
        {
            Exception exception;
            if (faultConverter != null && faultConverter.TryCreateException(reply, fault, out exception))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(exception);
            }

            bool checkSender;
            bool checkReceiver;
            FaultCode code;

            if (version.Envelope == EnvelopeVersion.Soap11)
            {
                checkSender = true;
                checkReceiver = true;
                code = fault.Code;
            }
            else
            {
                checkSender = fault.Code.IsSenderFault;
                checkReceiver = fault.Code.IsReceiverFault;
                code = fault.Code.SubCode;
            }

            if (code == null)
            {
                return;
            }

            if (code.Namespace == null)
            {
                return;
            }

            if (checkSender)
            {
                if (string.Compare(code.Namespace, FaultCodeConstants.Namespaces.NetDispatch, StringComparison.Ordinal) == 0)
                {
                    if (string.Compare(code.Name, FaultCodeConstants.Codes.SessionTerminated, StringComparison.Ordinal) == 0)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new ChannelTerminatedException(fault.Reason.GetMatchingTranslation(CultureInfo.CurrentCulture).Text));
                    }

                    //if (string.Compare(code.Name, FaultCodeConstants.Codes.TransactionAborted, StringComparison.Ordinal) == 0)
                    //{
                    //    throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new ProtocolException(fault.Reason.GetMatchingTranslation(CultureInfo.CurrentCulture).Text));
                    //}
                }

                // throw SecurityAccessDeniedException explicitly
                // MessageSecurity
                //if (string.Compare(code.Namespace, SecurityVersion.Default.HeaderNamespace.Value, StringComparison.Ordinal) == 0)
                //{
                //    if (string.Compare(code.Name, SecurityVersion.Default.FailedAuthenticationFaultCode.Value, StringComparison.Ordinal) == 0)
                //    {
                //        throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new SecurityAccessDeniedException(fault.Reason.GetMatchingTranslation(CultureInfo.CurrentCulture).Text));
                //    }
                //}
            }

            if (checkReceiver)
            {
                if (string.Compare(code.Namespace, FaultCodeConstants.Namespaces.NetDispatch, StringComparison.Ordinal) == 0)
                {
                    if (string.Compare(code.Name, FaultCodeConstants.Codes.InternalServiceFault, StringComparison.Ordinal) == 0)
                    {
                        if (HasSession)
                        {
                            Fault();
                        }
                        if (fault.HasDetail)
                        {
                            ExceptionDetail detail = fault.GetDetail<ExceptionDetail>();
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new FaultException<ExceptionDetail>(detail, fault.Reason, fault.Code, action));
                        }
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new FaultException(fault, action));
                    }
                    if (string.Compare(code.Name, FaultCodeConstants.Codes.DeserializationFailed, StringComparison.Ordinal) == 0)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new ProtocolException(
                            fault.Reason.GetMatchingTranslation(CultureInfo.CurrentCulture).Text));
                    }
                }
            }
        }

        void ThrowIfIdleAborted(ProxyOperationRuntime operation)
        {
            if (idleManager != null && idleManager.DidIdleAbort)
            {
                string text = SR.Format(SR.SFxServiceChannelIdleAborted, operation.Name);
                Exception error = new CommunicationObjectAbortedException(text);
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(error);
            }
        }

        void ThrowIfIsConnectionOpened(ProxyOperationRuntime operation)
        {
            if (operation.IsSessionOpenNotificationEnabled)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                    SR.Format(SR.SFxServiceChannelCannotBeCalledBecauseIsSessionOpenNotificationEnabled, operation.Name, "Action", OperationDescription.SessionOpenedAction, "Open")));
            }
        }

        void ThrowIfOpening()
        {
            if (State == CommunicationState.Opening)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SFxCannotCallAutoOpenWhenExplicitOpenCalled));
            }
        }

        internal void IncrementActivity()
        {
            Interlocked.Increment(ref activityCount);
        }

        void OnInnerChannelFaulted(object sender, EventArgs e)
        {
            Fault();

            if (HasSession)
            {
                DispatchRuntime dispatchRuntime = ClientRuntime.DispatchRuntime;
                if (dispatchRuntime != null)
                {
                    dispatchRuntime.GetRuntime().InputSessionFaulted(this);
                }
            }

            if (autoClose)
            {
                Abort();
            }
        }

        void AddMessageProperties(Message message, OperationContext context)
        {
            if (allowOutputBatching)
            {
                message.Properties.AllowOutputBatching = true;
            }

            if (context != null && context.InternalServiceChannel == this)
            {
                if (!context.OutgoingMessageVersion.IsMatch(message.Headers.MessageVersion))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                        SR.Format(SR.SFxVersionMismatchInOperationContextAndMessage2, context.OutgoingMessageVersion, message.Headers.MessageVersion)
                        ));
                }

                if (context.HasOutgoingMessageHeaders)
                {
                    message.Headers.CopyHeadersFrom(context.OutgoingMessageHeaders);
                }

                if (context.HasOutgoingMessageProperties)
                {
                    message.Properties.CopyProperties(context.OutgoingMessageProperties);
                }
            }
        }

        #region IChannel Members
        public Task SendAsync(Message message)
        {
            var helper = new TimeoutHelper(OperationTimeout);
            return SendAsync(message, helper.GetCancellationToken());
        }

        public Task SendAsync(Message message, CancellationToken token)
        {
            ProxyOperationRuntime operation = UnhandledProxyOperation;
            return CallAsync(message.Headers.Action, true, operation, new object[] { message }, Array.Empty<object>(), token);
        }

        public Task<Message> RequestAsync(Message message)
        {
            var helper = new TimeoutHelper(OperationTimeout);
            return RequestAsync(message, helper.GetCancellationToken());
        }

        public async Task<Message> RequestAsync(Message message, CancellationToken token)
        {
            ProxyOperationRuntime operation = UnhandledProxyOperation;
            return (Message)await CallAsync(message.Headers.Action, false, operation, new object[] { message }, Array.Empty<object>(), token);
        }

        protected override void OnAbort()
        {
            if (idleManager != null)
            {
                idleManager.CancelTimer();
            }

            binder.Abort();

            CleanupChannelCollections();

            //ServiceThrottle serviceThrottle = this.serviceThrottle;
            //if (serviceThrottle != null)
            //    serviceThrottle.DeactivateChannel();

            //rollback the attached transaction if one is present
            //if ((this.instanceContext != null) && this.HasSession)
            //{
            //    if (instanceContext.HasTransaction)
            //    {
            //        instanceContext.Transaction.CompletePendingTransaction(instanceContext.Transaction.Attached, new Exception()); // error!=null forces Tx rollback
            //    }
            //}
        }

        protected override async Task OnCloseAsync(CancellationToken token)
        {
            if (idleManager != null)
            {
                idleManager.CancelTimer();
            }

            //if (this.InstanceContext != null && this.InstanceContext.HasTransaction)
            //{
            //    this.InstanceContext.CompleteAttachedTransaction();
            //}

            if (closeBinder)
                await InnerChannel.CloseAsync(token);

            CleanupChannelCollections();

            //ServiceThrottle serviceThrottle = this.serviceThrottle;
            //if (serviceThrottle != null)
            //{
            //    serviceThrottle.DeactivateChannel();
            //}
        }

        protected override async Task OnOpenAsync(CancellationToken token)
        {
            if (autoOpenManager == null)
            {
                explicitlyOpened = true;
            }

            //this.TraceChannelOpenStarted();

            if (openBinder)
            {
                await InnerChannel.OpenAsync(token);
            }

            CompletedIOOperation();

            //this.TraceChannelOpenCompleted();
        }

        void CleanupChannelCollections()
        {
            if (!hasCleanedUpChannelCollections)
            {
                lock (ThisLock)
                {
                    if (!hasCleanedUpChannelCollections)
                    {
                        if (InstanceContext != null)
                        {
                            InstanceContext.OutgoingChannels.Remove((IChannel)proxy);
                        }

                        hasCleanedUpChannelCollections = true;
                    }
                }
            }
        }
        #endregion

        #region IClientChannel Members

        bool IDuplexContextChannel.AutomaticInputSessionShutdown
        {
            get { return autoClose; }
            set { autoClose = value; }
        }

        //bool IContextChannel.AllowOutputBatching
        //{
        //    get { return this.allowOutputBatching; }
        //    set { this.allowOutputBatching = value; }
        //}

        Task IDuplexContextChannel.CloseOutputSessionAsync(CancellationToken token)
        {
            return GetDuplexSessionOrThrow().CloseOutputSessionAsync(token);
        }

        IDuplexSession GetDuplexSessionOrThrow()
        {
            if (InnerChannel == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.channelIsNotAvailable0));
            }

            ISessionChannel<IDuplexSession> duplexSessionChannel = InnerChannel as ISessionChannel<IDuplexSession>;
            if (duplexSessionChannel == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.channelDoesNotHaveADuplexSession0));
            }

            return duplexSessionChannel.Session;
        }

        IExtensionCollection<IContextChannel> IExtensibleObject<IContextChannel>.Extensions
        {
            get
            {
                lock (ThisLock)
                {
                    if (extensions == null)
                        extensions = new ExtensionCollection<IContextChannel>((IContextChannel)Proxy, ThisLock);
                    return extensions;
                }
            }
        }

        InstanceContext IDuplexContextChannel.CallbackInstance
        {
            get { return instanceContext; }
            set
            {
                lock (ThisLock)
                {
                    if (instanceContext != null)
                    {
                        instanceContext.OutgoingChannels.Remove((IChannel)proxy);
                    }

                    instanceContext = value;

                    if (instanceContext != null)
                    {
                        instanceContext.OutgoingChannels.Add((IChannel)proxy);
                    }
                }
            }
        }

        IInputSession IContextChannel.InputSession
        {
            get
            {
                if (InnerChannel != null)
                {
                    ISessionChannel<IInputSession> inputSession = InnerChannel as ISessionChannel<IInputSession>;
                    if (inputSession != null)
                        return inputSession.Session;

                    ISessionChannel<IDuplexSession> duplexSession = InnerChannel as ISessionChannel<IDuplexSession>;
                    if (duplexSession != null)
                        return duplexSession.Session;
                }

                return null;
            }
        }

        IOutputSession IContextChannel.OutputSession
        {
            get
            {
                if (InnerChannel != null)
                {
                    ISessionChannel<IOutputSession> outputSession = InnerChannel as ISessionChannel<IOutputSession>;
                    if (outputSession != null)
                        return outputSession.Session;

                    ISessionChannel<IDuplexSession> duplexSession = InnerChannel as ISessionChannel<IDuplexSession>;
                    if (duplexSession != null)
                        return duplexSession.Session;
                }

                return null;
            }
        }

        string IContextChannel.SessionId
        {
            get
            {
                if (InnerChannel != null)
                {
                    ISessionChannel<IInputSession> inputSession = InnerChannel as ISessionChannel<IInputSession>;
                    if (inputSession != null)
                        return inputSession.Session.Id;

                    ISessionChannel<IOutputSession> outputSession = InnerChannel as ISessionChannel<IOutputSession>;
                    if (outputSession != null)
                        return outputSession.Session.Id;

                    ISessionChannel<IDuplexSession> duplexSession = InnerChannel as ISessionChannel<IDuplexSession>;
                    if (duplexSession != null)
                        return duplexSession.Session.Id;
                }

                return null;
            }
        }

        IServiceChannelDispatcher IChannel.ChannelDispatcher
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        event EventHandler<UnknownMessageReceivedEventArgs> IClientChannel.UnknownMessageReceived
        {
            add
            {
                lock (ThisLock)
                {
                    unknownMessageReceived += value;
                }
            }
            remove
            {
                lock (ThisLock)
                {
                    unknownMessageReceived -= value;
                }
            }
        }

        void IDisposable.Dispose()
        {
            CloseAsync().GetAwaiter().GetResult();
        }

        #endregion

        //void TraceChannelOpenStarted()
        //{
        //    if (TD.ClientChannelOpenStartIsEnabled() && this.endpointDispatcher == null)
        //    {
        //        TD.ClientChannelOpenStart(this.EventActivity);
        //    }
        //    else if (TD.ServiceChannelOpenStartIsEnabled())
        //    {
        //        TD.ServiceChannelOpenStart(this.EventActivity);
        //    }

        //    //if (DiagnosticUtility.ShouldTraceInformation)
        //    //{
        //    //    Dictionary<string, string> values = new Dictionary<string, string>(4);
        //    //    bool traceNeeded = false;
        //    //    DispatchRuntime behavior = this.DispatchRuntime;
        //    //    if (behavior != null)
        //    //    {
        //    //        if (behavior.Type != null)
        //    //        {
        //    //            values["ServiceType"] = behavior.Type.AssemblyQualifiedName;
        //    //        }
        //    //        values["ContractNamespace"] = this.clientRuntime.ContractNamespace;
        //    //        values["ContractName"] = this.clientRuntime.ContractName;
        //    //        traceNeeded = true;
        //    //    }
        //    //    if ((this.endpointDispatcher != null) && (this.endpointDispatcher.ListenUri != null))
        //    //    {
        //    //        values["Uri"] = this.endpointDispatcher.ListenUri.ToString();
        //    //        traceNeeded = true;
        //    //    }
        //    //    if (traceNeeded)
        //    //    {
        //    //        TraceUtility.TraceEvent(TraceEventType.Information, TraceCode.ServiceChannelLifetime,
        //    //            SR.Format(SR.TraceCodeServiceChannelLifetime),
        //    //            new DictionaryTraceRecord(values), this, null);
        //    //    }
        //    //}
        //}

        //void TraceChannelOpenCompleted()
        //{
        //    if (this.endpointDispatcher == null && TD.ClientChannelOpenStopIsEnabled())
        //    {
        //        TD.ClientChannelOpenStop(this.EventActivity);
        //    }
        //    else if (TD.ServiceChannelOpenStopIsEnabled())
        //    {
        //        TD.ServiceChannelOpenStop(this.EventActivity);
        //    }
        //}

        //static void TraceServiceChannelCallStart(EventTraceActivity eventTraceActivity, bool isSynchronous)
        //{
        //    if (TD.ServiceChannelCallStartIsEnabled())
        //    {
        //        if (isSynchronous)
        //        {
        //            TD.ServiceChannelCallStart(eventTraceActivity);
        //        }
        //        else
        //        {
        //            TD.ServiceChannelBeginCallStart(eventTraceActivity);
        //        }
        //    }
        //}

        // Invariants for signalling the CallOnce manager.
        //
        // 1) If a Call, BeginCall, or EndCall on the channel throws,
        //    the manager will SignalNext itself.
        // 2) If a Waiter times out, it will SignalNext its manager
        //    once it is both timed out and signalled.
        // 3) Once Call or EndCall returns successfully, it guarantees
        //    that SignalNext will be called once the // next stage
        //    has sufficiently completed.
        interface ICallOnce
        {
            Task CallAsync(ServiceChannel channel, CancellationToken token);
        }

        class CallOpenOnce : ICallOnce
        {
            static CallOpenOnce instance;

            internal static CallOpenOnce Instance
            {
                get
                {
                    if (CallOpenOnce.instance == null)
                    {
                        CallOpenOnce.instance = new CallOpenOnce();
                    }
                    return CallOpenOnce.instance;
                }
            }

            Task ICallOnce.CallAsync(ServiceChannel channel, CancellationToken token)
            {
                return channel.OpenAsync(token);
            }
        }

        class CallOnceManager
        {
            readonly ICallOnce callOnce;
            readonly ServiceChannel channel;
            bool isFirst = true;
            Queue<IWaiter> queue;

            static readonly Action<object> signalWaiter = CallOnceManager.SignalWaiter;

            internal CallOnceManager(ServiceChannel channel, ICallOnce callOnce)
            {
                this.callOnce = callOnce;
                this.channel = channel;
                queue = new Queue<IWaiter>();
            }

            object ThisLock
            {
                get { return this; }
            }

            internal async Task CallOnceAsync(CancellationToken token)
            {
                AsyncWaiter waiter = null;
                bool first = false;

                if (queue != null)
                {
                    lock (ThisLock)
                    {
                        if (queue != null)
                        {
                            if (isFirst)
                            {
                                first = true;
                                isFirst = false;
                            }
                            else
                            {
                                waiter = new AsyncWaiter(this);
                                queue.Enqueue(waiter);
                            }
                        }
                    }
                }

                if (first)
                {
                    bool throwing = true;
                    try
                    {
                        await callOnce.CallAsync(channel, token);
                        throwing = false;
                    }
                    finally
                    {
                        if (throwing)
                        {
                            SignalNext();
                        }
                    }
                }
                else if (waiter != null)
                {
                    await waiter.WaitAsync(token);
                }
            }

            static internal void SignalNextIfNonNull(CallOnceManager manager)
            {
                if (manager != null)
                {
                    manager.SignalNext();
                }
            }

            internal void SignalNext()
            {
                if (queue == null)
                {
                    return;
                }

                IWaiter waiter = null;

                lock (ThisLock)
                {
                    if (queue != null)
                    {
                        if (queue.Count > 0)
                        {
                            waiter = queue.Dequeue();
                        }
                        else
                        {
                            queue = null;
                        }
                    }
                }

                if (waiter != null)
                {
                    ActionItem.Schedule(CallOnceManager.signalWaiter, waiter);
                }
            }

            static void SignalWaiter(object state)
            {
                ((IWaiter)state).Signal();
            }

            interface IWaiter
            {
                void Signal();
            }

            class AsyncWaiter : IWaiter
            {
                readonly AsyncManualResetEvent wait = new AsyncManualResetEvent();
                readonly CallOnceManager manager;
                bool isTimedOut = false;
                bool isSignaled = false;
                int waitCount = 0;

                internal AsyncWaiter(CallOnceManager manager)
                {
                    this.manager = manager;
                }

                bool ShouldSignalNext
                {
                    get { return isTimedOut && isSignaled; }
                }

                void IWaiter.Signal()
                {
                    wait.Set();
                    CloseWaitHandle();

                    bool signalNext;
                    lock (manager.ThisLock)
                    {
                        isSignaled = true;
                        signalNext = ShouldSignalNext;
                    }
                    if (signalNext)
                    {
                        manager.SignalNext();
                    }
                }

                internal async Task<bool> WaitAsync(CancellationToken token)
                {
                    try
                    {
                        if (!await wait.WaitAsync(token))
                        {
                            bool signalNext;
                            lock (manager.ThisLock)
                            {
                                isTimedOut = true;
                                signalNext = ShouldSignalNext;
                            }
                            if (signalNext)
                            {
                                manager.SignalNext();
                            }
                        }
                    }
                    finally
                    {
                        CloseWaitHandle();
                    }

                    return !isTimedOut;
                }

                void CloseWaitHandle()
                {
                    if (Interlocked.Increment(ref waitCount) == 2)
                    {
                        wait.Dispose();
                    }
                }
            }
        }

        internal class SessionIdleManager
        {
            IChannelBinder binder;
            ServiceChannel channel;
            long idleTicks;
            long lastActivity;
            IOThreadTimer timer;
            static Action<object> timerCallback;
            bool didIdleAbort;
            bool isTimerCancelled;
            object thisLock;
            bool? isNeeded = null;

            public SessionIdleManager() { }

            internal SessionIdleManager UseIfNeeded(IChannelBinder binder, TimeSpan idle)
            {
                if (isNeeded.HasValue)
                {
                    return isNeeded.Value ? this : null;
                }

                if (binder.HasSession && (idle != TimeSpan.MaxValue))
                {
                    this.binder = binder;
                    timer = new IOThreadTimer(GetTimerCallback(), this, false);
                    idleTicks = Ticks.FromTimeSpan(idle);
                    timer.SetAt(Ticks.Now + this.idleTicks);
                    thisLock = new object();
                    isNeeded = true;
                    return this;
                }
                else
                {
                    isNeeded = false;
                    return null;
                }
            }

            internal bool DidIdleAbort
            {
                get
                {
                    lock (thisLock)
                    {
                        return didIdleAbort;
                    }
                }
            }

            internal void CancelTimer()
            {
                lock (thisLock)
                {
                    isTimerCancelled = true;
                    timer.Cancel();
                }
            }

            internal void CompletedActivity()
            {
                Interlocked.Exchange(ref lastActivity, Ticks.Now);
            }

            internal void RegisterChannel(ServiceChannel channel, out bool didIdleAbort)
            {
                lock (thisLock)
                {
                    this.channel = channel;
                    didIdleAbort = this.didIdleAbort;
                }
            }

            static Action<object> GetTimerCallback()
            {
                if (SessionIdleManager.timerCallback == null)
                {
                    SessionIdleManager.timerCallback = SessionIdleManager.TimerCallback;
                }
                return SessionIdleManager.timerCallback;
            }

            static void TimerCallback(object state)
            {
                ((SessionIdleManager)state).TimerCallback();
            }

            void TimerCallback()
            {
                // This reads lastActivity atomically without changing its value.
                // (it only sets if it is zero, and then it sets it to zero).
                long last = Interlocked.CompareExchange(ref lastActivity, 0, 0);
                long abortTime = last + idleTicks;

                lock (thisLock)
                {
                    long ticksNow = Ticks.Now;
                    if (ticksNow > abortTime)
                    {
                        didIdleAbort = true;
                        if (channel != null)
                        {
                            channel.Abort();
                        }
                        else
                        {
                            binder.Abort();
                        }
                    }
                    else
                    {
                        if (!isTimerCancelled && binder.Channel.State != CommunicationState.Faulted && binder.Channel.State != CommunicationState.Closed)
                        {
                            timer.SetAt(abortTime);
                        }
                    }
                }
            }
        }
    }

}