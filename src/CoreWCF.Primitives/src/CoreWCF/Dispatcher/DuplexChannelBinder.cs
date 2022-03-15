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
        private IdentityVerifier _identityVerifier;
        private int _pending;
        private List<IDuplexRequest> _requests;
        private List<ICorrelatorKey> _timedOutRequests;
        private ChannelHandler _channelHandler;
        private bool _requestAborted;
        private bool _initialized = false;
        private IDefaultCommunicationTimeouts _timeouts;
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

        public TimeSpan DefaultSendTimeout => _timeouts.SendTimeout;
        public TimeSpan DefaultCloseTimeout => _timeouts.CloseTimeout;

        public IChannel Channel
        {
            get { return _channel; }
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
                _identityVerifier = value ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
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
                if (_correlator is RequestReplyCorrelator requestReplyCorrelator)
                {
                    foreach (IDuplexRequest request in array)
                    {
                        if (request is ICorrelatorKey keyedRequest)
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
                if (_correlator is RequestReplyCorrelator requestReplyCorrelator)
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
            Fx.Assert(dispatcher is IDefaultCommunicationTimeouts, "Next Dispatcher must implement IDefaultCommunicationTimeouts");
            _timeouts = dispatcher as IDefaultCommunicationTimeouts;
            _next = dispatcher;
        }

        public Task DispatchAsync(RequestContext context)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotImplementedException());
        }

        public Task DispatchAsync(Message message)
        {
            Fx.Assert(_next != null, "SetNextDispatcher wasn't called");
            Fx.Assert(_channel.State != CommunicationState.Closed, "Expected dispatcher state to be Opened or Faulted, instead it's " + _channel.State.ToString());
            if (_channel.State == CommunicationState.Faulted || message == null)
            {
                AbortRequests();
                return _next.DispatchAsync((RequestContext)null);
            }

            return _next.DispatchAsync(CreateRequestContext(message));
        }

        private class DuplexRequestContext : RequestContextBase
        {
            private readonly DuplexChannelBinder _binder;
            private readonly IDuplexChannel _channel;

            internal DuplexRequestContext(IDuplexChannel channel, Message request, DuplexChannelBinder binder)
                : base(request, binder.DefaultCloseTimeout, binder.DefaultSendTimeout)
            {
                _channel = channel;
                _binder = binder;
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
                    return _channel.SendAsync(message, token);
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
            private Message _reply;
            private readonly DuplexChannelBinder _parent;
            private readonly AsyncManualResetEvent _wait = new AsyncManualResetEvent();
            private int _waitCount = 0;
            private RequestReplyCorrelator.Key _requestCorrelatorKey;

            internal AsyncDuplexRequest(DuplexChannelBinder parent)
            {
                _parent = parent;
            }

            RequestReplyCorrelator.Key ICorrelatorKey.RequestCorrelatorKey
            {
                get
                {
                    return _requestCorrelatorKey;
                }
                set
                {
                    Fx.Assert(_requestCorrelatorKey == null, "RequestCorrelatorKey is already set for this request");
                    _requestCorrelatorKey = value;
                }
            }

            public void Abort()
            {
                _wait.Set();
            }

            internal async Task<Message> WaitForReplyAsync(CancellationToken token)
            {
                try
                {
                    if (!await _wait.WaitAsync(token))
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(_parent.GetReceiveTimeoutException(TimeSpan.Zero));
                    }
                }
                finally
                {
                    CloseWaitHandle();
                }

                _parent.ThrowIfInvalidReplyIdentity(_reply);
                return _reply;
            }

            public void GotReply(Message reply)
            {
                lock (_parent.ThisLock)
                {
                    _parent.RequestCompleting(this);
                }
                _reply = reply;
                _wait.Set();
                CloseWaitHandle();
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
}
