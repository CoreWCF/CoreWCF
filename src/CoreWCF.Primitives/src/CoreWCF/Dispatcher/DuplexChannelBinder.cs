using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.Runtime;
using CoreWCF.Channels;
using CoreWCF.Diagnostics;
using CoreWCF.Security;

namespace CoreWCF.Dispatcher
{
    internal class DuplexChannelBinder : IChannelBinder
    {
        IDuplexChannel channel;
        IRequestReplyCorrelator correlator;
        TimeSpan defaultCloseTimeout;
        TimeSpan defaultSendTimeout;
        IdentityVerifier identityVerifier;
        bool isSession;
        Uri listenUri;
        int pending;
        bool syncPumpEnabled;
        List<IDuplexRequest> requests;
        List<ICorrelatorKey> timedOutRequests;
        ChannelHandler channelHandler;
        volatile bool requestAborted;
        bool initialized = false;

        public DuplexChannelBinder() { }

        internal void Init(IDuplexSessionChannel channel, IRequestReplyCorrelator correlator, Uri listenUri)
        {
            Init((IDuplexChannel)channel, correlator, listenUri);
            isSession = true;
        }

        internal void Init(IDuplexChannel channel, IRequestReplyCorrelator correlator, Uri listenUri)
        {
            if (initialized)
            {
                Fx.Assert(this.channel == channel, "Wrong channel when calling Init");
                Fx.Assert(this.correlator == correlator, "Wrong channel when calling Init");
                Fx.Assert(this.listenUri == listenUri, "Wrong listenUri when calling Init");
                return;
            }

            Fx.Assert(channel != null, "caller must verify");
            Fx.Assert(correlator != null, "caller must verify");

            this.channel = channel;
            this.listenUri = listenUri;
            this.channel.Faulted += new EventHandler(OnFaulted);
            initialized = true;
        }

        public IChannel Channel
        {
            get { return channel; }
        }

        public TimeSpan DefaultCloseTimeout
        {
            get { return defaultCloseTimeout; }
            set { defaultCloseTimeout = value; }
        }

        internal ChannelHandler ChannelHandler
        {
            get
            {
                if (!(channelHandler != null))
                {
                    Fx.Assert("DuplexChannelBinder.ChannelHandler: (channelHandler != null)");
                }
                return channelHandler;
            }
            set
            {
                if (!(channelHandler == null))
                {
                    Fx.Assert("DuplexChannelBinder.ChannelHandler: (channelHandler == null)");
                }
                channelHandler = value;
            }
        }

        public TimeSpan DefaultSendTimeout
        {
            get { return defaultSendTimeout; }
            set { defaultSendTimeout = value; }
        }

        public bool HasSession
        {
            get { return isSession; }
        }

        internal IdentityVerifier IdentityVerifier
        {
            get
            {
                if (identityVerifier == null)
                {
                    identityVerifier = IdentityVerifier.CreateDefault();
                }

                return identityVerifier;
            }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("value");
                }

                identityVerifier = value;
            }
        }

        public Uri ListenUri
        {
            get { return listenUri; }
        }

        public EndpointAddress LocalAddress
        {
            get { return channel.LocalAddress; }
        }

        bool Pumping
        {
            get
            {
                if (syncPumpEnabled)
                    return true;

                if (ChannelHandler != null && ChannelHandler.HasRegisterBeenCalled)
                    return true;

                return false;
            }
        }

        public EndpointAddress RemoteAddress
        {
            get { return channel.RemoteAddress; }
        }

        List<IDuplexRequest> Requests
        {
            get
            {
                lock (ThisLock)
                {
                    if (requests == null)
                        requests = new List<IDuplexRequest>();
                    return requests;
                }
            }
        }

        List<ICorrelatorKey> TimedOutRequests
        {
            get
            {
                lock (ThisLock)
                {
                    if (timedOutRequests == null)
                    {
                        timedOutRequests = new List<ICorrelatorKey>();
                    }
                    return timedOutRequests;
                }
            }
        }

        object ThisLock
        {
            get { return this; }
        }

        void OnFaulted(object sender, EventArgs e)
        {
            //Some unhandled exception happened on the channel. 
            //So close all pending requests so the callbacks (in case of async)
            //on the requests are called.
            AbortRequests();
        }

        public void Abort()
        {
            channel.Abort();
            AbortRequests();
        }

        public void CloseAfterFault(TimeSpan timeout)
        {
            var helper = new TimeoutHelper(timeout);
            channel.CloseAsync(helper.GetCancellationToken());
            AbortRequests();
        }

        void AbortRequests()
        {
            IDuplexRequest[] array = null;
            lock (ThisLock)
            {
                if (requests != null)
                {
                    array = requests.ToArray();

                    foreach (IDuplexRequest request in array)
                    {
                        request.Abort();
                    }
                }
                requests = null;
                requestAborted = true;
            }

            // Remove requests from the correlator since the channel might be either faulting or aborting,
            // We are not going to get a reply for these requests. If they are not removed from the correlator, this will cause a leak.
            // This operation does not have to be under the lock
            if (array != null && array.Length > 0)
            {
                RequestReplyCorrelator requestReplyCorrelator = correlator as RequestReplyCorrelator;
                if (requestReplyCorrelator != null)
                {
                    foreach (IDuplexRequest request in array)
                    {
                        ICorrelatorKey keyedRequest = request as ICorrelatorKey;
                        if (keyedRequest != null)
                        {
                            requestReplyCorrelator.RemoveRequest(keyedRequest);
                        }
                    }
                }
            }

            //if there are any timed out requests, delete it from the correlator table
            DeleteTimedoutRequestsFromCorrelator();
        }

        TimeoutException GetReceiveTimeoutException(TimeSpan timeout)
        {
            EndpointAddress address = channel.RemoteAddress ?? channel.LocalAddress;
            if (address != null)
            {
                return new TimeoutException(SR.Format(SR.SFxRequestTimedOut2, address, timeout));
            }
            else
            {
                return new TimeoutException(SR.Format(SR.SFxRequestTimedOut1, timeout));
            }
        }

        internal bool HandleRequestAsReply(Message message)
        {
            UniqueId relatesTo = null;
            try
            {
                relatesTo = message.Headers.RelatesTo;
            }
            catch (MessageHeaderException)
            {
                // ignore it
            }
            if (relatesTo == null)
            {
                return false;
            }
            else
            {
                return HandleRequestAsReplyCore(message);
            }
        }

        bool HandleRequestAsReplyCore(Message message)
        {
            IDuplexRequest request = correlator.Find<IDuplexRequest>(message, true);
            if (request != null)
            {
                request.GotReply(message);
                return true;
            }
            return false;
        }

        public void EnsurePumping()
        {
            lock (ThisLock)
            {
                if (!syncPumpEnabled)
                {
                    if (!ChannelHandler.HasRegisterBeenCalled)
                    {
                        ChannelHandler.Register(ChannelHandler);
                    }
                }
            }
        }

        public async Task<TryAsyncResult<RequestContext>> TryReceiveAsync(CancellationToken token)
        {
            if (channel.State == CommunicationState.Faulted)
            {
                AbortRequests();
                return TryAsyncResult.FromResult((RequestContext)null);
            }

            var result = await channel.TryReceiveAsync(token);
            if (result.Success)
            {
                if (result.Result != null)
                {
                    return TryAsyncResult.FromResult((RequestContext)new DuplexRequestContext(channel, result.Result, this));
                }
                else
                {
                    AbortRequests();
                    return TryAsyncResult.FromResult((RequestContext)null);
                }
            }
            else
            {
                return TryAsyncResult<RequestContext>.FailedResult;
            }
        }

        public RequestContext CreateRequestContext(Message message)
        {
            return new DuplexRequestContext(channel, message, this);
        }

        public Task SendAsync(Message message, CancellationToken token)
        {
            return channel.SendAsync(message, token);
        }
            
        public async Task<Message> RequestAsync(Message message, CancellationToken token)
        {
            AsyncDuplexRequest duplexRequest = null;
            bool optimized = false;

            RequestReplyCorrelator.PrepareRequest(message);

            lock (ThisLock)
            {
                if (!Pumping)
                {
                    optimized = true;
                    syncPumpEnabled = true;
                }

                if (!optimized)
                    duplexRequest = new AsyncDuplexRequest(this);

                RequestStarting(message, duplexRequest);
            }

            if (optimized)
            {
                UniqueId messageId = message.Headers.MessageId;

                try
                {
                    await channel.SendAsync(message, token);
                    //if (DiagnosticUtility.ShouldUseActivity &&
                    //    ServiceModelActivity.Current != null &&
                    //    ServiceModelActivity.Current.ActivityType == ActivityType.ProcessAction)
                    //{
                    //    ServiceModelActivity.Current.Suspend();
                    //}

                    for (;;)
                    {
                        var result = await channel.TryReceiveAsync(token);
                        if (!result.Success)
                        {
                            // TODO: Derive CancellationToken to attach timeout
                            throw TraceUtility.ThrowHelperError(GetReceiveTimeoutException(TimeSpan.Zero), message);
                        }

                        if (result.Result == null)
                        {
                            AbortRequests();
                            return null;
                        }

                        if (result.Result.Headers.RelatesTo == messageId)
                        {
                            ThrowIfInvalidReplyIdentity(result.Result);
                            return result.Result;
                        }
                        else if (!HandleRequestAsReply(result.Result))
                        {
                            // SFx drops a message here
                            //if (DiagnosticUtility.ShouldTraceInformation)
                            //{
                            //    EndpointDispatcher dispatcher = null;
                            //    if (this.ChannelHandler != null && this.ChannelHandler.Channel != null)
                            //    {
                            //        dispatcher = this.ChannelHandler.Channel.EndpointDispatcher;
                            //    }
                            //    TraceUtility.TraceDroppedMessage(reply, dispatcher);
                            //}
                            result.Result.Close();
                        }
                    }
                }
                finally
                {
                    lock (ThisLock)
                    {
                        RequestCompleting(null);
                        syncPumpEnabled = false;
                        if (pending > 0)
                            EnsurePumping();
                    }
                }
            }
            else
            {
                await channel.SendAsync(message, token);
                EnsurePumping();
                return await duplexRequest.WaitForReplyAsync(token);
            }
        }

        // ASSUMPTION: (mmaruch) caller holds lock (this.mutex)
        void RequestStarting(Message message, IDuplexRequest request)
        {
            if (request != null)
            {
                Requests.Add(request);
                if (!requestAborted)
                {
                    correlator.Add<IDuplexRequest>(message, request);
                }
            }
            pending++;

        }

        // ASSUMPTION: (mmaruch) caller holds lock (this.mutex)
        void RequestCompleting(IDuplexRequest request)
        {
            pending--;
            if (pending == 0)
            {
                requests = null;
            }
            else if ((request != null) && (requests != null))
            {
                requests.Remove(request);
            }
        }

        // ASSUMPTION: caller holds ThisLock
        void AddToTimedOutRequestList(ICorrelatorKey request)
        {
            Fx.Assert(request != null, "request cannot be null");
            TimedOutRequests.Add(request);
        }

        // ASSUMPTION: caller holds  ThisLock
        void RemoveFromTimedOutRequestList(ICorrelatorKey request)
        {
            Fx.Assert(request != null, "request cannot be null");
            if (timedOutRequests != null)
            {
                timedOutRequests.Remove(request);
            }
        }

        void DeleteTimedoutRequestsFromCorrelator()
        {
            ICorrelatorKey[] array = null;
            if (timedOutRequests != null && timedOutRequests.Count > 0)
            {
                lock (ThisLock)
                {
                    if (timedOutRequests != null && timedOutRequests.Count > 0)
                    {
                        array = timedOutRequests.ToArray();
                        timedOutRequests = null;
                    }
                }
            }

            // Remove requests from the correlator since the channel might be either faulting, aborting or closing 
            // We are not going to get a reply for these timed out requests. If they are not removed from the correlator, this will cause a leak.
            // This operation does not have to be under the lock
            if (array != null && array.Length > 0)
            {
                RequestReplyCorrelator requestReplyCorrelator = correlator as RequestReplyCorrelator;
                if (requestReplyCorrelator != null)
                {
                    foreach (ICorrelatorKey request in array)
                    {
                        requestReplyCorrelator.RemoveRequest(request);
                    }
                }
            }

        }

        //[MethodImpl(MethodImplOptions.NoInlining)]
        //void EnsureIncomingIdentity(SecurityMessageProperty property, EndpointAddress address, Message reply)
        //{
        //    this.IdentityVerifier.EnsureIncomingIdentity(address, property.ServiceSecurityContext.AuthorizationContext);
        //}

        void ThrowIfInvalidReplyIdentity(Message reply)
        {
            //if (!this.isSession)
            //{
            //    SecurityMessageProperty property = reply.Properties.Security;
            //    EndpointAddress address = this.channel.RemoteAddress;

            //    if ((property != null) && (address != null))
            //    {
            //        EnsureIncomingIdentity(property, address, reply);
            //    }
            //}
        }

        public Task<bool> WaitForMessageAsync(CancellationToken token)
        {
            return channel.WaitForMessageAsync(token);
        }

        class DuplexRequestContext : RequestContextBase
        {
            DuplexChannelBinder binder;
            IDuplexChannel channel;

            internal DuplexRequestContext(IDuplexChannel channel, Message request, DuplexChannelBinder binder)
                : base(request, binder.DefaultCloseTimeout, binder.DefaultSendTimeout)
            {
                this.channel = channel;
                this.binder = binder;
            }

            protected override void OnAbort()
            {
            }

            protected override Task OnCloseAsync(CancellationToken token)
            {
                return Task.CompletedTask;
            }

            protected override Task OnReplyAsync(Message message, CancellationToken token)
            {
                if (message != null)
                {
                    return channel.SendAsync(message, token);
                }

                return Task.CompletedTask;
            }
        }

        interface IDuplexRequest
        {
            void Abort();
            void GotReply(Message reply);
        }

        class AsyncDuplexRequest : IDuplexRequest, ICorrelatorKey
        {
            Message reply;
            DuplexChannelBinder parent;
            AsyncManualResetEvent wait = new AsyncManualResetEvent();
            int waitCount = 0;
            RequestReplyCorrelator.Key requestCorrelatorKey;

            internal AsyncDuplexRequest(DuplexChannelBinder parent)
            {
                this.parent = parent;
            }

            RequestReplyCorrelator.Key ICorrelatorKey.RequestCorrelatorKey
            {
                get
                {
                    return requestCorrelatorKey;
                }
                set
                {
                    Fx.Assert(requestCorrelatorKey == null, "RequestCorrelatorKey is already set for this request");
                    requestCorrelatorKey = value;
                }
            }

            public void Abort()
            {
                wait.Set();
            }

            internal async Task<Message> WaitForReplyAsync(CancellationToken token)
            {
                try
                {
                    if(!await wait.WaitAsync(token))
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(parent.GetReceiveTimeoutException(TimeSpan.Zero));
                    }
                }
                finally
                {
                    CloseWaitHandle();
                }

                parent.ThrowIfInvalidReplyIdentity(reply);
                return reply;
            }

            public void GotReply(Message reply)
            {
                lock (parent.ThisLock)
                {
                    parent.RequestCompleting(this);
                }
                this.reply = reply;
                wait.Set();
                CloseWaitHandle();
            }

            void CloseWaitHandle()
            {
                if (Interlocked.Increment(ref waitCount) == 2)
                {
                    wait.Dispose();
                }
            }
        }

        // used to read-ahead by a single message and auto-close the session when we read null
        class AutoCloseDuplexSessionChannel : IDuplexSessionChannel
        {
            IDuplexSessionChannel innerChannel;
            InputQueue<Message> pendingMessages;
            Action messageDequeuedCallback;
            CloseState closeState;

            public AutoCloseDuplexSessionChannel(IDuplexSessionChannel innerChannel)
            {
                this.innerChannel = innerChannel;
                pendingMessages = new InputQueue<Message>();
                messageDequeuedCallback = new Action(StartBackgroundReceive); // kick off a new receive when a message is picked up
                closeState = new CloseState();
            }

            object ThisLock
            {
                get
                {
                    return this;
                }
            }

            public EndpointAddress LocalAddress
            {
                get { return innerChannel.LocalAddress; }
            }

            public EndpointAddress RemoteAddress
            {
                get { return innerChannel.RemoteAddress; }
            }

            public Uri Via
            {
                get { return innerChannel.Via; }
            }

            public IDuplexSession Session
            {
                get { return innerChannel.Session; }
            }

            public CommunicationState State
            {
                get { return innerChannel.State; }
            }

            public event EventHandler Closing
            {
                add { innerChannel.Closing += value; }
                remove { innerChannel.Closing -= value; }
            }

            public event EventHandler Closed
            {
                add { innerChannel.Closed += value; }
                remove { innerChannel.Closed -= value; }
            }

            public event EventHandler Faulted
            {
                add { innerChannel.Faulted += value; }
                remove { innerChannel.Faulted -= value; }
            }

            public event EventHandler Opened
            {
                add { innerChannel.Opened += value; }
                remove { innerChannel.Opened -= value; }
            }

            public event EventHandler Opening
            {
                add { innerChannel.Opening += value; }
                remove { innerChannel.Opening -= value; }
            }

            TimeSpan DefaultCloseTimeout
            {
                get
                {
                    IDefaultCommunicationTimeouts defaultTimeouts = innerChannel as IDefaultCommunicationTimeouts;

                    if (defaultTimeouts != null)
                    {
                        return defaultTimeouts.CloseTimeout;
                    }
                    else
                    {
                        return ServiceDefaults.CloseTimeout;
                    }
                }
            }

            TimeSpan DefaultReceiveTimeout
            {
                get
                {
                    IDefaultCommunicationTimeouts defaultTimeouts = innerChannel as IDefaultCommunicationTimeouts;

                    if (defaultTimeouts != null)
                    {
                        return defaultTimeouts.ReceiveTimeout;
                    }
                    else
                    {
                        return ServiceDefaults.ReceiveTimeout;
                    }
                }
            }

            // kick off an async receive so that we notice when the server is trying to shutdown
            async void StartBackgroundReceive()
            {
                Exception exceptionFromBeginReceive = null;
                try
                {
                    var message = await innerChannel.ReceiveAsync(CancellationToken.None);
                    if (message == null)
                    {
                        // we've hit end of session, time for auto-close to kick in
                        pendingMessages.Shutdown();
                        await CloseInnerChannelAsync();
                    }
                    else
                    {
                        pendingMessages.EnqueueAndDispatch(message, messageDequeuedCallback, true);
                    }
                }
                catch (Exception e)
                {
                    if (Fx.IsFatal(e))
                    {
                        throw;
                    }
                    exceptionFromBeginReceive = e;

                    pendingMessages.EnqueueAndDispatch(e, messageDequeuedCallback, false);
                }
            }

            async Task CloseInnerChannelAsync()
            {
                lock (ThisLock)
                {
                    if (!closeState.TryBackgroundClose() || State != CommunicationState.Opened)
                    {
                        return;
                    }
                }

                Exception backgroundCloseException = null;
                try
                {
                    await innerChannel.CloseAsync();
                }
                catch (Exception e)
                {
                    if (Fx.IsFatal(e))
                    {
                        throw;
                    }

                    innerChannel.Abort();

                    backgroundCloseException = e;
                }

                if (backgroundCloseException != null)
                {
                    // stash away exception to throw out of user's Close()
                    closeState.CaptureBackgroundException(backgroundCloseException);
                }
            }

            public Task<Message> ReceiveAsync()
            {
                var helper = new TimeoutHelper(DefaultReceiveTimeout);
                return ReceiveAsync(helper.GetCancellationToken());
            }

            public Task<Message> ReceiveAsync(CancellationToken token)
            {
                return pendingMessages.DequeueAsync(token);
            }

            public Task<TryAsyncResult<Message>> TryReceiveAsync(CancellationToken token)
            {
                return pendingMessages.TryDequeueAsync(token);
            }

            public Task<bool> WaitForMessageAsync(CancellationToken token)
            {
                return pendingMessages.WaitForItemAsync(token);
            }

            public T GetProperty<T>() where T : class
            {
                return innerChannel.GetProperty<T>();
            }

            public void Abort()
            {
                innerChannel.Abort();
                Cleanup();
            }

            public Task CloseAsync()
            {
                var helper = new TimeoutHelper(DefaultCloseTimeout);
                return CloseAsync(helper.GetCancellationToken());
            }

            public async Task CloseAsync(CancellationToken token)
            {
                bool performChannelClose;
                lock (ThisLock)
                {
                    performChannelClose = closeState.TryUserClose();
                }
                if (performChannelClose)
                {
                    await innerChannel.CloseAsync(token);
                }
                else
                {
                    await closeState.WaitForBackgroundCloseAsync(token);
                }
                Cleanup();
            }

            // called from both Abort and Close paths
            void Cleanup()
            {
                pendingMessages.Dispose();
            }

            public async Task OpenAsync()
            {
                await innerChannel.OpenAsync();
                StartBackgroundReceive();
            }

            public async Task OpenAsync(CancellationToken token)
            {
                await innerChannel.OpenAsync(token);
                StartBackgroundReceive();
            }

            public Task SendAsync(Message message)
            {
                return innerChannel.SendAsync(message);
            }

            public Task SendAsync(Message message, CancellationToken token)
            {
                return innerChannel.SendAsync(message, token);
            }

            class CloseState
            {
                bool userClose;
                InputQueue<object> backgroundCloseData;

                public CloseState()
                {
                }

                public bool TryBackgroundClose()
                {
                    Fx.Assert(backgroundCloseData == null, "can't try twice");
                    if (!userClose)
                    {
                        backgroundCloseData = new InputQueue<object>();
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }

                public void FinishBackgroundClose()
                {
                    Fx.Assert(backgroundCloseData != null, "Only callable from background close");
                    backgroundCloseData.Close();
                }

                public bool TryUserClose()
                {
                    if (backgroundCloseData == null)
                    {
                        userClose = true;
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }

                public async Task WaitForBackgroundCloseAsync(CancellationToken token)
                {
                    Fx.Assert(backgroundCloseData != null, "Need to check background close first");
                    object dummy = await backgroundCloseData.DequeueAsync(token);
                    Fx.Assert(dummy == null, "we should get an exception or null");
                }

                public void CaptureBackgroundException(Exception exception)
                {
                    backgroundCloseData.EnqueueAndDispatch(exception, null, true);
                }

            }
        }
    }
}