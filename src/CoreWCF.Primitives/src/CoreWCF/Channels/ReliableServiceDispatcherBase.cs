// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.Configuration;
using CoreWCF.Dispatcher;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    internal abstract class ReliableServiceDispatcherBase<TChannel> : ChannelManagerBase, IServiceDispatcher
    {
        private bool _closed = false;

        protected ReliableServiceDispatcherBase(ReliableSessionBindingElement settings, Binding binding, IServiceDispatcher innerServiceDispatcher)
        {
            AcknowledgementInterval = settings.AcknowledgementInterval;
            FlowControlEnabled = settings.FlowControlEnabled;
            InactivityTimeout = settings.InactivityTimeout;
            MaxPendingChannels = settings.MaxPendingChannels;
            MaxRetryCount = settings.MaxRetryCount;
            MaxTransferWindowSize = settings.MaxTransferWindowSize;
            MessageVersion = binding.MessageVersion;
            Ordered = settings.Ordered;
            ReliableMessagingVersion = settings.ReliableMessagingVersion;
            InnerServiceDispatcher = innerServiceDispatcher;
            Binding = binding;
        }

        public TimeSpan AcknowledgementInterval { get; }

        protected FaultHelper FaultHelper { get; set; }

        public bool FlowControlEnabled { get; }

        public TimeSpan InactivityTimeout { get; }

        protected IServiceDispatcher InnerServiceDispatcher { get; }

        // Must call under lock.
        protected bool IsAccepting => State == CommunicationState.Opened;

        public IMessageFilterTable<EndpointAddress> LocalAddresses { get; set; }

        public int MaxPendingChannels { get; }

        public int MaxRetryCount { get; }

        public int MaxTransferWindowSize { get; }

        public MessageVersion MessageVersion { get; }

        public bool Ordered { get; }

        public ReliableMessagingVersion ReliableMessagingVersion { get; }

        public TimeSpan SendTimeout => InternalSendTimeout;

        protected abstract bool Duplex { get; }

        // Must call under lock.
        protected abstract bool HasChannels();

        // Must call under lock. Must call after the ReliableChannelListener has been opened.
        protected abstract bool IsLastChannel(UniqueId inputId);

        protected override void OnAbort()
        {
            // TODO: As there's an inversion of control compared with WCF, we need to figure out if this
            // will even get aborted and should the abort call flow the other way?
            bool abortInnerServiceDispatcher;

            lock (ThisLock)
            {
                _closed = true;
                abortInnerServiceDispatcher = !HasChannels();
            }

            if (abortInnerServiceDispatcher)
            {
                AbortInnerServiceDispatcher();
            }
        }

        protected virtual void AbortInnerServiceDispatcher()
        {
            FaultHelper.Abort();
            if (InnerServiceDispatcher is CommunicationObject innerCommunicationObject) { innerCommunicationObject.Abort(); }
        }

        protected virtual async Task CloseInnerServiceDispatcherAsync(CancellationToken token)
        {
            await FaultHelper.CloseAsync(token);
            if (InnerServiceDispatcher is CommunicationObject innerCommunicationObject)
            {
                await innerCommunicationObject.CloseAsync(token);
            }
        }

        protected override async Task OnCloseAsync(CancellationToken token)
        {
            if (this.ShouldCloseOnServiceDispatcherClose())
            {
                await CloseInnerServiceDispatcherAsync(token);
                _closed = true;
            }
        }

        protected override Task OnOpenAsync(CancellationToken token)
        {
            if (InnerServiceDispatcher is CommunicationObject innerCommunicationObject)
            {
                return innerCommunicationObject.OpenAsync(token);
            }
            return Task.CompletedTask;
        }

        public void OnReliableChannelAbort(UniqueId inputId, UniqueId outputId)
        {
            lock (ThisLock)
            {
                RemoveChannel(inputId, outputId);

                if (!_closed || HasChannels())
                {
                    return;
                }
            }

            AbortInnerServiceDispatcher();
        }

        public async Task OnReliableChannelCloseAsync(UniqueId inputId, UniqueId outputId, CancellationToken token)
        {
            if (ShouldCloseOnReliableChannelClose(inputId, outputId))
            {
                await CloseInnerServiceDispatcherAsync(token);

                lock (ThisLock)
                {
                    RemoveChannel(inputId, outputId);
                }
            }
        }

        // Must call under lock.
        protected abstract void RemoveChannel(UniqueId inputId, UniqueId outputId);

        private bool ShouldCloseOnChannelListenerClose()
        {
            lock (ThisLock)
            {
                if (!HasChannels())
                {
                    return true;
                }
                else
                {
                    _closed = true;
                    return false;
                }
            }
        }

        private bool ShouldCloseOnReliableChannelClose(UniqueId inputId, UniqueId outputId)
        {
            lock (ThisLock)
            {
                if (_closed && IsLastChannel(inputId))
                {
                    return true;
                }
                else
                {
                    RemoveChannel(inputId, outputId);
                    return false;
                }
            }
        }

        public Uri BaseAddress => InnerServiceDispatcher.BaseAddress;
        public Binding Binding { get; }
        public ServiceHostBase Host => InnerServiceDispatcher.Host;
        public IList<Type> SupportedChannelTypes { get; }

        public abstract Task<IServiceChannelDispatcher> CreateServiceChannelDispatcherAsync(IChannel channel);
    }

    // TReliableChannel is the type of the channel that the ReliableChannelListener creates, eg ReliableReplySessionChannel
    // TInnerChannel is the type of the channel share that the ReliableChannelListener creates, eg IReplySessionChannel
    internal abstract class ReliableServiceDispatcher<TChannel, TReliableChannel, TInnerChannel> : ReliableServiceDispatcherBase<TChannel>
        where TChannel : class, IChannel
        where TReliableChannel : class, IChannel
        where TInnerChannel : class, IChannel
    {
        Dictionary<UniqueId, TReliableChannel> channelsByInput;
        Dictionary<UniqueId, TReliableChannel> channelsByOutput;

        protected ReliableServiceDispatcher (ReliableSessionBindingElement binding, BindingContext context, IServiceDispatcher innerServiceDispatcher) : base(binding, context.Binding, innerServiceDispatcher)
        {
            //this.typedListener = context.BuildInnerChannelListener<TInnerChannel>();
            //this.inputQueueChannelAcceptor = new InputQueueChannelAcceptor<TChannel>(this);
            //this.Acceptor = this.inputQueueChannelAcceptor;
        }

        IServerReliableChannelBinder CreateBinder(TInnerChannel channel, EndpointAddress localAddress, EndpointAddress remoteAddress)
        {
            return ServerReliableChannelBinder<TInnerChannel>.CreateBinder(channel, localAddress,
                remoteAddress, TolerateFaultsMode.IfNotSecuritySession, DefaultCloseTimeout,
                DefaultSendTimeout);
        }
    }
}
