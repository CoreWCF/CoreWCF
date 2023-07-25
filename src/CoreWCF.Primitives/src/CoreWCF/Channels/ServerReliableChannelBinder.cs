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
            TimeSpan defaultSendTimeout, TimeSpan defaultReceiveTimeout)
            : base(channel, maskingMode, faultMode, defaultCloseTimeout, defaultSendTimeout, defaultReceiveTimeout)
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

        protected override bool MustCloseChannel
        {
            get
            {
                return MustOpenChannel || HasSession;
            }
        }

        protected override bool MustOpenChannel
        {
            get
            {
                return _serviceChannelDispatcher != null;
            }
        }

        public override EndpointAddress RemoteAddress
        {
            get
            {
                return _remoteAddress;
            }
        }

        private void AddAddressedProperty(Message message)
        {
            message.Properties.Add(AddressedPropertyName, new object());
        }

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

        // protected override IAsyncResult BeginTryGetChannel(TimeSpan timeout, AsyncCallback callback, object state);
        // public IAsyncResult BeginWaitForRequest(TimeSpan timeout, AsyncCallback callback, object state);
        // bool CompleteAcceptChannel(IAsyncResult result);
        // protected override bool EndTryGetChannel(IAsyncResult result);
        // public bool EndWaitForRequest(IAsyncResult result);
        // void OnAcceptChannelComplete(IAsyncResult result);
        // static void OnAcceptChannelCompleteStatic(IAsyncResult result);
        // protected abstract bool OnWaitForRequest(TChannel channel, TimeSpan timeout);

        public static IServerReliableChannelBinder CreateBinder(TChannel channel,
            EndpointAddress cachedLocalAddress, EndpointAddress remoteAddress,
            TolerateFaultsMode faultMode, TimeSpan defaultCloseTimeout,
            TimeSpan defaultSendTimeout, TimeSpan defaultReceiveTimeout)
        {
            Type type = typeof(TChannel);

            if (type == typeof(IDuplexChannel))
            {
                return new DuplexServerReliableChannelBinder((IDuplexChannel)channel,
                    cachedLocalAddress, remoteAddress, MaskingMode.All, defaultCloseTimeout,
                    defaultSendTimeout, defaultReceiveTimeout);
            }
            else if (type == typeof(IDuplexSessionChannel))
            {
                return new DuplexSessionServerReliableChannelBinder((IDuplexSessionChannel)channel,
                    cachedLocalAddress, remoteAddress, MaskingMode.All, faultMode,
                    defaultCloseTimeout, defaultSendTimeout, defaultReceiveTimeout);
            }
            else if (type == typeof(IReplyChannel))
            {
                return new ReplyServerReliableChannelBinder((IReplyChannel)channel,
                    cachedLocalAddress, remoteAddress, MaskingMode.All, defaultCloseTimeout,
                    defaultSendTimeout, defaultReceiveTimeout);
            }
            else if (type == typeof(IReplySessionChannel))
            {
                return new ReplySessionServerReliableChannelBinder((IReplySessionChannel)channel,
                    cachedLocalAddress, remoteAddress, MaskingMode.All, faultMode,
                    defaultCloseTimeout, defaultSendTimeout, defaultReceiveTimeout);
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

        protected override void OnAbort()
        {
        }

        protected override Task OnCloseAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }

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

        protected override Task OnOpenAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }

        //protected override bool TryGetChannel(TimeSpan timeout)
        //{
        //    if (!this.pendingChannelEvent.Wait(timeout))
        //        return false;

        //    TChannel abortChannel = null;

        //    lock (this.ThisLock)
        //    {
        //        if (this.State != CommunicationState.Faulted &&
        //            this.State != CommunicationState.Closing &&
        //            this.State != CommunicationState.Closed)
        //        {
        //            if (!this.Synchronizer.SetChannel(this.pendingChannel))
        //            {
        //                abortChannel = this.pendingChannel;
        //            }

        //            this.pendingChannel = null;
        //            this.pendingChannelEvent.Reset();
        //        }
        //    }

        //    if (abortChannel != null)
        //    {
        //        abortChannel.Abort();
        //    }

        //    return true;
        //}

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

        private abstract class DuplexServerReliableChannelBinder<TDuplexChannel>
            : ServerReliableChannelBinder<TDuplexChannel>
            where TDuplexChannel : class, IDuplexChannel
        {
            protected DuplexServerReliableChannelBinder(TDuplexChannel channel,
                EndpointAddress cachedLocalAddress, EndpointAddress remoteAddress,
                MaskingMode maskingMode, TolerateFaultsMode faultMode,
                TimeSpan defaultCloseTimeout, TimeSpan defaultSendTimeout, TimeSpan defaultReceiveTimeout)
                : base(channel, cachedLocalAddress, remoteAddress, maskingMode, faultMode,
                defaultCloseTimeout, defaultSendTimeout, defaultReceiveTimeout)
            {
            }

            public override bool CanSendAsynchronously
            {
                get
                {
                    return true;
                }
            }

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

            protected abstract void OnMessageReceived(Message message);

            protected override bool OnTryReceive(TDuplexChannel channel, TimeSpan timeout,
                out RequestContext requestContext)
            {
                Message message;
                bool success = channel.TryReceive(timeout, out message);
                if (success)
                {
                    OnMessageReceived(message);
                }
                requestContext = WrapMessage(message);
                return success;
            }
        }

        private sealed class DuplexServerReliableChannelBinder
            : DuplexServerReliableChannelBinder<IDuplexChannel>
        {
            public DuplexServerReliableChannelBinder(IDuplexChannel channel,
                EndpointAddress cachedLocalAddress, EndpointAddress remoteAddress,
                MaskingMode maskingMode, TimeSpan defaultCloseTimeout, TimeSpan defaultSendTimeout, TimeSpan defaultReceiveTimeout)
                : base(channel, cachedLocalAddress, remoteAddress, maskingMode, TolerateFaultsMode.Never,
                defaultCloseTimeout, defaultSendTimeout, defaultReceiveTimeout)
            {
            }

            public override bool HasSession
            {
                get
                {
                    return false;
                }
            }

            public override ISession GetInnerSession()
            {
                return null;
            }

            protected override bool HasSecuritySession(IDuplexChannel channel)
            {
                return false;
            }

            protected override void OnMessageReceived(Message message)
            {
            }
        }

        private sealed class DuplexSessionServerReliableChannelBinder
            : DuplexServerReliableChannelBinder<IDuplexSessionChannel>
        {
            public DuplexSessionServerReliableChannelBinder(IDuplexSessionChannel channel,
                EndpointAddress cachedLocalAddress, EndpointAddress remoteAddress,
                MaskingMode maskingMode, TolerateFaultsMode faultMode,
                TimeSpan defaultCloseTimeout, TimeSpan defaultSendTimeout, TimeSpan defaultReceiveTimeout)
                : base(channel, cachedLocalAddress, remoteAddress, maskingMode, faultMode,
                defaultCloseTimeout, defaultSendTimeout, defaultReceiveTimeout)
            {
            }

            public override bool HasSession
            {
                get
                {
                    return true;
                }
            }

            protected override IAsyncResult BeginCloseChannel(IDuplexSessionChannel channel,
                TimeSpan timeout, AsyncCallback callback, object state)
            {
                return ReliableChannelBinderHelper.BeginCloseDuplexSessionChannel(this, channel,
                    timeout, callback, state);
            }

            protected override void CloseChannel(IDuplexSessionChannel channel, TimeSpan timeout)
            {
                ReliableChannelBinderHelper.CloseDuplexSessionChannel(this, channel, timeout);
            }

            protected override void EndCloseChannel(IDuplexSessionChannel channel,
                IAsyncResult result)
            {
                ReliableChannelBinderHelper.EndCloseDuplexSessionChannel(channel, result);
            }

            public override ISession GetInnerSession()
            {
                return Synchronizer.CurrentChannel.Session;
            }

            protected override bool HasSecuritySession(IDuplexSessionChannel channel)
            {
                return channel.Session is ISecuritySession;
            }

            protected override void OnMessageReceived(Message message)
            {
                if (message == null)
                    Synchronizer.OnReadEof();
            }
        }

        private abstract class ReplyServerReliableChannelBinder<TReplyChannel>
            : ServerReliableChannelBinder<TReplyChannel>
            where TReplyChannel : class, IReplyChannel
        {
            public ReplyServerReliableChannelBinder(TReplyChannel channel,
                EndpointAddress cachedLocalAddress, EndpointAddress remoteAddress,
                MaskingMode maskingMode, TolerateFaultsMode faultMode,
                TimeSpan defaultCloseTimeout, TimeSpan defaultSendTimeout, TimeSpan defaultReceiveTimeout)
                : base(channel, cachedLocalAddress, remoteAddress, maskingMode, faultMode,
                defaultCloseTimeout, defaultSendTimeout, defaultReceiveTimeout)
            {
            }

            public override bool CanSendAsynchronously
            {
                get
                {
                    return false;
                }
            }

            protected override EndpointAddress GetInnerChannelLocalAddress()
            {
                IReplyChannel channel = Synchronizer.CurrentChannel;
                EndpointAddress localAddress = (channel == null) ? null : channel.LocalAddress;
                return localAddress;
            }

            protected override IAsyncResult OnBeginTryReceive(TReplyChannel channel,
                TimeSpan timeout, AsyncCallback callback, object state)
            {
                return channel.BeginTryReceiveRequest(timeout, callback, state);
            }

            protected override IAsyncResult OnBeginWaitForRequest(TReplyChannel channel,
                TimeSpan timeout, AsyncCallback callback, object state)
            {
                return channel.BeginWaitForRequest(timeout, callback, state);
            }

            protected override bool OnEndTryReceive(TReplyChannel channel, IAsyncResult result,
                out RequestContext requestContext)
            {
                bool success = channel.EndTryReceiveRequest(result, out requestContext);
                if (success && (requestContext == null))
                {
                    OnReadNullMessage();
                }
                requestContext = WrapRequestContext(requestContext);
                return success;
            }

            protected override bool OnEndWaitForRequest(TReplyChannel channel, IAsyncResult result)
            {
                return channel.EndWaitForRequest(result);
            }

            protected virtual void OnReadNullMessage()
            {
            }

            protected override bool OnTryReceive(TReplyChannel channel, TimeSpan timeout,
                out RequestContext requestContext)
            {
                bool success = channel.TryReceiveRequest(timeout, out requestContext);
                if (success && (requestContext == null))
                {
                    OnReadNullMessage();
                }
                requestContext = WrapRequestContext(requestContext);
                return success;
            }

            protected override bool OnWaitForRequest(TReplyChannel channel, TimeSpan timeout)
            {
                return channel.WaitForRequest(timeout);
            }
        }

        private sealed class ReplyServerReliableChannelBinder
            : ReplyServerReliableChannelBinder<IReplyChannel>
        {
            public ReplyServerReliableChannelBinder(IReplyChannel channel,
                EndpointAddress cachedLocalAddress, EndpointAddress remoteAddress,
                MaskingMode maskingMode, TimeSpan defaultCloseTimeout, TimeSpan defaultSendTimeout, TimeSpan defaultReceiveTimeout)
                : base(channel, cachedLocalAddress, remoteAddress, maskingMode,
                TolerateFaultsMode.Never, defaultCloseTimeout, defaultSendTimeout, defaultReceiveTimeout)
            {
            }

            public override bool HasSession
            {
                get
                {
                    return false;
                }
            }

            public override ISession GetInnerSession()
            {
                return null;
            }

            protected override bool HasSecuritySession(IReplyChannel channel)
            {
                return false;
            }
        }

        private sealed class ReplySessionServerReliableChannelBinder
            : ReplyServerReliableChannelBinder<IReplySessionChannel>
        {
            public ReplySessionServerReliableChannelBinder(IReplySessionChannel channel,
                EndpointAddress cachedLocalAddress, EndpointAddress remoteAddress,
                MaskingMode maskingMode, TolerateFaultsMode faultMode,
                TimeSpan defaultCloseTimeout, TimeSpan defaultSendTimeout, TimeSpan defaultReceiveTimeout)
                : base(channel, cachedLocalAddress, remoteAddress, maskingMode, faultMode,
                defaultCloseTimeout, defaultSendTimeout, defaultReceiveTimeout)
            {
            }

            public override bool HasSession
            {
                get
                {
                    return true;
                }
            }

            protected override IAsyncResult BeginCloseChannel(IReplySessionChannel channel,
               TimeSpan timeout, AsyncCallback callback, object state)
            {
                return ReliableChannelBinderHelper.BeginCloseReplySessionChannel(this, channel,
                    timeout, callback, state);
            }

            protected override void CloseChannel(IReplySessionChannel channel, TimeSpan timeout)
            {
                ReliableChannelBinderHelper.CloseReplySessionChannel(this, channel, timeout);
            }

            protected override void EndCloseChannel(IReplySessionChannel channel,
                IAsyncResult result)
            {
                ReliableChannelBinderHelper.EndCloseReplySessionChannel(channel, result);
            }

            public override ISession GetInnerSession()
            {
                return Synchronizer.CurrentChannel.Session;
            }

            protected override bool HasSecuritySession(IReplySessionChannel channel)
            {
                return channel.Session is ISecuritySession;
            }

            protected override void OnReadNullMessage()
            {
                Synchronizer.OnReadEof();
            }
        }
    }
}
