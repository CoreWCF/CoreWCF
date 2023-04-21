// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using CoreWCF.Description;
using CoreWCF.Dispatcher;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    // This class is sealed because the constructor could call Abort, which is virtual
    internal sealed class ServiceChannel : CommunicationObject, IChannel, IClientChannel, IDuplexContextChannel, IOutputChannel, IRequestChannel, IServiceChannel
    {
        private int _activityCount = 0;
        private readonly bool _allowOutputBatching = false;
        private bool _autoClose = true;

        //CallOnceManager autoDisplayUIManager;
        private CallOnceManager _autoOpenManager;
        private readonly bool _closeBinder = true;
        private bool _doneReceiving;
        private EndpointDispatcher _endpointDispatcher;
        private bool _explicitlyOpened;
        private ExtensionCollection<IContextChannel> _extensions;
        private readonly SessionIdleManager _idleManager;
        private EndpointAddress _localAddress;
        private readonly bool _openBinder = false;
        private TimeSpan _operationTimeout;
        private object _proxy;
        private ServiceThrottle _serviceThrottle;
        private string _terminatingOperationName;
        private bool _hasCleanedUpChannelCollections;

        //EventTraceActivity eventActivity;
        private readonly IDefaultCommunicationTimeouts _timeouts;
        private EventHandler<UnknownMessageReceivedEventArgs> _unknownMessageReceived;

        private ServiceChannel(IChannelBinder binder, Binding binding)
        {
            MessageVersion = binding.MessageVersion;
            Binder = binder ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(binder));
            IsReplyChannel = Binder.Channel is IReplyChannel;

            IChannel innerChannel = binder.Channel;
            HasSession = (innerChannel is ISessionChannel<IDuplexSession>) ||
                        (innerChannel is ISessionChannel<IInputSession>) ||
                        (innerChannel is ISessionChannel<IOutputSession>);

            IncrementActivity();
            _openBinder = (binder.Channel.State == CommunicationState.Created);

            _operationTimeout = binding.SendTimeout;
            _timeouts = binding;
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
            ChannelDispatcher = serviceDispatcher.ChannelDispatcher;
            _endpointDispatcher = endpointDispatcher ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(endpointDispatcher));
            ClientRuntime = endpointDispatcher.DispatchRuntime.CallbackClientRuntime;

            SetupInnerChannelFaultHandler();

            _autoClose = endpointDispatcher.DispatchRuntime.AutomaticInputSessionShutdown;
            IsPending = true;

            _idleManager = idleManager;

            if (!binder.HasSession)
            {
                _closeBinder = false;
            }

            if (_idleManager != null)
            {
                _idleManager.RegisterChannel(this, out bool didIdleAbort);
                if (didIdleAbort)
                {
                    Abort();
                }
            }
        }

        private CallOnceManager AutoOpenManager
        {
            get
            {
                if (!_explicitlyOpened && (_autoOpenManager == null))
                {
                    EnsureAutoOpenManagers();
                }
                return _autoOpenManager;
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

        internal bool CloseFactory { get; set; }

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
                if (_endpointDispatcher != null)
                {
                    return _endpointDispatcher.DispatchRuntime;
                }
                if (ClientRuntime != null)
                {
                    return ClientRuntime.DispatchRuntime;
                }
                return null;
            }
        }

        internal MessageVersion MessageVersion { get; }

        internal IChannelBinder Binder { get; }

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
                return _timeouts.CloseTimeout;
                //}
            }
        }

        internal ChannelDispatcher ChannelDispatcher { get; }

        internal EndpointDispatcher EndpointDispatcher
        {
            get { return _endpointDispatcher; }
            set
            {
                lock (ThisLock)
                {
                    _endpointDispatcher = value;
                    ClientRuntime = value.DispatchRuntime.CallbackClientRuntime;
                }
            }
        }

        //internal ServiceChannelFactory Factory
        //{
        //    get { return this.factory; }
        //}

        internal IChannel InnerChannel
        {
            get { return Binder.Channel; }
        }

        internal bool IsPending { get; set; }

        internal bool HasSession { get; }

        internal bool IsReplyChannel { get; }

        public Uri ListenUri
        {
            get
            {
                return Binder.ListenUri;
            }
        }

        public EndpointAddress LocalAddress
        {
            get
            {
                if (_localAddress == null)
                {
                    if (_endpointDispatcher != null)
                    {
                        _localAddress = _endpointDispatcher.EndpointAddress;
                    }
                    else
                    {
                        _localAddress = Binder.LocalAddress;
                    }
                }
                return _localAddress;
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
            get { return _operationTimeout; }
            set
            {
                if (value < TimeSpan.Zero)
                {
                    string message = SRCommon.SFxTimeoutOutOfRange0;
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value, message));
                }
                if (TimeoutHelper.IsTooLarge(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value, SRCommon.SFxTimeoutOutOfRangeTooBig));
                }


                _operationTimeout = value;
            }
        }

        internal object Proxy
        {
            get
            {
                object proxy = _proxy;
                if (proxy != null)
                {
                    return proxy;
                }
                else
                {
                    return this;
                }
            }
            set
            {
                _proxy = value;
                EventSender = value;   // need to use "proxy" as open/close event source
            }
        }

        internal ClientRuntime ClientRuntime { get; private set; }

        public EndpointAddress RemoteAddress
        {
            get
            {
                if (InnerChannel is IOutputChannel outputChannel)
                {
                    return outputChannel.RemoteAddress;
                }

                if (InnerChannel is IRequestChannel requestChannel)
                {
                    return requestChannel.RemoteAddress;
                }

                return null;
            }
        }

        private ProxyOperationRuntime UnhandledProxyOperation
        {
            get { return ClientRuntime.GetRuntime().UnhandledProxyOperation; }
        }

        public Uri Via
        {
            get
            {
                if (InnerChannel is IOutputChannel outputChannel)
                {
                    return outputChannel.Via;
                }

                if (InnerChannel is IRequestChannel requestChannel)
                {
                    return requestChannel.Via;
                }

                return null;
            }
        }

        internal InstanceContext InstanceContext { get; set; }

        internal ServiceThrottle InstanceContextServiceThrottle { get; set; }

        internal ServiceThrottle ServiceThrottle
        {
            get { return _serviceThrottle; }
            set
            {
                ThrowIfDisposed();
                _serviceThrottle = value;
            }
        }

        private void SetupInnerChannelFaultHandler()
        {
            // need to call this method after this.binder and this.clientRuntime are set to prevent a potential 
            // NullReferenceException in this method or in the OnInnerChannelFaulted method; 
            // because this method accesses this.binder and OnInnerChannelFaulted accesses this.clientRuntime.
            Binder.Channel.Faulted += OnInnerChannelFaulted;
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
            {
                return true;
            }

            if (t.IsAssignableFrom(typeof(IDuplexContextChannel)))
            {
                return InnerChannel is IDuplexChannel;
            }

            if (t.IsAssignableFrom(typeof(IServiceChannel)))
            {
                return true;
            }

            return false;
        }

        internal void CompletedIOOperation()
        {
            if (_idleManager != null)
            {
                _idleManager.CompletedActivity();
            }
        }

        private void EnsureAutoOpenManagers()
        {
            lock (ThisLock)
            {
                if (!_explicitlyOpened)
                {
                    if (_autoOpenManager == null)
                    {
                        _autoOpenManager = new CallOnceManager(this, CallOpenOnce.Instance);
                    }
                }
            }
        }

        private async Task EnsureOpenedAsync(CancellationToken token)
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
            {
                return innerChannel.GetProperty<T>();
            }

            return null;
        }

        private void PrepareCall(ProxyOperationRuntime operation, bool oneway, ref ProxyRpc rpc)
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

            if (_terminatingOperationName != null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxTerminatingOperationAlreadyCalled1, _terminatingOperationName)));
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
            return BeginCall(action, oneway, operation, ins, _operationTimeout, callback, asyncState);
        }

        internal IAsyncResult BeginCall(string action, bool oneway, ProxyOperationRuntime operation, object[] ins, TimeSpan timeout, AsyncCallback callback, object asyncState)
        {
            var helper = new TimeoutHelper(_operationTimeout);
            return BeginCallAsync(action, oneway, operation, ins, helper.GetCancellationToken()).ToApm(callback, asyncState);
        }

        internal async Task<ProxyRpc> BeginCallAsync(string action, bool oneway, ProxyOperationRuntime operation, object[] ins, CancellationToken token)
        {
            ThrowIfIdleAborted(operation);
            ThrowIfIsConnectionOpened(operation);

            ProxyRpc rpc = new ProxyRpc(this, operation, action, ins, token);

            PrepareCall(operation, oneway, ref rpc);

            if (!_explicitlyOpened)
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
                    await Binder.SendAsync(rpc.Request, rpc.CancellationToken);
                }
                else
                {
                    rpc.Reply = await Binder.RequestAsync(rpc.Request, rpc.CancellationToken);

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
                CallOnceManager.SignalNextIfNonNull(_autoOpenManager);
                await ConcurrencyBehavior.LockInstanceAfterCalloutAsync(OperationContext.Current);
            }

            return rpc;
        }


        internal Task<object> CallAsync(string action, bool oneway, ProxyOperationRuntime operation, object[] ins, object[] outs)
        {
            var helper = new TimeoutHelper(_operationTimeout);
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

            if (!_explicitlyOpened)
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
                    await Binder.SendAsync(rpc.Request, rpc.CancellationToken);
                }
                else
                {
                    rpc.Reply = await Binder.RequestAsync(rpc.Request, rpc.CancellationToken);

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
                CallOnceManager.SignalNextIfNonNull(_autoOpenManager);
                await ConcurrencyBehavior.LockInstanceAfterCalloutAsync(OperationContext.Current);
            }

            rpc.OutputParameters = outs;
            HandleReply(operation, ref rpc);
            //}
            return rpc.ReturnValue;
        }

        internal object EndCall(string action, object[] outs, IAsyncResult result)
        {
            ProxyRpc rpc = result.ToApmEnd<ProxyRpc>();
            rpc.OutputParameters = outs;
            HandleReply(rpc.Operation, ref rpc);
            return rpc.ReturnValue;
        }

        internal Task DecrementActivityAsync()
        {
            int updatedActivityCount = Interlocked.Decrement(ref _activityCount);

            if (!((updatedActivityCount >= 0)))
            {
                throw Fx.AssertAndThrowFatal("ServiceChannel.DecrementActivity: (updatedActivityCount >= 0)");
            }

            if (updatedActivityCount == 0 && _autoClose && State == CommunicationState.Opened)
            {
                return AutoCloseAsync();
            }

            return Task.CompletedTask;

            async Task AutoCloseAsync()
            {
                try
                {
                    var helper = new TimeoutHelper(CloseTimeout);
                    await CloseAsync(helper.GetCancellationToken());
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
            EventHandler<UnknownMessageReceivedEventArgs> handler = _unknownMessageReceived;
            if (handler != null)
            {
                handler(_proxy, new UnknownMessageReceivedEventArgs(message));
            }
        }

        private TimeoutException GetOpenTimeoutException(TimeSpan timeout)
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

        internal Task HandleReceiveCompleteAsync(RequestContext context)
        {
            if (context == null && HasSession)
            {
                bool first;
                lock (ThisLock)
                {
                    first = !_doneReceiving;
                    _doneReceiving = true;
                }

                if (first)
                {
                    DispatchRuntime dispatchBehavior = ClientRuntime.DispatchRuntime;
                    if (dispatchBehavior != null)
                    {
                        dispatchBehavior.GetRuntime().InputSessionDoneReceiving(this);
                    }

                    return DecrementActivityAsync();
                }
            }

            return Task.CompletedTask;
        }

        private void HandleReply(ProxyOperationRuntime operation, ref ProxyRpc rpc)
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
                    if (operation.DeserializeReply && ClientRuntime.IsFault(ref rpc.Reply))
                    {
                        MessageFault fault = MessageFault.CreateFault(rpc.Reply, ClientRuntime.MaxFaultSize);
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

        private void TerminateIfNecessary(ref ProxyRpc rpc)
        {
            if (rpc.Operation.IsTerminating)
            {
                _terminatingOperationName = rpc.Operation.Name;
                TerminatingOperationBehavior.AfterReply(ref rpc);
            }
        }

        private void ThrowIfFaultUnderstood(Message reply, MessageFault fault, string action, MessageVersion version, FaultConverter faultConverter)
        {
            if (faultConverter != null && faultConverter.TryCreateException(reply, fault, out Exception exception))
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

        private void ThrowIfIdleAborted(ProxyOperationRuntime operation)
        {
            if (_idleManager != null && _idleManager.DidIdleAbort)
            {
                string text = SR.Format(SR.SFxServiceChannelIdleAborted, operation.Name);
                Exception error = new CommunicationObjectAbortedException(text);
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(error);
            }
        }

        private void ThrowIfIsConnectionOpened(ProxyOperationRuntime operation)
        {
            if (operation.IsSessionOpenNotificationEnabled)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                    SR.Format(SR.SFxServiceChannelCannotBeCalledBecauseIsSessionOpenNotificationEnabled, operation.Name, "Action", OperationDescription.SessionOpenedAction, "Open")));
            }
        }

        private void ThrowIfOpening()
        {
            if (State == CommunicationState.Opening)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SFxCannotCallAutoOpenWhenExplicitOpenCalled));
            }
        }

        internal void IncrementActivity()
        {
            Interlocked.Increment(ref _activityCount);
        }

        private void OnInnerChannelFaulted(object sender, EventArgs e)
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

            if (_autoClose)
            {
                Abort();
            }
        }

        private void AddMessageProperties(Message message, OperationContext context)
        {
            if (_allowOutputBatching)
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
            if (_idleManager != null)
            {
                _idleManager.CancelTimer();
            }

            Binder.Abort();

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
            if (_idleManager != null)
            {
                _idleManager.CancelTimer();
            }

            //if (this.InstanceContext != null && this.InstanceContext.HasTransaction)
            //{
            //    this.InstanceContext.CompleteAttachedTransaction();
            //}

            if (_closeBinder)
            {
                await InnerChannel.CloseAsync(token);
            }

            CleanupChannelCollections();

            //ServiceThrottle serviceThrottle = this.serviceThrottle;
            //if (serviceThrottle != null)
            //{
            //    serviceThrottle.DeactivateChannel();
            //}
        }

        protected override async Task OnOpenAsync(CancellationToken token)
        {
            if (_autoOpenManager == null)
            {
                _explicitlyOpened = true;
            }

            //this.TraceChannelOpenStarted();

            if (_openBinder)
            {
                await InnerChannel.OpenAsync(token);
            }

            CompletedIOOperation();

            //this.TraceChannelOpenCompleted();
        }

        private void CleanupChannelCollections()
        {
            if (!_hasCleanedUpChannelCollections)
            {
                lock (ThisLock)
                {
                    if (!_hasCleanedUpChannelCollections)
                    {
                        if (InstanceContext != null)
                        {
                            InstanceContext.OutgoingChannels.Remove((IChannel)_proxy);
                        }

                        _hasCleanedUpChannelCollections = true;
                    }
                }
            }
        }
        #endregion

        #region IClientChannel Members

        bool IDuplexContextChannel.AutomaticInputSessionShutdown
        {
            get { return _autoClose; }
            set { _autoClose = value; }
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

        private IDuplexSession GetDuplexSessionOrThrow()
        {
            if (InnerChannel == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.channelIsNotAvailable0));
            }

            if (!(InnerChannel is ISessionChannel<IDuplexSession> duplexSessionChannel))
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
                    if (_extensions == null)
                    {
                        _extensions = new ExtensionCollection<IContextChannel>((IContextChannel)Proxy, ThisLock);
                    }

                    return _extensions;
                }
            }
        }

        InstanceContext IDuplexContextChannel.CallbackInstance
        {
            get { return InstanceContext; }
            set
            {
                lock (ThisLock)
                {
                    if (InstanceContext != null)
                    {
                        InstanceContext.OutgoingChannels.Remove((IChannel)_proxy);
                    }

                    InstanceContext = value;

                    if (InstanceContext != null)
                    {
                        InstanceContext.OutgoingChannels.Add((IChannel)_proxy);
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
                    if (InnerChannel is ISessionChannel<IInputSession> inputSession)
                    {
                        return inputSession.Session;
                    }

                    if (InnerChannel is ISessionChannel<IDuplexSession> duplexSession)
                    {
                        return duplexSession.Session;
                    }
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
                    if (InnerChannel is ISessionChannel<IOutputSession> outputSession)
                    {
                        return outputSession.Session;
                    }

                    if (InnerChannel is ISessionChannel<IDuplexSession> duplexSession)
                    {
                        return duplexSession.Session;
                    }
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
                    if (InnerChannel is ISessionChannel<IInputSession> inputSession)
                    {
                        return inputSession.Session.Id;
                    }

                    if (InnerChannel is ISessionChannel<IOutputSession> outputSession)
                    {
                        return outputSession.Session.Id;
                    }

                    if (InnerChannel is ISessionChannel<IDuplexSession> duplexSession)
                    {
                        return duplexSession.Session.Id;
                    }
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
                    _unknownMessageReceived += value;
                }
            }
            remove
            {
                lock (ThisLock)
                {
                    _unknownMessageReceived -= value;
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
        private interface ICallOnce
        {
            Task CallAsync(ServiceChannel channel, CancellationToken token);
        }

        private class CallOpenOnce : ICallOnce
        {
            private static CallOpenOnce s_instance;

            internal static CallOpenOnce Instance
            {
                get
                {
                    if (s_instance == null)
                    {
                        s_instance = new CallOpenOnce();
                    }
                    return s_instance;
                }
            }

            Task ICallOnce.CallAsync(ServiceChannel channel, CancellationToken token)
            {
                return channel.OpenAsync(token);
            }
        }

        private class CallOnceManager
        {
            private readonly ICallOnce _callOnce;
            private readonly ServiceChannel _channel;
            private bool _isFirst = true;
            private Queue<IWaiter> _queue;
            private static readonly Action<object> s_signalWaiter = SignalWaiter;

            internal CallOnceManager(ServiceChannel channel, ICallOnce callOnce)
            {
                _callOnce = callOnce;
                _channel = channel;
                _queue = new Queue<IWaiter>();
            }

            private object ThisLock
            {
                get { return this; }
            }

            internal async Task CallOnceAsync(CancellationToken token)
            {
                AsyncWaiter waiter = null;
                bool first = false;

                if (_queue != null)
                {
                    lock (ThisLock)
                    {
                        if (_queue != null)
                        {
                            if (_isFirst)
                            {
                                first = true;
                                _isFirst = false;
                            }
                            else
                            {
                                waiter = new AsyncWaiter(this);
                                _queue.Enqueue(waiter);
                            }
                        }
                    }
                }

                if (first)
                {
                    bool throwing = true;
                    try
                    {
                        await _callOnce.CallAsync(_channel, token);
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

            internal static void SignalNextIfNonNull(CallOnceManager manager)
            {
                if (manager != null)
                {
                    manager.SignalNext();
                }
            }

            internal void SignalNext()
            {
                if (_queue == null)
                {
                    return;
                }

                IWaiter waiter = null;

                lock (ThisLock)
                {
                    if (_queue != null)
                    {
                        if (_queue.Count > 0)
                        {
                            waiter = _queue.Dequeue();
                        }
                        else
                        {
                            _queue = null;
                        }
                    }
                }

                if (waiter != null)
                {
                    ActionItem.Schedule(s_signalWaiter, waiter);
                }
            }

            private static void SignalWaiter(object state)
            {
                ((IWaiter)state).Signal();
            }

            private interface IWaiter
            {
                void Signal();
            }

            private class AsyncWaiter : IWaiter
            {
                private readonly AsyncManualResetEvent _wait = new AsyncManualResetEvent();
                private readonly CallOnceManager _manager;
                private bool _isTimedOut = false;
                private bool _isSignaled = false;
                private int _waitCount = 0;

                internal AsyncWaiter(CallOnceManager manager)
                {
                    _manager = manager;
                }

                private bool ShouldSignalNext
                {
                    get { return _isTimedOut && _isSignaled; }
                }

                void IWaiter.Signal()
                {
                    _wait.Set();
                    CloseWaitHandle();

                    bool signalNext;
                    lock (_manager.ThisLock)
                    {
                        _isSignaled = true;
                        signalNext = ShouldSignalNext;
                    }
                    if (signalNext)
                    {
                        _manager.SignalNext();
                    }
                }

                internal async Task<bool> WaitAsync(CancellationToken token)
                {
                    try
                    {
                        if (!await _wait.WaitAsync(token))
                        {
                            bool signalNext;
                            lock (_manager.ThisLock)
                            {
                                _isTimedOut = true;
                                signalNext = ShouldSignalNext;
                            }
                            if (signalNext)
                            {
                                _manager.SignalNext();
                            }
                        }
                    }
                    finally
                    {
                        CloseWaitHandle();
                    }

                    return !_isTimedOut;
                }

                private void CloseWaitHandle()
                {
                    if (Interlocked.Increment(ref _waitCount) == 2)
                    {
                        _wait.Dispose();
                    }
                }
            }
        }

        internal class SessionIdleManager
        {
            private IChannelBinder _binder;
            private ServiceChannel _channel;
            private long _idleTicks;
            private long _lastActivity;
            private IOThreadTimer _timer;
            private static Action<object> s_timerCallback;
            private bool _didIdleAbort;
            private bool _isTimerCancelled;
            private object _thisLock;
            private bool? _isNeeded = null;

            public SessionIdleManager()
            {
                _thisLock = new object();
            }

            internal SessionIdleManager UseIfNeeded(IChannelBinder binder, TimeSpan idle)
            {
                if (_isNeeded.HasValue)
                {
                    return _isNeeded.Value ? this : null;
                }

                if (binder.HasSession && (idle != TimeSpan.MaxValue))
                {
                    _binder = binder;
                    _timer = new IOThreadTimer(GetTimerCallback(), this, false);
                    _idleTicks = Ticks.FromTimeSpan(idle);
                    _timer.SetAt(Ticks.Now + _idleTicks);
                    _isNeeded = true;
                    return this;
                }
                else
                {
                    _isNeeded = false;
                    return null;
                }
            }

            internal bool DidIdleAbort
            {
                get
                {
                    lock (_thisLock)
                    {
                        return _didIdleAbort;
                    }
                }
            }

            internal void CancelTimer()
            {
                lock (_thisLock)
                {
                    _isTimerCancelled = true;
                    _timer?.Cancel();
                }
            }

            internal void CompletedActivity()
            {
                Interlocked.Exchange(ref _lastActivity, Ticks.Now);
            }

            internal void RegisterChannel(ServiceChannel channel, out bool didIdleAbort)
            {
                lock (_thisLock)
                {
                    _channel = channel;
                    didIdleAbort = _didIdleAbort;
                }
            }

            private static Action<object> GetTimerCallback()
            {
                if (s_timerCallback == null)
                {
                    s_timerCallback = TimerCallback;
                }
                return s_timerCallback;
            }

            private static void TimerCallback(object state)
            {
                ((SessionIdleManager)state).TimerCallback();
            }

            private void TimerCallback()
            {
                // This reads lastActivity atomically without changing its value.
                // (it only sets if it is zero, and then it sets it to zero).
                long last = Interlocked.CompareExchange(ref _lastActivity, 0, 0);
                long abortTime = last + _idleTicks;

                lock (_thisLock)
                {
                    long ticksNow = Ticks.Now;
                    if (ticksNow > abortTime)
                    {
                        _didIdleAbort = true;
                        if (_channel != null)
                        {
                            _channel.Abort();
                        }
                        else
                        {
                            _binder.Abort();
                        }
                    }
                    else
                    {
                        if (!_isTimerCancelled && _binder.Channel.State != CommunicationState.Faulted && _binder.Channel.State != CommunicationState.Closed)
                        {
                            _timer.SetAt(abortTime);
                        }
                    }
                }
            }
        }
    }
}
