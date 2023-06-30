// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
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
        private IServiceDispatcher _serviceDispatcher;
        private IServiceChannelDispatcher _serviceChannelDispatcher;
        private EndpointAddress cachedLocalAddress;
        private EndpointAddress remoteAddress;
        private TChannel pendingChannel;
        private readonly InterruptibleWaitObject pendingChannelEvent = new InterruptibleWaitObject(false, false);


        protected ServerReliableChannelBinder(IServiceDispatcher serviceDispatcher,
            EndpointAddress remoteAddress, MessageFilter filter, int priority, MaskingMode maskingMode,
            TolerateFaultsMode faultMode, TimeSpan defaultCloseTimeout,
            TimeSpan defaultSendTimeout)
            : base(null, maskingMode, faultMode, defaultCloseTimeout, defaultSendTimeout)
        {
            _serviceDispatcher = serviceDispatcher;
            this.remoteAddress = remoteAddress;
        }

        protected ServerReliableChannelBinder(TChannel channel, EndpointAddress cachedLocalAddress,
            EndpointAddress remoteAddress, MaskingMode maskingMode,
            TolerateFaultsMode faultMode, TimeSpan defaultCloseTimeout,
            TimeSpan defaultSendTimeout)
            : base(channel, maskingMode, faultMode, defaultCloseTimeout, defaultSendTimeout)
        {
            this.cachedLocalAddress = cachedLocalAddress;
            this.remoteAddress = remoteAddress;
        }

        protected override bool CanGetChannelForReceive => true;

        public override EndpointAddress LocalAddress
        {
            get
            {
                if (this.cachedLocalAddress != null)
                {
                    return this.cachedLocalAddress;
                }
                else
                {
                    return this.GetInnerChannelLocalAddress();
                }
            }
        }

        protected override bool MustCloseChannel
        {
            get
            {
                return this.MustOpenChannel || this.HasSession;
            }
        }

        protected override bool MustOpenChannel
        {
            get
            {
                return this._serviceChannelDispatcher != null;
            }
        }

        public override EndpointAddress RemoteAddress
        {
            get
            {
                return this.remoteAddress;
            }
        }

        private void AddAddressedProperty(Message message)
        {
            message.Properties.Add(AddressedPropertyName, new object());
        }

        protected override void AddOutputHeaders(Message message)
        {
            if (this.GetAddressedProperty(message) == null)
            {
                this.RemoteAddress.ApplyTo(message);
                this.AddAddressedProperty(message);
            }
        }

        public bool AddressResponse(Message request, Message response)
        {
            if (this.GetAddressedProperty(response) != null)
            {
                throw Fx.AssertAndThrow("The binder can't address a response twice");
            }

            try
            {
                RequestReplyCorrelator.PrepareReply(response, request);
            }
            catch (MessageHeaderException exception)
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
            catch (MessageHeaderException exception)
            {
                // swallow it - we don't need to address the reply if the addressing headers are bad
                //if (DiagnosticUtility.ShouldTraceInformation)
                //    DiagnosticUtility.TraceHandledException(exception, TraceEventType.Information);
            }

            if (sendResponse)
                this.AddAddressedProperty(response);

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

        public static IServerReliableChannelBinder CreateBinder(ChannelBuilder builder,
            EndpointAddress remoteAddress, MessageFilter filter, int priority,
            TolerateFaultsMode faultMode, TimeSpan defaultCloseTimeout,
            TimeSpan defaultSendTimeout)
        {
            Type type = typeof(TChannel);

            if (type == typeof(IDuplexChannel))
            {
                return new DuplexServerReliableChannelBinder(builder, remoteAddress, filter,
                    priority, MaskingMode.None, defaultCloseTimeout, defaultSendTimeout);
            }
            else if (type == typeof(IDuplexSessionChannel))
            {
                return new DuplexSessionServerReliableChannelBinder(builder, remoteAddress, filter,
                    priority, MaskingMode.None, faultMode, defaultCloseTimeout,
                    defaultSendTimeout);
            }
            else if (type == typeof(IReplyChannel))
            {
                return new ReplyServerReliableChannelBinder(builder, remoteAddress, filter,
                    priority, MaskingMode.None, defaultCloseTimeout, defaultSendTimeout);
            }
            else if (type == typeof(IReplySessionChannel))
            {
                return new ReplySessionServerReliableChannelBinder(builder, remoteAddress, filter,
                    priority, MaskingMode.None, faultMode, defaultCloseTimeout,
                    defaultSendTimeout);
            }
            else
            {
                throw Fx.AssertAndThrow("ServerReliableChannelBinder supports creation of IDuplexChannel, IDuplexSessionChannel, IReplyChannel, and IReplySessionChannel only.");
            }
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

            lock (this.ThisLock)
            {
                channel = pendingChannel;
                pendingChannel = null;
                pendingChannelEvent.Set();
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

            lock (this.ThisLock)
            {
                if (!this.Synchronizer.TolerateFaults ||
                    this.State == CommunicationState.Faulted ||
                    this.State == CommunicationState.Closing ||
                    this.State == CommunicationState.Closed)
                {
                    return false;
                }
                else
                {
                    oldPendingChannel = this.pendingChannel;
                    this.pendingChannel = (TChannel)channel;
                    oldBinderChannel = this.Synchronizer.AbortCurentChannel();
                }
            }

            if (oldPendingChannel != null)
            {
                oldPendingChannel.Abort();
            }

            this.pendingChannelEvent.Set();

            if (oldBinderChannel != null)
            {
                oldBinderChannel.Abort();
            }

            return true;
        }

        abstract class DuplexServerReliableChannelBinder<TDuplexChannel>
            : ServerReliableChannelBinder<TDuplexChannel>
            where TDuplexChannel : class, IDuplexChannel
        {
            protected DuplexServerReliableChannelBinder(ChannelBuilder builder,
                EndpointAddress remoteAddress, MessageFilter filter, int priority,
                MaskingMode maskingMode, TolerateFaultsMode faultMode,
                TimeSpan defaultCloseTimeout, TimeSpan defaultSendTimeout)
                : base(builder, remoteAddress, filter, priority, maskingMode, faultMode,
                defaultCloseTimeout, defaultSendTimeout)
            {
            }

            protected DuplexServerReliableChannelBinder(TDuplexChannel channel,
                EndpointAddress cachedLocalAddress, EndpointAddress remoteAddress,
                MaskingMode maskingMode, TolerateFaultsMode faultMode,
                TimeSpan defaultCloseTimeout, TimeSpan defaultSendTimeout)
                : base(channel, cachedLocalAddress, remoteAddress, maskingMode, faultMode,
                defaultCloseTimeout, defaultSendTimeout)
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
                IDuplexChannel channel = this.Synchronizer.CurrentChannel;
                EndpointAddress localAddress = (channel == null) ? null : channel.LocalAddress;
                return localAddress;
            }

            protected override IAsyncResult OnBeginSend(TDuplexChannel channel, Message message,
                TimeSpan timeout, AsyncCallback callback, object state)
            {
                return channel.BeginSend(message, timeout, callback, state);
            }

            protected override IAsyncResult OnBeginTryReceive(TDuplexChannel channel,
                TimeSpan timeout, AsyncCallback callback, object state)
            {
                return channel.BeginTryReceive(timeout, callback, state);
            }

            protected override IAsyncResult OnBeginWaitForRequest(TDuplexChannel channel,
                TimeSpan timeout, AsyncCallback callback, object state)
            {
                return channel.BeginWaitForMessage(timeout, callback, state);
            }

            protected override void OnEndSend(TDuplexChannel channel, IAsyncResult result)
            {
                channel.EndSend(result);
            }

            protected override bool OnEndTryReceive(TDuplexChannel channel, IAsyncResult result,
                out RequestContext requestContext)
            {
                Message message;
                bool success = channel.EndTryReceive(result, out message);
                if (success)
                {
                    this.OnMessageReceived(message);
                }
                requestContext = this.WrapMessage(message);
                return success;
            }

            protected override bool OnEndWaitForRequest(TDuplexChannel channel,
                IAsyncResult result)
            {
                return channel.EndWaitForMessage(result);
            }

            protected abstract void OnMessageReceived(Message message);

            protected override void OnSend(TDuplexChannel channel, Message message,
                TimeSpan timeout)
            {
                channel.Send(message, timeout);
            }

            protected override bool OnTryReceive(TDuplexChannel channel, TimeSpan timeout,
                out RequestContext requestContext)
            {
                Message message;
                bool success = channel.TryReceive(timeout, out message);
                if (success)
                {
                    this.OnMessageReceived(message);
                }
                requestContext = this.WrapMessage(message);
                return success;
            }

            protected override bool OnWaitForRequest(TDuplexChannel channel, TimeSpan timeout)
            {
                return channel.WaitForMessage(timeout);
            }

        }

        sealed class DuplexServerReliableChannelBinder
            : DuplexServerReliableChannelBinder<IDuplexChannel>
        {
            public DuplexServerReliableChannelBinder(ChannelBuilder builder,
                EndpointAddress remoteAddress, MessageFilter filter, int priority,
                MaskingMode maskingMode, TimeSpan defaultCloseTimeout, TimeSpan defaultSendTimeout)
                : base(builder, remoteAddress, filter, priority, maskingMode,
                TolerateFaultsMode.Never, defaultCloseTimeout, defaultSendTimeout)
            {
            }

            public DuplexServerReliableChannelBinder(IDuplexChannel channel,
                EndpointAddress cachedLocalAddress, EndpointAddress remoteAddress,
                MaskingMode maskingMode, TimeSpan defaultCloseTimeout, TimeSpan defaultSendTimeout)
                : base(channel, cachedLocalAddress, remoteAddress, maskingMode, TolerateFaultsMode.Never,
                defaultCloseTimeout, defaultSendTimeout)
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

        sealed class DuplexSessionServerReliableChannelBinder
            : DuplexServerReliableChannelBinder<IDuplexSessionChannel>
        {
            public DuplexSessionServerReliableChannelBinder(ChannelBuilder builder,
                EndpointAddress remoteAddress, MessageFilter filter, int priority,
                MaskingMode maskingMode, TolerateFaultsMode faultMode,
                TimeSpan defaultCloseTimeout, TimeSpan defaultSendTimeout)
                : base(builder, remoteAddress, filter, priority, maskingMode, faultMode,
                defaultCloseTimeout, defaultSendTimeout)
            {
            }

            public DuplexSessionServerReliableChannelBinder(IDuplexSessionChannel channel,
                EndpointAddress cachedLocalAddress, EndpointAddress remoteAddress,
                MaskingMode maskingMode, TolerateFaultsMode faultMode,
                TimeSpan defaultCloseTimeout, TimeSpan defaultSendTimeout)
                : base(channel, cachedLocalAddress, remoteAddress, maskingMode, faultMode,
                defaultCloseTimeout, defaultSendTimeout)
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
                return this.Synchronizer.CurrentChannel.Session;
            }

            protected override bool HasSecuritySession(IDuplexSessionChannel channel)
            {
                return channel.Session is ISecuritySession;
            }

            protected override void OnMessageReceived(Message message)
            {
                if (message == null)
                    this.Synchronizer.OnReadEof();
            }
        }

        abstract class ReplyServerReliableChannelBinder<TReplyChannel>
            : ServerReliableChannelBinder<TReplyChannel>
            where TReplyChannel : class, IReplyChannel
        {
            public ReplyServerReliableChannelBinder(ChannelBuilder builder,
                EndpointAddress remoteAddress, MessageFilter filter, int priority,
                MaskingMode maskingMode, TolerateFaultsMode faultMode,
                TimeSpan defaultCloseTimeout, TimeSpan defaultSendTimeout)
                : base(builder, remoteAddress, filter, priority, maskingMode, faultMode,
                defaultCloseTimeout, defaultSendTimeout)
            {
            }

            public ReplyServerReliableChannelBinder(TReplyChannel channel,
                EndpointAddress cachedLocalAddress, EndpointAddress remoteAddress,
                MaskingMode maskingMode, TolerateFaultsMode faultMode,
                TimeSpan defaultCloseTimeout, TimeSpan defaultSendTimeout)
                : base(channel, cachedLocalAddress, remoteAddress, maskingMode, faultMode,
                defaultCloseTimeout, defaultSendTimeout)
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
                IReplyChannel channel = this.Synchronizer.CurrentChannel;
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
                    this.OnReadNullMessage();
                }
                requestContext = this.WrapRequestContext(requestContext);
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
                    this.OnReadNullMessage();
                }
                requestContext = this.WrapRequestContext(requestContext);
                return success;
            }

            protected override bool OnWaitForRequest(TReplyChannel channel, TimeSpan timeout)
            {
                return channel.WaitForRequest(timeout);
            }
        }

        sealed class ReplyServerReliableChannelBinder
            : ReplyServerReliableChannelBinder<IReplyChannel>
        {
            public ReplyServerReliableChannelBinder(ChannelBuilder builder,
                EndpointAddress remoteAddress, MessageFilter filter, int priority,
                MaskingMode maskingMode, TimeSpan defaultCloseTimeout, TimeSpan defaultSendTimeout)
                : base(builder, remoteAddress, filter, priority, maskingMode,
                TolerateFaultsMode.Never, defaultCloseTimeout, defaultSendTimeout)
            {
            }

            public ReplyServerReliableChannelBinder(IReplyChannel channel,
                EndpointAddress cachedLocalAddress, EndpointAddress remoteAddress,
                MaskingMode maskingMode, TimeSpan defaultCloseTimeout, TimeSpan defaultSendTimeout)
                : base(channel, cachedLocalAddress, remoteAddress, maskingMode,
                TolerateFaultsMode.Never, defaultCloseTimeout, defaultSendTimeout)
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

        sealed class ReplySessionServerReliableChannelBinder
            : ReplyServerReliableChannelBinder<IReplySessionChannel>
        {
            public ReplySessionServerReliableChannelBinder(ChannelBuilder builder,
                EndpointAddress remoteAddress, MessageFilter filter, int priority,
                MaskingMode maskingMode, TolerateFaultsMode faultMode,
                TimeSpan defaultCloseTimeout, TimeSpan defaultSendTimeout)
                : base(builder, remoteAddress, filter, priority, maskingMode, faultMode,
                defaultCloseTimeout, defaultSendTimeout)
            {
            }

            public ReplySessionServerReliableChannelBinder(IReplySessionChannel channel,
                EndpointAddress cachedLocalAddress, EndpointAddress remoteAddress,
                MaskingMode maskingMode, TolerateFaultsMode faultMode,
                TimeSpan defaultCloseTimeout, TimeSpan defaultSendTimeout)
                : base(channel, cachedLocalAddress, remoteAddress, maskingMode, faultMode,
                defaultCloseTimeout, defaultSendTimeout)
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
                return this.Synchronizer.CurrentChannel.Session;
            }

            protected override bool HasSecuritySession(IReplySessionChannel channel)
            {
                return channel.Session is ISecuritySession;
            }

            protected override void OnReadNullMessage()
            {
                this.Synchronizer.OnReadEof();
            }
        }
    }
}
