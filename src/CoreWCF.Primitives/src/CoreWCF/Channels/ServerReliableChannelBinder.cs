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

        private bool IsListenerExceptionNullOrHandleable(Exception e)
        {
            if (e == null)
            {
                return true;
            }

            if (this.listener.State == CommunicationState.Faulted)
            {
                return false;
            }

            return this.IsHandleable(e);
        }

        protected override void OnAbort()
        {
            if (this.listener != null)
            {
                this.listener.Abort();
            }
        }

        protected override Task OnCloseAsync(CancellationToken token)
        {
            throw new Exception("Need to work out what to do here");
            //if (this.listener != null)
            //{
            //    this.listener.Close(timeout);
            //}
        }

        protected override void OnShutdown()
        {
            TChannel channel = null;

            lock (this.ThisLock)
            {
                channel = this.pendingChannel;
                this.pendingChannel = null;
                this.pendingChannelEvent.Set();
            }

            if (channel != null)
                channel.Abort();
        }

        protected override Task OnOpenAsync(CancellationToken token)
        {
            if (this.listener != null)
            {
                this.listener.Open(timeout);
                this.StartAccepting();
            }
        }

        void StartAccepting()
        {
            Exception expectedException = null;
            Exception unexpectedException = null;

            while (this.listener.State == CommunicationState.Opened)
            {
                expectedException = null;
                unexpectedException = null;

                try
                {
                    IAsyncResult result = this.listener.BeginAcceptChannel(TimeSpan.MaxValue,
                        onAcceptChannelComplete, this);

                    if (!result.CompletedSynchronously)
                    {
                        return;
                    }
                    else if (!this.CompleteAcceptChannel(result))
                    {
                        break;
                    }
                }
                catch (Exception e)
                {
                    if (Fx.IsFatal(e))
                    {
                        throw;
                    }

                    if (this.IsHandleable(e))
                    {
                        expectedException = e;
                        continue;
                    }
                    else
                    {
                        unexpectedException = e;
                        break;
                    }
                }
            }

            if (unexpectedException != null)
            {
                this.Fault(unexpectedException);
            }
            else if (this.listener.State == CommunicationState.Faulted)
            {
                this.Fault(expectedException);
            }
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
    }
}
