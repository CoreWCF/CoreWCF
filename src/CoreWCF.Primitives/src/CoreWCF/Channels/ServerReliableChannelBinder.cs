// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using CoreWCF.Dispatcher;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    internal abstract class ServerReliableChannelBinder<TChannel> : ReliableChannelBinder<TChannel>, IServerReliableChannelBinder where TChannel : class, IChannel
    {
        private const string AddressedPropertyName = "MessageAddressedByBinderProperty";
        private readonly IServiceChannelDispatcher _serviceChannelDispatcher;
        private readonly EndpointAddress _cachedLocalAddress;
        private readonly EndpointAddress _remoteAddress;
        private TChannel _pendingChannel;
        private readonly InterruptibleWaitObject _pendingChannelEvent = new InterruptibleWaitObject(false, false);

        protected ServerReliableChannelBinder(TChannel channel, EndpointAddress cachedLocalAddress,
            EndpointAddress remoteAddress, MaskingMode maskingMode,
            TolerateFaultsMode faultMode, TimeSpan defaultCloseTimeout,
            TimeSpan defaultSendTimeout)
            : base(channel, maskingMode, faultMode, defaultCloseTimeout, defaultSendTimeout)
        {
            _cachedLocalAddress = cachedLocalAddress;
            _remoteAddress = remoteAddress;
        }

        protected override bool CanGetChannelForReceive => true;

        public override EndpointAddress LocalAddress
        {
            get
            {
                if (_cachedLocalAddress != null)
                {
                    return _cachedLocalAddress;
                }
                else
                {
                    return GetInnerChannelLocalAddress();
                }
            }
        }

        protected override bool MustCloseChannel => MustOpenChannel || HasSession;
        protected override bool MustOpenChannel => _serviceChannelDispatcher != null;
        public override EndpointAddress RemoteAddress => _remoteAddress;
        private void AddAddressedProperty(Message message) => message.Properties.Add(AddressedPropertyName, new object());

        protected override void AddOutputHeaders(Message message)
        {
            if (GetAddressedProperty(message) == null)
            {
                RemoteAddress.ApplyTo(message);
                AddAddressedProperty(message);
            }
        }

        public bool AddressResponse(Message request, Message response)
        {
            if (GetAddressedProperty(response) != null)
            {
                throw Fx.AssertAndThrow("The binder can't address a response twice");
            }

            try
            {
                RequestReplyCorrelator.PrepareReply(response, request);
            }
            catch (MessageHeaderException)
            {
                // swallow it - we don't need to correlate the reply if the MessageId header is bad
                //if (DiagnosticUtility.ShouldTraceInformation)
                //    DiagnosticUtility.TraceHandledException(exception, TraceEventType.Information);
            }

            bool sendResponse = true;
            try
            {
                sendResponse = RequestReplyCorrelator.AddressReply(response, request);
            }
            catch (MessageHeaderException)
            {
                // swallow it - we don't need to address the reply if the addressing headers are bad
                //if (DiagnosticUtility.ShouldTraceInformation)
                //    DiagnosticUtility.TraceHandledException(exception, TraceEventType.Information);
            }

            if (sendResponse)
                AddAddressedProperty(response);

            return sendResponse;
        }

        public static IServerReliableChannelBinder CreateBinder(TChannel channel,
            EndpointAddress cachedLocalAddress, EndpointAddress remoteAddress,
            TolerateFaultsMode faultMode, TimeSpan defaultCloseTimeout,
            TimeSpan defaultSendTimeout)
        {
            Type type = typeof(TChannel);

            if (type == typeof(IDuplexChannel))
            {
                return new DuplexServerReliableChannelBinder((IDuplexChannel)channel,
                    cachedLocalAddress, remoteAddress, MaskingMode.All, defaultCloseTimeout,
                    defaultSendTimeout);
            }
            else if (type == typeof(IDuplexSessionChannel))
            {
                return new DuplexSessionServerReliableChannelBinder((IDuplexSessionChannel)channel,
                    cachedLocalAddress, remoteAddress, MaskingMode.All, faultMode,
                    defaultCloseTimeout, defaultSendTimeout);
            }
            else if (type == typeof(IReplyChannel))
            {
                return new ReplyServerReliableChannelBinder((IReplyChannel)channel,
                    cachedLocalAddress, remoteAddress, MaskingMode.All, defaultCloseTimeout,
                    defaultSendTimeout);
            }
            else if (type == typeof(IReplySessionChannel))
            {
                return new ReplySessionServerReliableChannelBinder((IReplySessionChannel)channel,
                    cachedLocalAddress, remoteAddress, MaskingMode.All, faultMode,
                    defaultCloseTimeout, defaultSendTimeout);
            }
            else
            {
                throw Fx.AssertAndThrow("ServerReliableChannelBinder supports creation of IDuplexChannel, IDuplexSessionChannel, IReplyChannel, and IReplySessionChannel only.");
            }
        }

        private object GetAddressedProperty(Message message)
        {
            object property;

            message.Properties.TryGetValue(AddressedPropertyName, out property);
            return property;
        }

        protected abstract EndpointAddress GetInnerChannelLocalAddress();
        protected override void OnAbort() { }
        protected override Task OnCloseAsync(CancellationToken token) => Task.CompletedTask;
        protected override Task OnOpenAsync(CancellationToken token) => Task.CompletedTask;

        protected override void OnShutdown()
        {
            TChannel channel = null;

            lock (ThisLock)
            {
                channel = _pendingChannel;
                _pendingChannel = null;
                _pendingChannelEvent.Set();
            }

            if (channel != null)
                channel.Abort();
        }

        protected override async Task<bool> TryGetChannelAsync(CancellationToken token)
        {
            if (!await _pendingChannelEvent.WaitAsync(token))
                return false;

            TChannel abortChannel = null;

            lock (ThisLock)
            {
                if (State != CommunicationState.Faulted &&
                    State != CommunicationState.Closing &&
                    State != CommunicationState.Closed)
                {
                    if (!Synchronizer.SetChannel(_pendingChannel))
                    {
                        abortChannel = _pendingChannel;
                    }

                    _pendingChannel = null;
                    _pendingChannelEvent.Reset();
                }
            }

            if (abortChannel != null)
            {
                abortChannel.Abort();
            }

            return true;
        }

        protected virtual void OnMessageReceived(Message message) { }
        protected virtual void OnReadNullMessage() { }

        public bool UseNewChannel(IChannel channel)
        {
            TChannel oldPendingChannel = null;
            TChannel oldBinderChannel = null;

            lock (ThisLock)
            {
                if (!Synchronizer.TolerateFaults ||
                    State == CommunicationState.Faulted ||
                    State == CommunicationState.Closing ||
                    State == CommunicationState.Closed)
                {
                    return false;
                }
                else
                {
                    oldPendingChannel = _pendingChannel;
                    _pendingChannel = (TChannel)channel;
                    oldBinderChannel = Synchronizer.AbortCurentChannel();
                }
            }

            if (oldPendingChannel != null)
            {
                oldPendingChannel.Abort();
            }

            _pendingChannelEvent.Set();

            if (oldBinderChannel != null)
            {
                oldBinderChannel.Abort();
            }

            return true;
        }

        internal virtual IServiceChannelDispatcher WrapServiceChannelDispatcher(IServiceChannelDispatcher serviceChannelDispatcher)
        {
            return new MessageRequestContextWrappingServiceChannelDispatcher(serviceChannelDispatcher, this);
        }

        private class MessageRequestContextWrappingServiceChannelDispatcher : IServiceChannelDispatcher
        {
            private IServiceChannelDispatcher _innerServiceChannelDispatcher;
            private ServerReliableChannelBinder<TChannel> _binder;

            public MessageRequestContextWrappingServiceChannelDispatcher(IServiceChannelDispatcher serviceChannelDispatcher, ServerReliableChannelBinder<TChannel> binder)
            {
                _innerServiceChannelDispatcher = serviceChannelDispatcher;
                _binder = binder;
            }

            public Task DispatchAsync(RequestContext context)
            {
                if (context == null)
                {
                    _binder.OnReadNullMessage();
                }

                context = _binder.WrapRequestContext(context);
                return _innerServiceChannelDispatcher.DispatchAsync(context);
            }

            public Task DispatchAsync(Message message)
            {
                _binder.OnMessageReceived(message);
                RequestContext requestContext = _binder.WrapMessage(message);
                return _innerServiceChannelDispatcher.DispatchAsync(requestContext);
            }
        }

        private abstract class DuplexServerReliableChannelBinder<TDuplexChannel> : ServerReliableChannelBinder<TDuplexChannel>
            where TDuplexChannel : class, IDuplexChannel
        {
            protected DuplexServerReliableChannelBinder(TDuplexChannel channel,
                EndpointAddress cachedLocalAddress, EndpointAddress remoteAddress,
                MaskingMode maskingMode, TolerateFaultsMode faultMode,
                TimeSpan defaultCloseTimeout, TimeSpan defaultSendTimeout)
                : base(channel, cachedLocalAddress, remoteAddress, maskingMode, faultMode,
                defaultCloseTimeout, defaultSendTimeout)
            {
            }

            public override bool CanSendAsynchronously => true;

            protected override EndpointAddress GetInnerChannelLocalAddress()
            {
                IDuplexChannel channel = Synchronizer.CurrentChannel;
                EndpointAddress localAddress = (channel == null) ? null : channel.LocalAddress;
                return localAddress;
            }

            protected override Task OnSendAsync(TDuplexChannel channel, Message message, CancellationToken token)
            {
                return channel.SendAsync(message, token);
            }
        }

        private sealed class DuplexServerReliableChannelBinder : DuplexServerReliableChannelBinder<IDuplexChannel>
        {
            public DuplexServerReliableChannelBinder(IDuplexChannel channel,
                EndpointAddress cachedLocalAddress, EndpointAddress remoteAddress,
                MaskingMode maskingMode, TimeSpan defaultCloseTimeout, TimeSpan defaultSendTimeout)
                : base(channel, cachedLocalAddress, remoteAddress, maskingMode, TolerateFaultsMode.Never,
                defaultCloseTimeout, defaultSendTimeout)
            {
            }

            public override bool HasSession => false;
            public override ISession GetInnerSession() => null;
            protected override bool HasSecuritySession(IDuplexChannel channel) => false;
        }

        private sealed class DuplexSessionServerReliableChannelBinder : DuplexServerReliableChannelBinder<IDuplexSessionChannel>
        {
            public DuplexSessionServerReliableChannelBinder(IDuplexSessionChannel channel,
                EndpointAddress cachedLocalAddress, EndpointAddress remoteAddress,
                MaskingMode maskingMode, TolerateFaultsMode faultMode,
                TimeSpan defaultCloseTimeout, TimeSpan defaultSendTimeout)
                : base(channel, cachedLocalAddress, remoteAddress, maskingMode, faultMode,
                defaultCloseTimeout, defaultSendTimeout)
            {
            }

            public override bool HasSession => true;

            protected override Task CloseChannelAsync(IDuplexSessionChannel channel, CancellationToken token)
            {
                return ReliableChannelBinderHelper.CloseDuplexSessionChannelAsync(this, channel, token);
            }

            public override ISession GetInnerSession() => Synchronizer.CurrentChannel.Session;

            protected override bool HasSecuritySession(IDuplexSessionChannel channel) => channel.Session is ISecuritySession;

            protected override void OnMessageReceived(Message message)
            {
                if (message == null)
                    Synchronizer.OnReadEof();
            }
        }

        private abstract class ReplyServerReliableChannelBinder<TReplyChannel> : ServerReliableChannelBinder<TReplyChannel>
            where TReplyChannel : class, IReplyChannel
        {
            public ReplyServerReliableChannelBinder(TReplyChannel channel,
                EndpointAddress cachedLocalAddress, EndpointAddress remoteAddress,
                MaskingMode maskingMode, TolerateFaultsMode faultMode,
                TimeSpan defaultCloseTimeout, TimeSpan defaultSendTimeout)
                : base(channel, cachedLocalAddress, remoteAddress, maskingMode, faultMode,
                defaultCloseTimeout, defaultSendTimeout)
            {
            }

            public override bool CanSendAsynchronously => false;

            protected override EndpointAddress GetInnerChannelLocalAddress()
            {
                IReplyChannel channel = Synchronizer.CurrentChannel;
                EndpointAddress localAddress = (channel == null) ? null : channel.LocalAddress;
                return localAddress;
            }
        }

        private sealed class ReplyServerReliableChannelBinder : ReplyServerReliableChannelBinder<IReplyChannel>
        {
            public ReplyServerReliableChannelBinder(IReplyChannel channel,
                EndpointAddress cachedLocalAddress, EndpointAddress remoteAddress,
                MaskingMode maskingMode, TimeSpan defaultCloseTimeout, TimeSpan defaultSendTimeout)
                : base(channel, cachedLocalAddress, remoteAddress, maskingMode,
                TolerateFaultsMode.Never, defaultCloseTimeout, defaultSendTimeout)
            {
            }

            public override bool HasSession => false;
            public override ISession GetInnerSession() => null;
            protected override bool HasSecuritySession(IReplyChannel channel) => false;
        }

        private sealed class ReplySessionServerReliableChannelBinder : ReplyServerReliableChannelBinder<IReplySessionChannel>
        {
            public ReplySessionServerReliableChannelBinder(IReplySessionChannel channel,
                EndpointAddress cachedLocalAddress, EndpointAddress remoteAddress,
                MaskingMode maskingMode, TolerateFaultsMode faultMode,
                TimeSpan defaultCloseTimeout, TimeSpan defaultSendTimeout)
                : base(channel, cachedLocalAddress, remoteAddress, maskingMode, faultMode,
                defaultCloseTimeout, defaultSendTimeout)
            {
            }

            public override bool HasSession => true;

            protected override Task CloseChannelAsync(IReplySessionChannel channel, CancellationToken token)
            {
                return ReliableChannelBinderHelper.CloseReplySessionChannelAsync(this, channel, token);
            }

            public override ISession GetInnerSession() => Synchronizer.CurrentChannel.Session;
            protected override bool HasSecuritySession(IReplySessionChannel channel) => channel.Session is ISecuritySession;
            protected override void OnReadNullMessage() => Synchronizer.OnReadEof();
        }
    }
}
