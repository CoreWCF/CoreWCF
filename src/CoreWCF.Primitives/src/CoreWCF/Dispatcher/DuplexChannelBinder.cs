// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Runtime;
using CoreWCF.Security;

namespace CoreWCF.Dispatcher
{
    internal class DuplexChannelBinder : IChannelBinder
    {
        private IDuplexChannel _channel;
        private IRequestReplyCorrelator _correlator;
        private TimeSpan _defaultCloseTimeout;
        private TimeSpan _defaultSendTimeout;
        private IdentityVerifier _identityVerifier;
        private int _pending;
        private List<IDuplexRequest> _requests;
        private List<ICorrelatorKey> _timedOutRequests;
        private ChannelHandler _channelHandler;
        private bool _requestAborted;
        private bool _initialized = false;
        private IServiceChannelDispatcher _next;

        public DuplexChannelBinder() { }

        internal void Init(IDuplexSessionChannel channel, IRequestReplyCorrelator correlator, Uri listenUri)
        {
            Init((IDuplexChannel)channel, correlator, listenUri);
            HasSession = true;
        }

        internal void Init(IDuplexChannel channel, IRequestReplyCorrelator correlator, Uri listenUri)
        {
            if (_initialized)
            {
                Fx.Assert(_channel == channel, "Wrong channel when calling Init");
                Fx.Assert(_correlator == correlator, "Wrong channel when calling Init");
                Fx.Assert(ListenUri == listenUri, "Wrong listenUri when calling Init");
                return;
            }

            Fx.Assert(channel != null, "caller must verify");
            Fx.Assert(correlator != null, "caller must verify");

            _channel = channel;
            ListenUri = listenUri;
            _correlator = correlator;
            _channel.Faulted += new EventHandler(OnFaulted);
            _initialized = true;
        }

        public IChannel Channel
        {
            get { return _channel; }
        }

        public TimeSpan DefaultCloseTimeout
        {
            get { return _defaultCloseTimeout; }
            set { _defaultCloseTimeout = value; }
        }

        internal ChannelHandler ChannelHandler
        {
            get
            {
                if (!(_channelHandler != null))
                {
                    Fx.Assert("DuplexChannelBinder.ChannelHandler: (channelHandler != null)");
                }
                return _channelHandler;
            }
            set
            {
                if (!(_channelHandler == null))
                {
                    Fx.Assert("DuplexChannelBinder.ChannelHandler: (channelHandler == null)");
                }
                _channelHandler = value;
            }
        }

        public TimeSpan DefaultSendTimeout
        {
            get { return _defaultSendTimeout; }
            set { _defaultSendTimeout = value; }
        }

        public bool HasSession { get; private set; }

        internal IdentityVerifier IdentityVerifier
        {
            get
            {
                if (_identityVerifier == null)
                {
                    _identityVerifier = IdentityVerifier.CreateDefault();
                }

                return _identityVerifier;
            }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }

                _identityVerifier = value;
            }
        }

        public Uri ListenUri { get; private set; }

        public EndpointAddress LocalAddress
        {
            get { return _channel.LocalAddress; }
        }

        public EndpointAddress RemoteAddress
        {
            get { return _channel.RemoteAddress; }
        }

        private List<IDuplexRequest> Requests
        {
            get
            {
                lock (ThisLock)
                {
                    if (_requests == null)
                    {
                        _requests = new List<IDuplexRequest>();
                    }

                    return _requests;
                }
            }
        }

        private List<ICorrelatorKey> TimedOutRequests
        {
            get
            {
                lock (ThisLock)
                {
                    if (_timedOutRequests == null)
                    {
                        _timedOutRequests = new List<ICorrelatorKey>();
                    }
                    return _timedOutRequests;
                }
            }
        }

        private object ThisLock
        {
            get { return this; }
        }

        private void OnFaulted(object sender, EventArgs e)
        {
            //Some unhandled exception happened on the channel. 
            //So close all pending requests so the callbacks (in case of async)
            //on the requests are called.
            AbortRequests();
        }

        public void Abort()
        {
            _channel.Abort();
            AbortRequests();
        }

        public void CloseAfterFault(TimeSpan timeout)
        {
            var helper = new TimeoutHelper(timeout);
            _channel.CloseAsync(helper.GetCancellationToken());
            AbortRequests();
        }

        private void AbortRequests()
        {
            IDuplexRequest[] array = null;
            lock (ThisLock)
            {
                if (_requests != null)
                {
                    array = _requests.ToArray();

                    foreach (IDuplexRequest request in array)
                    {
                        request.Abort();
                    }
                }
                _requests = null;
                _requestAborted = true;
            }

            // Remove requests from the correlator since the channel might be either faulting or aborting,
            // We are not going to get a reply for these requests. If they are not removed from the correlator, this will cause a leak.
            // This operation does not have to be under the lock
            if (array != null && array.Length > 0)
            {
                RequestReplyCorrelator requestReplyCorrelator = _correlator as RequestReplyCorrelator;
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

        private TimeoutException GetReceiveTimeoutException(TimeSpan timeout)
        {
            EndpointAddress address = _channel.RemoteAddress ?? _channel.LocalAddress;
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

        private bool HandleRequestAsReplyCore(Message message)
        {
            IDuplexRequest request = _correlator.Find<IDuplexRequest>(message, true);
            if (request != null)
            {
                request.GotReply(message);
                return true;
            }

            return false;
        }

        public RequestContext CreateRequestContext(Message message)
        {
            return new DuplexRequestContext(_channel, message, this);
        }

        public Task SendAsync(Message message, CancellationToken token)
        {
            return _channel.SendAsync(message, token);
        }

        public async Task<Message> RequestAsync(Message message, CancellationToken token)
        {
            RequestReplyCorrelator.PrepareRequest(message);
            AsyncDuplexRequest duplexRequest = new AsyncDuplexRequest(this);

            lock (ThisLock)
            {
                RequestStarting(message, duplexRequest);
            }

            await _channel.SendAsync(message, token);
            return await duplexRequest.WaitForReplyAsync(token);
        }

        // ASSUMPTION: caller holds lock (this.mutex)
        private void RequestStarting(Message message, IDuplexRequest request)
        {
            if (request != null)
            {
                Requests.Add(request);
                if (!_requestAborted)
                {
                    _correlator.Add<IDuplexRequest>(message, request);
                }
            }

            _pending++;
        }

        // ASSUMPTION: (mmaruch) caller holds lock (this.mutex)
        private void RequestCompleting(IDuplexRequest request)
        {
            _pending--;
            if (_pending == 0)
            {
                _requests = null;
            }
            else if ((request != null) && (_requests != null))
            {
                _requests.Remove(request);
            }
        }

        // ASSUMPTION: caller holds ThisLock
        private void AddToTimedOutRequestList(ICorrelatorKey request)
        {
            Fx.Assert(request != null, "request cannot be null");
            TimedOutRequests.Add(request);
        }

        // ASSUMPTION: caller holds  ThisLock
        private void RemoveFromTimedOutRequestList(ICorrelatorKey request)
        {
            Fx.Assert(request != null, "request cannot be null");
            if (_timedOutRequests != null)
            {
                _timedOutRequests.Remove(request);
            }
        }

        private void DeleteTimedoutRequestsFromCorrelator()
        {
            ICorrelatorKey[] array = null;
            if (_timedOutRequests != null && _timedOutRequests.Count > 0)
            {
                lock (ThisLock)
                {
                    if (_timedOutRequests != null && _timedOutRequests.Count > 0)
                    {
                        array = _timedOutRequests.ToArray();
                        _timedOutRequests = null;
                    }
                }
            }

            // Remove requests from the correlator since the channel might be either faulting, aborting or closing 
            // We are not going to get a reply for these timed out requests. If they are not removed from the correlator, this will cause a leak.
            // This operation does not have to be under the lock
            if (array != null && array.Length > 0)
            {
                RequestReplyCorrelator requestReplyCorrelator = _correlator as RequestReplyCorrelator;
                if (requestReplyCorrelator != null)
                {
                    foreach (ICorrelatorKey request in array)
                    {
                        requestReplyCorrelator.RemoveRequest(request);
                    }
                }
            }

        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void EnsureIncomingIdentity(SecurityMessageProperty property, EndpointAddress address, Message reply)
        {
            IdentityVerifier.EnsureIncomingIdentity(address, property.ServiceSecurityContext.AuthorizationContext);
        }

        private void ThrowIfInvalidReplyIdentity(Message reply)
        {
            if (!HasSession)
            {
                SecurityMessageProperty property = reply.Properties.Security;
                EndpointAddress address = _channel.RemoteAddress;

                if ((property != null) && (address != null))
                {
                    EnsureIncomingIdentity(property, address, reply);
                }
            }
        }

        public void SetNextDispatcher(IServiceChannelDispatcher dispatcher)
        {
            _next = dispatcher;
        }

        public Task DispatchAsync(RequestContext context)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotImplementedException());
        }

        public Task DispatchAsync(Message message)
        {
            Fx.Assert(_next != null, "SetNextDispatcher wasn't called");
            if (_channel.State == CommunicationState.Faulted || message == null)
            {
                AbortRequests();
                return _next.DispatchAsync((RequestContext)null);
            }

            return _next.DispatchAsync(new DuplexRequestContext(_channel, message, this));
        }

        private class DuplexRequestContext : RequestContextBase
        {
            private readonly DuplexChannelBinder binder;
            private readonly IDuplexChannel channel;

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

        private interface IDuplexRequest
        {
            void Abort();
            void GotReply(Message reply);
        }

        private class AsyncDuplexRequest : IDuplexRequest, ICorrelatorKey
        {
            private Message reply;
            private readonly DuplexChannelBinder parent;
            private readonly AsyncManualResetEvent wait = new AsyncManualResetEvent();
            private int waitCount = 0;
            private RequestReplyCorrelator.Key requestCorrelatorKey;

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
                    if (!await wait.WaitAsync(token))
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

            private void CloseWaitHandle()
            {
                if (Interlocked.Increment(ref waitCount) == 2)
                {
                    wait.Dispose();
                }
            }
        }
    }
}