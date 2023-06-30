// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.Configuration;
using CoreWCF.Dispatcher;
using CoreWCF.Runtime;
using CoreWCF.Security;

namespace CoreWCF.Channels
{
    internal abstract class ReliableServiceDispatcherBase<TChannel> : ChannelManagerBase, IReliableFactorySettings, IServiceDispatcher
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

        internal IServiceDispatcher InnerServiceDispatcher { get; }

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

        public Uri Uri => InnerServiceDispatcher.BaseAddress;

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
            if (ShouldCloseOnServiceDispatcherClose())
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

        private bool ShouldCloseOnServiceDispatcherClose()
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

        public Task<IServiceChannelDispatcher> CreateInnerServiceChannelDispatcherAsync(IChannel channel)
        {
            return InnerServiceDispatcher.CreateServiceChannelDispatcherAsync(channel);
        }

        protected override TimeSpan DefaultCloseTimeout => ServiceDefaults.CloseTimeout;
        protected override TimeSpan DefaultOpenTimeout => ServiceDefaults.OpenTimeout;
        protected override TimeSpan DefaultReceiveTimeout => ServiceDefaults.ReceiveTimeout;
        protected override TimeSpan DefaultSendTimeout => ServiceDefaults.SendTimeout;
    }

    // TReliableChannel is the type of the channel that the ReliableChannelListener creates, eg ReliableReplySessionChannel
    // TInnerChannel is the type of the channel share that the ReliableChannelListener creates, eg IReplySessionChannel
    internal abstract class ReliableServiceDispatcher<TChannel, TReliableChannel, TInnerChannel> : ReliableServiceDispatcherBase<TChannel>
        where TChannel : class, IChannel
        where TReliableChannel : class, IChannel
        where TInnerChannel : class, IChannel
    {
        private Dictionary<UniqueId, TReliableChannel> channelsByInput;
        private Dictionary<UniqueId, TReliableChannel> channelsByOutput;
        private readonly IServiceDispatcher _innerServiceDispatcher;

        protected ReliableServiceDispatcher (ReliableSessionBindingElement binding, BindingContext context, IServiceDispatcher innerServiceDispatcher) : base(binding, context.Binding, innerServiceDispatcher)
        {
            _innerServiceDispatcher = innerServiceDispatcher;
            //this.typedListener = context.BuildInnerChannelListener<TInnerChannel>();
            //this.inputQueueChannelAcceptor = new InputQueueChannelAcceptor<TChannel>(this);
            //this.Acceptor = this.inputQueueChannelAcceptor;
        }

        // TODO: Search for all usages of ThisLock and make sure they are using this async lock.
        protected AsyncLock AsyncLock { get; } = new AsyncLock();

        private IServerReliableChannelBinder CreateBinder(TInnerChannel channel, EndpointAddress localAddress, EndpointAddress remoteAddress)
        {
            return ServerReliableChannelBinder<TInnerChannel>.CreateBinder(channel, localAddress,
                remoteAddress, TolerateFaultsMode.IfNotSecuritySession, DefaultCloseTimeout, DefaultSendTimeout);
        }

        protected abstract Task<TReliableChannel> CreateChannelAsync(UniqueId id, CreateSequenceInfo createSequenceInfo, IServerReliableChannelBinder binder);

        //public override async Task<IServiceChannelDispatcher> CreateServiceChannelDispatcherAsync(IChannel channel)
        //{
        //    IServiceChannelDispatcher channelDispatcher;
        //    TInnerChannel innerChannel;
        //    try
        //    {
        //        innerChannel = (TInnerChannel)channel;
        //        OnInnerChannelAccepted(innerChannel);
        //        await innerChannel.OpenAsync();
        //        channelDispatcher = await _innerServiceDispatcher.CreateServiceChannelDispatcherAsync(channel);

        //    }
        //    catch (Exception e)
        //    {
        //        if (Fx.IsFatal(e))
        //            throw;

        //        DiagnosticUtility.TraceHandledException(e, TraceEventType.Error);

        //        channel.Abort();
        //        return null;
        //    }

        //    ProcessChannel(innerChannel);
        //}

        //protected void Dispatch()
        //{
        //    this.inputQueueChannelAcceptor.Dispatch();
        //}

        // override to hook up events, etc pre-Open
        protected virtual void OnInnerChannelAccepted(TInnerChannel channel)
        {
        }

        protected TReliableChannel GetChannel(WsrmMessageInfo info, out UniqueId id)
        {
            id = WsrmUtilities.GetInputId(info);

            lock (ThisLock)
            {
                TReliableChannel channel = null;
                if ((id == null) || !channelsByInput.TryGetValue(id, out channel))
                {
                    if (Duplex)
                    {
                        UniqueId outputId = WsrmUtilities.GetOutputId(ReliableMessagingVersion, info);
                        if (outputId != null)
                        {
                            id = outputId;
                            channelsByOutput.TryGetValue(id, out channel);
                        }
                    }
                }

                return channel;
            }
        }

        private async Task HandleAcceptCompleteAsync(TInnerChannel channel)
        {
            if (channel == null)
            {
                return;
            }

            try
            {
                OnInnerChannelAccepted(channel);
                await channel.OpenAsync();
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                    throw;

                DiagnosticUtility.TraceHandledException(e, TraceEventType.Error);

                channel.Abort();
                return;
            }

            ProcessChannel(channel);
        }

        protected bool HandleException(Exception e, ICommunicationObject o)
        {
            if ((e is CommunicationException || e is TimeoutException) &&
                (o.State == CommunicationState.Opened))
            {
                DiagnosticUtility.TraceHandledException(e, TraceEventType.Warning);

                return true;
            }

            DiagnosticUtility.TraceHandledException(e, TraceEventType.Error);

            return false;
        }

        // Must call under lock.
        protected override bool HasChannels()
        {
            return (channelsByInput == null) ? false : (channelsByInput.Count > 0);
        }

        private bool IsExpectedException(Exception e)
        {
            if (e is ProtocolException)
            {
                return false;
            }
            else
            {
                return e is CommunicationException;
            }
        }

        // Must call under lock. Must call after the ReliableChannelListener has been opened.
        protected override bool IsLastChannel(UniqueId inputId)
        {
            return (channelsByInput.Count == 1) ? channelsByInput.ContainsKey(inputId) : false;
        }

        public override async Task<IServiceChannelDispatcher> CreateServiceChannelDispatcherAsync(IChannel channel)
        {
            if (channel != null)
            {
                await HandleAcceptCompleteAsync(channel as TInnerChannel);
            }
            return await CreateServiceChannelDispatcherCoreAsync(channel);
        }

        public abstract Task<IServiceChannelDispatcher> CreateServiceChannelDispatcherCoreAsync(IChannel channel);

        protected override void OnFaulted()
        {
            base.OnFaulted();
        }

        protected override void OnOpened()
        {
            base.OnOpened();

            channelsByInput = new Dictionary<UniqueId, TReliableChannel>();
            if (Duplex)
                channelsByOutput = new Dictionary<UniqueId, TReliableChannel>();
        }

        protected async Task<(TReliableChannel channel, bool newChannel)> ProcessCreateSequenceAsync(WsrmMessageInfo info, TInnerChannel channel)
        {
            bool newChannel = false;
            TReliableChannel reliableChannel = null;

            CreateSequenceInfo createSequenceInfo = info.CreateSequenceInfo;
            EndpointAddress acksTo;

            if (!WsrmUtilities.ValidateCreateSequence<TChannel>(info, this, channel, out acksTo))
                return (null, newChannel);

            await using (await AsyncLock.TakeLockAsync())
            {
                UniqueId id;

                if ((createSequenceInfo.OfferIdentifier != null)
                    && Duplex
                    && channelsByOutput.TryGetValue(createSequenceInfo.OfferIdentifier, out reliableChannel))
                {
                    return (reliableChannel, newChannel);
                }

                if (!IsAccepting)
                {
                    info.FaultReply = WsrmUtilities.CreateEndpointNotFoundFault(MessageVersion, SR.Format(SR.RMEndpointNotFoundReason, _innerServiceDispatcher.BaseAddress));
                    return (reliableChannel, newChannel);
                }

                // We don't have a concept of waiting for a channel to be accepted so this code doesn't have a modern equivalent
                //if (this.inputQueueChannelAcceptor.PendingCount >= MaxPendingChannels)
                //{
                //    info.FaultReply = WsrmUtilities.CreateCSRefusedServerTooBusyFault(MessageVersion, ReliableMessagingVersion, SR.Format(SR.ServerTooBusy, this.Uri));
                //    return (null, newChannel);
                //}

                id = WsrmUtilities.NextSequenceId();

                reliableChannel = await CreateChannelAsync(id, createSequenceInfo,
                    this.CreateBinder(channel, acksTo, createSequenceInfo.ReplyTo));
                channelsByInput.Add(id, reliableChannel);
                if (Duplex)
                    channelsByOutput.Add(createSequenceInfo.OfferIdentifier, reliableChannel);

                // Not needed as we don't have a pending AcceptChannel that then needs a pending TryReceiveRequest
                //dispatch = EnqueueWithoutDispatch((TChannel)(object)reliableChannel);
                newChannel = true;

                return (reliableChannel, newChannel);
            }
        }

        protected abstract void ProcessChannel(TInnerChannel channel);

        // Must call under lock.
        protected override void RemoveChannel(UniqueId inputId, UniqueId outputId)
        {
            channelsByInput.Remove(inputId);

            if (Duplex)
                channelsByOutput.Remove(outputId);
        }

        //IServerReliableChannelBinder CreateBinder(TInnerChannel channel, EndpointAddress localAddress, EndpointAddress remoteAddress)
        //{
        //    return ServerReliableChannelBinder<TInnerChannel>.CreateBinder(channel, localAddress,
        //        remoteAddress, TolerateFaultsMode.IfNotSecuritySession, DefaultCloseTimeout,
        //        DefaultSendTimeout);
        //}
    }

    /* */
    internal abstract class ReliableServiceDispatcherOverDatagram<TChannel, TReliableChannel, TInnerChannel, TItem>
    : ReliableServiceDispatcher<TChannel, TReliableChannel, TInnerChannel>
    where TChannel : class, IChannel
    where TReliableChannel : class, IChannel
    where TInnerChannel : class, IChannel
    where TItem : class, IDisposable
    {
        private readonly Action<object> _asyncHandleReceiveComplete;
        private readonly AsyncCallback _onTryReceiveComplete;
        private readonly ChannelTracker<TInnerChannel, object> _channelTracker;

        protected ReliableServiceDispatcherOverDatagram(ReliableSessionBindingElement binding, BindingContext context, IServiceDispatcher innerDispatcher)
            : base(binding, context, innerDispatcher)
        {
            _channelTracker = new ChannelTracker<TInnerChannel, object>();
        }

//        private void AsyncHandleReceiveComplete(object state)
//        {
//            try
//            {
//                IAsyncResult result = (IAsyncResult)state;
//                TInnerChannel channel = (TInnerChannel)result.AsyncState;
//                TItem item = null;

//                try
//                {
//                    EndTryReceiveItem(channel, result, out item);
//                    if (item == null)
//                        return;
//                }
//#pragma warning suppress 56500 // covered by FxCOP
//                catch (Exception e)
//                {
//                    if (Fx.IsFatal(e))
//                        throw;

//                    if (!HandleException(e, channel))
//                    {
//                        channel.Abort();
//                        return;
//                    }
//                }

//                if (item != null && HandleReceiveComplete(item, channel))
//                    StartReceiving(channel, true);
//            }
//#pragma warning suppress 56500 // covered by FxCOP
//            catch (Exception e)
//            {
//                if (Fx.IsFatal(e))
//                    throw;

//                Fault(e);
//            }
//        }

        private async Task<(TReliableChannel reliableChanel,bool newChannel, bool success)> ProcessItemAsync(TItem item, WsrmMessageInfo info, TInnerChannel channel)
        {
            TReliableChannel reliableChannel = null;
            bool newChannel = false;
            Message faultReply;

            if (info.FaultReply != null)
            {
                faultReply = info.FaultReply;
            }
            else if (info.CreateSequenceInfo == null)
            {
                UniqueId id;
                reliableChannel = GetChannel(info, out id);

                if (reliableChannel != null)
                    return (reliableChannel, newChannel, true);

                if (id == null)
                {
                    DisposeItem(item);
                    return (reliableChannel, newChannel, true);
                }

                faultReply = new UnknownSequenceFault(id).CreateMessage(MessageVersion,
                    ReliableMessagingVersion);
            }
            else
            {
                (reliableChannel, newChannel) = await ProcessCreateSequenceAsync(info, channel);

                if (reliableChannel != null)
                    return (reliableChannel, newChannel, true);

                faultReply = info.FaultReply;
            }

            try
            {
                await SendReplyAsync(faultReply, channel, item);
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                    throw;

                if (!HandleException(e, channel))
                {
                    channel.Abort();
                    return (reliableChannel, newChannel, false);
                }
            }
            finally
            {
                faultReply.Close();
                DisposeItem(item);
            }

            return (reliableChannel, newChannel, true);
        }

        //protected abstract IAsyncResult BeginTryReceiveItem(TInnerChannel channel, AsyncCallback callback, object state);
        protected abstract void DisposeItem(TItem item);
        //protected abstract void EndTryReceiveItem(TInnerChannel channel, IAsyncResult result, out TItem item);

        protected abstract Message GetMessage(TItem item);

        private async Task DispatchAsync(TItem item, TInnerChannel channel)
        {
            Message message = null;

            // GetMessage can call RequestContext.RequestMessage which can throw.
            try
            {
                message = GetMessage(item);
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                    throw;

                if (!HandleException(e, this))
                    throw;

                item.Dispose();

                return;
            }

            WsrmMessageInfo info = WsrmMessageInfo.Get(MessageVersion, ReliableMessagingVersion, channel,
                null, message);

            if (info.ParsingException != null)
            {
                DisposeItem(item);
                return;
            }

            TReliableChannel reliableChannel;

            bool newChannel;
            bool dispatch;
            bool success;
            (reliableChannel, newChannel, success) = await ProcessItemAsync(item, info, channel);
            if (!success)
            {
                return;
            }

            if (reliableChannel == null)
            {
                DisposeItem(item);
                return;
            }

            await ProcessSequencedItemAsync(reliableChannel, item, info);
        }

        //        private void OnTryReceiveComplete(IAsyncResult result)
        //        {
        //            if (!result.CompletedSynchronously)
        //            {
        //                try
        //                {
        //                    TInnerChannel channel = (TInnerChannel)result.AsyncState;
        //                    TItem item = null;

        //                    try
        //                    {
        //                        EndTryReceiveItem(channel, result, out item);
        //                        if (item == null)
        //                            return;
        //                    }
        //                    catch (Exception e)
        //                    {
        //                        if (Fx.IsFatal(e))
        //                            throw;

        //                        if (!HandleException(e, channel))
        //                        {
        //                            channel.Abort();
        //                            return;
        //                        }
        //                    }

        //                    if (item != null && HandleReceiveComplete(item, channel))
        //                        StartReceiving(channel, true);
        //                }
        //#pragma warning suppress 56500 // covered by FxCOP
        //                catch (Exception e)
        //                {
        //                    if (Fx.IsFatal(e))
        //                        throw;

        //                    Fault(e);
        //                }
        //            }
        //        }

        protected override async Task OnOpenAsync(CancellationToken token)
        {
            await _channelTracker.OpenAsync(token);
            await base.OnOpenAsync(token);
        }

        protected override void OnInnerChannelAccepted(TInnerChannel channel)
        {
            base.OnInnerChannelAccepted(channel);
            _channelTracker.PrepareChannel(channel);
        }

        protected override void ProcessChannel(TInnerChannel channel)
        {
            _channelTracker.Add(channel, null);
        }

        protected override void AbortInnerServiceDispatcher()
        {
            //base.AbortInnerListener();
            _channelTracker.Abort();
        }

        protected override async Task CloseInnerServiceDispatcherAsync(CancellationToken token)
        {
            await base.CloseInnerServiceDispatcherAsync(token);
            await _channelTracker.CloseAsync(token);
        }

        protected abstract Task ProcessSequencedItemAsync(TReliableChannel reliableChannel, TItem item, WsrmMessageInfo info);
        protected abstract Task SendReplyAsync(Message reply, TInnerChannel channel, TItem item);

//        private void StartReceiving(TInnerChannel channel, bool canBlock)
//        {
//            while (true)
//            {
//                TItem item = null;

//                try
//                {
//                    IAsyncResult result = BeginTryReceiveItem(channel, _onTryReceiveComplete, channel);
//                    if (!result.CompletedSynchronously)
//                        break;

//                    if (!canBlock)
//                    {
//                        ActionItem.Schedule(_asyncHandleReceiveComplete, result);
//                        break;
//                    }

//                    EndTryReceiveItem(channel, result, out item);

//                    if (item == null)
//                        break;
//                }
//#pragma warning suppress 56500 // covered by FxCOP
//                catch (Exception e)
//                {
//                    if (Fx.IsFatal(e))
//                        throw;

//                    if (!HandleException(e, channel))
//                    {
//                        channel.Abort();
//                        break;
//                    }
//                }

//                if (item != null && !HandleReceiveComplete(item, channel))
//                    break;
//            }
//        }

        public override Task<IServiceChannelDispatcher> CreateServiceChannelDispatcherCoreAsync(IChannel channel)
        {
            return Task.FromResult<IServiceChannelDispatcher>(new ReliableServiceDatagramChannelDispatcher(channel, this));
        }

        private class ReliableServiceDatagramChannelDispatcher : IServiceChannelDispatcher
        {
            private TInnerChannel _innerChannel;
            private ReliableServiceDispatcherOverDatagram<TChannel, TReliableChannel, TInnerChannel, TItem> _serviceDispatcher;

            public ReliableServiceDatagramChannelDispatcher(IChannel channel, ReliableServiceDispatcherOverDatagram<TChannel, TReliableChannel, TInnerChannel, TItem> serviceDispatcher)
            {
                if (channel is TInnerChannel innerChannel)
                {
                    _innerChannel = innerChannel;
                }
                else
                {
                    throw new ArgumentException();
                }

                _serviceDispatcher = serviceDispatcher;
            }

            public Task DispatchAsync(RequestContext context)
            {
                if (context is TItem item)
                {
                    return _serviceDispatcher.DispatchAsync(item, _innerChannel);
                }

                throw new ArgumentException();
            }

            public Task DispatchAsync(Message message)
            {
                if (message is TItem item)
                {
                    return _serviceDispatcher.DispatchAsync(item, _innerChannel);
                }

                throw new ArgumentException();
            }
        }
    }

    //internal abstract class ReliableServiceDispatcherOverDuplex<TChannel, TReliableChannel> :
    //    ReliableServiceDispatcherOverDatagram<TChannel, TReliableChannel, IDuplexChannel, Message>
    //    where TChannel : class, IChannel
    //    where TReliableChannel : class, IChannel
    //{
    //    protected ReliableListenerOverDuplex(ReliableSessionBindingElement binding, BindingContext context)
    //        : base(binding, context)
    //    {
    //        FaultHelper = new SendFaultHelper(context.Binding.SendTimeout, context.Binding.CloseTimeout);
    //    }

    //    protected override IAsyncResult BeginTryReceiveItem(IDuplexChannel channel, AsyncCallback callback, object state)
    //    {
    //        return channel.BeginTryReceive(TimeSpan.MaxValue, callback, state);
    //    }

    //    protected override void DisposeItem(Message item)
    //    {
    //        ((IDisposable)item).Dispose();
    //    }

    //    protected override void EndTryReceiveItem(IDuplexChannel channel, IAsyncResult result, out Message item)
    //    {
    //        channel.EndTryReceive(result, out item);
    //    }

    //    protected override Message GetMessage(Message item)
    //    {
    //        return item;
    //    }

    //    protected override void SendReply(Message reply, IDuplexChannel channel, Message item)
    //    {
    //        if (FaultHelper.AddressReply(item, reply))
    //            channel.Send(reply);
    //    }
    //}

    /* */
    internal abstract class ReliableServiceDispatcherOverReply<TChannel, TReliableChannel>
        : ReliableServiceDispatcherOverDatagram<TChannel, TReliableChannel, IReplyChannel, RequestContext>
        where TChannel : class, IChannel
        where TReliableChannel : class, IChannel
    {
        protected ReliableServiceDispatcherOverReply(ReliableSessionBindingElement binding, BindingContext context, IServiceDispatcher innerDispatcher)
            : base(binding, context, innerDispatcher)
        {
            FaultHelper = new ReplyFaultHelper(context.Binding.SendTimeout, context.Binding.CloseTimeout);
        }

        protected override void DisposeItem(RequestContext item)
        {
            ((IDisposable)item.RequestMessage).Dispose();
            ((IDisposable)item).Dispose();
        }

        protected override Message GetMessage(RequestContext item)
        {
            return item.RequestMessage;
        }

        protected override Task SendReplyAsync(Message reply, IReplyChannel channel, RequestContext item)
        {
            if (FaultHelper.AddressReply(item.RequestMessage, reply))
            {
                return item.ReplyAsync(reply);
            }

            return Task.CompletedTask;
        }
    }

    internal abstract class ReliableServiceDispatcherOverSession<TChannel, TReliableChannel, TInnerChannel, TInnerSession, TItem>
        : ReliableServiceDispatcher<TChannel, TReliableChannel, TInnerChannel>
        where TChannel : class, IChannel
        where TReliableChannel : InputQueueReplyChannel
        where TInnerChannel : class, IChannel, ISessionChannel<TInnerSession>
        where TInnerSession : ISession
        where TItem : IDisposable
    {
        protected ReliableServiceDispatcherOverSession(ReliableSessionBindingElement binding, BindingContext context, IServiceDispatcher serviceDispatcher)
            : base(binding, context, serviceDispatcher) { }

//        private void AsyncHandleReceiveComplete(object state)
//        {
//            try
//            {
//                IAsyncResult result = (IAsyncResult)state;
//                TInnerChannel channel = (TInnerChannel)result.AsyncState;
//                TItem item = default(TItem);

//                try
//                {
//                    EndTryReceiveItem(channel, result, out item);
//                    if (item == null)
//                    {
//                        channel.Close();
//                        return;
//                    }
//                }
//#pragma warning suppress 56500 // covered by FxCOP
//                catch (Exception e)
//                {
//                    if (Fx.IsFatal(e))
//                        throw;

//                    if (!HandleException(e, channel))
//                    {
//                        channel.Abort();
//                        return;
//                    }
//                }

//                if (item != null)
//                    DispatchAsync(item, channel);
//            }
//#pragma warning suppress 56500 // covered by FxCOP
//            catch (Exception e)
//            {
//                if (Fx.IsFatal(e))
//                    throw;

//                Fault(e);
//            }
//        }

        //protected abstract IAsyncResult BeginTryReceiveItem(TInnerChannel channel, AsyncCallback callback, object state);
        protected abstract void DisposeItem(TItem item);
        //protected abstract void EndTryReceiveItem(TInnerChannel channel, IAsyncResult result, out TItem item);
        protected abstract Message GetMessage(TItem item);

        public override Task<IServiceChannelDispatcher> CreateServiceChannelDispatcherCoreAsync(IChannel channel)
        {
            return Task.FromResult<IServiceChannelDispatcher>(new ReliableServiceSessionChannelDispatcher(channel, this));
        }

        private async Task DispatchAsync(TItem item, TInnerChannel channel)
        {
            WsrmMessageInfo info = WsrmMessageInfo.Get(MessageVersion, ReliableMessagingVersion, channel,
                channel.Session as ISecureConversationSession, GetMessage(item));

            if (info.ParsingException != null)
            {
                DisposeItem(item);
                channel.Abort();
                return;
            }

            TReliableChannel reliableChannel = null;
            bool newChannel = false;

            Message faultReply = null;
            if (info.FaultReply != null)
            {
                faultReply = info.FaultReply;
            }
            else if (info.CreateSequenceInfo == null)
            {
                UniqueId id;
                reliableChannel = GetChannel(info, out id);

                if ((reliableChannel == null) && (id == null))
                {
                    DisposeItem(item);
                    channel.Abort();
                    return;
                }

                if (reliableChannel == null)
                    faultReply = new UnknownSequenceFault(id).CreateMessage(MessageVersion,
                        ReliableMessagingVersion);
            }
            else
            {
                (reliableChannel, newChannel) = await ProcessCreateSequenceAsync(info, channel);

                if (reliableChannel == null)
                    faultReply = info.FaultReply;
            }

            if (reliableChannel != null)
            {
                // reliableChannel derives from InputQueueReplyChannel so ProcessSequencedItem will
                // call InputQueueReplyChannel.Enqueue which will dispatch the item.
                await ProcessSequencedItemAsync(channel, item, reliableChannel, info, newChannel);
                //if (dispatch)
                //    this.Dispatch();
            }
            else
            {
                try
                {
                    await SendReplyAsync(faultReply, channel, item);
                    await channel.CloseAsync();
                }
                catch (Exception e)
                {
                    if (Fx.IsFatal(e))
                        throw;

                    DiagnosticUtility.TraceHandledException(e, TraceEventType.Error);

                    channel.Abort();
                }
                finally
                {
                    faultReply.Close();
                    DisposeItem(item);
                }
            }
        }

        protected override void ProcessChannel(TInnerChannel channel) { }

        protected abstract Task ProcessSequencedItemAsync(TInnerChannel channel, TItem item, TReliableChannel reliableChannel, WsrmMessageInfo info, bool newChannel);
        protected abstract Task SendReplyAsync(Message reply, TInnerChannel channel, TItem item);

        private class ReliableServiceSessionChannelDispatcher : IServiceChannelDispatcher
        {
            private TInnerChannel _innerChannel;
            private ReliableServiceDispatcherOverSession<TChannel, TReliableChannel, TInnerChannel, TInnerSession, TItem> _serviceDispatcher;

            public ReliableServiceSessionChannelDispatcher(IChannel channel, ReliableServiceDispatcherOverSession<TChannel, TReliableChannel, TInnerChannel, TInnerSession, TItem> serviceDispatcher)
            {
                if (channel is TInnerChannel innerChannel)
                {
                    _innerChannel = innerChannel;
                }
                else
                {
                    throw new ArgumentException();
                }

                _serviceDispatcher = serviceDispatcher;
            }

            public Task DispatchAsync(RequestContext context)
            {
                if (context is TItem item)
                {
                    return _serviceDispatcher.DispatchAsync(item, _innerChannel);
                }

                throw new ArgumentException();
            }

            public Task DispatchAsync(Message message)
            {
                if (message is TItem item)
                {
                    return _serviceDispatcher.DispatchAsync(item, _innerChannel);
                }

                throw new ArgumentException();
            }
        }
    }

    //internal abstract class ReliableServiceDispatcherOverDuplexSession<TChannel, TReliableChannel>
    //    : ReliableServiceDispatcherOverSession<TChannel, TReliableChannel, IDuplexSessionChannel, IDuplexSession, Message>
    //    where TChannel : class, IChannel
    //    where TReliableChannel : class, IChannel
    //{
    //    protected ReliableListenerOverDuplexSession(ReliableSessionBindingElement binding, BindingContext context)
    //        : base(binding, context)
    //    {
    //        FaultHelper = new SendFaultHelper(context.Binding.SendTimeout, context.Binding.CloseTimeout);
    //    }

    //    protected override IAsyncResult BeginTryReceiveItem(IDuplexSessionChannel channel, AsyncCallback callback, object state)
    //    {
    //        return channel.BeginTryReceive(TimeSpan.MaxValue, callback, channel);
    //    }

    //    protected override void DisposeItem(Message item)
    //    {
    //        ((IDisposable)item).Dispose();
    //    }

    //    protected override void EndTryReceiveItem(IDuplexSessionChannel channel, IAsyncResult result, out Message item)
    //    {
    //        channel.EndTryReceive(result, out item);
    //    }

    //    protected override Message GetMessage(Message item)
    //    {
    //        return item;
    //    }

    //    protected override void SendReply(Message reply, IDuplexSessionChannel channel, Message item)
    //    {
    //        if (FaultHelper.AddressReply(item, reply))
    //            channel.Send(reply);
    //    }
    //}

    internal abstract class ReliableServiceDispatcherOverReplySession<TChannel, TReliableChannel>
        : ReliableServiceDispatcherOverSession<TChannel, TReliableChannel, IReplySessionChannel, IInputSession, RequestContext>
        where TChannel : class, IChannel
        where TReliableChannel : InputQueueReplyChannel
    {
        protected ReliableServiceDispatcherOverReplySession(ReliableSessionBindingElement binding, BindingContext context, IServiceDispatcher innerDispatcher)
            : base(binding, context, innerDispatcher)
        {
            FaultHelper = new ReplyFaultHelper(context.Binding.SendTimeout, context.Binding.CloseTimeout);
        }

        //protected override IAsyncResult BeginTryReceiveItem(IReplySessionChannel channel, AsyncCallback callback, object state)
        //{
        //    return channel.BeginTryReceiveRequest(TimeSpan.MaxValue, callback, channel);
        //}

        protected override void DisposeItem(RequestContext item)
        {
            ((IDisposable)item.RequestMessage).Dispose();
            ((IDisposable)item).Dispose();
        }

        //protected override void EndTryReceiveItem(IReplySessionChannel channel, IAsyncResult result, out RequestContext item)
        //{
        //    channel.EndTryReceiveRequest(result, out item);
        //}

        protected override Message GetMessage(RequestContext item)
        {
            return item.RequestMessage;
        }

        protected override Task SendReplyAsync(Message reply, IReplySessionChannel channel, RequestContext item)
        {
            if (FaultHelper.AddressReply(item.RequestMessage, reply))
            {
                return item.ReplyAsync(reply);
            }
            else
            {
                return Task.CompletedTask;
            }
        }
    }

    //internal class ReliableDuplexServiceDispatcherOverDuplex : ReliableServiceDispatcherOverDuplex<IDuplexSessionChannel, ServerReliableDuplexSessionChannel>
    //{
    //    public ReliableDuplexListenerOverDuplex(ReliableSessionBindingElement binding, BindingContext context)
    //        : base(binding, context)
    //    {
    //    }

    //    protected override bool Duplex
    //    {
    //        get { return true; }
    //    }

    //    protected override ServerReliableDuplexSessionChannel CreateChannel(
    //        UniqueId id,
    //        CreateSequenceInfo createSequenceInfo,
    //        IServerReliableChannelBinder binder)
    //    {
    //        binder.Open(InternalOpenTimeout);
    //        return new ServerReliableDuplexSessionChannel(this, binder, FaultHelper, id, createSequenceInfo.OfferIdentifier);
    //    }

    //    protected override void ProcessSequencedItem(ServerReliableDuplexSessionChannel channel, Message message, WsrmMessageInfo info)
    //    {
    //        channel.ProcessDemuxedMessage(info);
    //    }
    //}

    //internal class ReliableInputServiceDispatcherOverDuplex : ReliableServiceDispatcherOverDuplex<IInputSessionChannel, ReliableInputSessionChannelOverDuplex>
    //{
    //    public ReliableInputListenerOverDuplex(ReliableSessionBindingElement binding, BindingContext context)
    //        : base(binding, context)
    //    {
    //    }

    //    protected override bool Duplex
    //    {
    //        get { return false; }
    //    }

    //    protected override ReliableInputSessionChannelOverDuplex CreateChannel(UniqueId id,
    //        CreateSequenceInfo createSequenceInfo,
    //        IServerReliableChannelBinder binder)
    //    {
    //        binder.Open(InternalOpenTimeout);
    //        return new ReliableInputSessionChannelOverDuplex(this, binder, FaultHelper, id);
    //    }

    //    protected override void ProcessSequencedItem(ReliableInputSessionChannelOverDuplex channel, Message message, WsrmMessageInfo info)
    //    {
    //        channel.ProcessDemuxedMessage(info);
    //    }
    //}

    //internal class ReliableDuplexServiceDispatcherOverDuplexSession : ReliableServiceDispatcherOverDuplexSession<IDuplexSessionChannel, ServerReliableDuplexSessionChannel>
    //{
    //    public ReliableDuplexListenerOverDuplexSession(ReliableSessionBindingElement binding, BindingContext context)
    //        : base(binding, context)
    //    {
    //    }

    //    protected override bool Duplex
    //    {
    //        get { return true; }
    //    }

    //    protected override ServerReliableDuplexSessionChannel CreateChannel(UniqueId id,
    //        CreateSequenceInfo createSequenceInfo,
    //        IServerReliableChannelBinder binder)
    //    {
    //        binder.Open(InternalOpenTimeout);
    //        return new ServerReliableDuplexSessionChannel(this, binder, FaultHelper, id, createSequenceInfo.OfferIdentifier);
    //    }

    //    protected override void ProcessSequencedItemAsync(IDuplexSessionChannel channel, Message message, ServerReliableDuplexSessionChannel reliableChannel, WsrmMessageInfo info, bool newChannel)
    //    {
    //        if (!newChannel)
    //        {
    //            IServerReliableChannelBinder binder = (IServerReliableChannelBinder)reliableChannel.Binder;

    //            if (!binder.UseNewChannel(channel))
    //            {
    //                message.Close();
    //                channel.Abort();
    //                return;
    //            }
    //        }

    //        reliableChannel.ProcessDemuxedMessage(info);
    //    }
    //}

    //internal class ReliableInputServiceDispatcherOverDuplexSession
    //    : ReliableServiceDispatcherOverDuplexSession<IInputSessionChannel, ReliableInputSessionChannelOverDuplex>
    //{
    //    public ReliableInputListenerOverDuplexSession(ReliableSessionBindingElement binding, BindingContext context)
    //        : base(binding, context)
    //    {
    //    }

    //    protected override bool Duplex
    //    {
    //        get { return false; }
    //    }

    //    protected override ReliableInputSessionChannelOverDuplex CreateChannel(UniqueId id,
    //        CreateSequenceInfo createSequenceInfo,
    //        IServerReliableChannelBinder binder)
    //    {
    //        binder.Open(InternalOpenTimeout);
    //        return new ReliableInputSessionChannelOverDuplex(this, binder, FaultHelper, id);
    //    }

    //    protected override void ProcessSequencedItemAsync(IDuplexSessionChannel channel, Message message, ReliableInputSessionChannelOverDuplex reliableChannel, WsrmMessageInfo info, bool newChannel)
    //    {
    //        if (!newChannel)
    //        {
    //            IServerReliableChannelBinder binder = reliableChannel.Binder;

    //            if (!binder.UseNewChannel(channel))
    //            {
    //                message.Close();
    //                channel.Abort();
    //                return;
    //            }
    //        }

    //        reliableChannel.ProcessDemuxedMessage(info);
    //    }
    //}

    //internal class ReliableInputServiceDispatcherOverReply : ReliableServiceDispatcherOverReply<IInputSessionChannel, ReliableInputSessionChannelOverReply>
    //{
    //    public ReliableInputListenerOverReply(ReliableSessionBindingElement binding, BindingContext context)
    //        : base(binding, context)
    //    {
    //    }

    //    protected override bool Duplex
    //    {
    //        get { return false; }
    //    }

    //    protected override ReliableInputSessionChannelOverReply CreateChannel(UniqueId id,
    //        CreateSequenceInfo createSequenceInfo,
    //        IServerReliableChannelBinder binder)
    //    {
    //        binder.Open(InternalOpenTimeout);
    //        return new ReliableInputSessionChannelOverReply(this, binder, FaultHelper, id);
    //    }

    //    protected override void ProcessSequencedItem(ReliableInputSessionChannelOverReply reliableChannel, RequestContext context, WsrmMessageInfo info)
    //    {
    //        reliableChannel.ProcessDemuxedRequest(reliableChannel.Binder.WrapRequestContext(context), info);
    //    }
    //}

    internal class ReliableReplyServiceDispatcherOverReply : ReliableServiceDispatcherOverReply<IReplySessionChannel, ReliableReplySessionChannel>
    {
        public ReliableReplyServiceDispatcherOverReply(ReliableSessionBindingElement binding, BindingContext context, IServiceDispatcher innerDispatcher)
            : base(binding, context, innerDispatcher)
        {
        }

        protected override bool Duplex
        {
            get { return true; }
        }

        protected override async Task<ReliableReplySessionChannel> CreateChannelAsync(UniqueId id,
            CreateSequenceInfo createSequenceInfo,
            IServerReliableChannelBinder binder)
        {
            await binder.OpenAsync(new TimeoutHelper(InternalOpenTimeout).GetCancellationToken());
            return new ReliableReplySessionChannel(this, binder, FaultHelper, id, createSequenceInfo.OfferIdentifier);
        }

        protected override Task ProcessSequencedItemAsync(ReliableReplySessionChannel reliableChannel, RequestContext context, WsrmMessageInfo info)
        {
            return reliableChannel.ProcessDemuxedRequestAsync(reliableChannel.Binder.WrapRequestContext(context), info);
        }
    }

    //internal class ReliableInputServiceDispatcherOverReplySession : ReliableServiceDispatcherOverReplySession<IInputSessionChannel, ReliableInputSessionChannelOverReply>
    //{
    //    public ReliableInputListenerOverReplySession(ReliableSessionBindingElement binding, BindingContext context)
    //        : base(binding, context)
    //    {
    //    }

    //    protected override bool Duplex
    //    {
    //        get { return false; }
    //    }

    //    protected override ReliableInputSessionChannelOverReply CreateChannel(
    //        UniqueId id,
    //        CreateSequenceInfo createSequenceInfo,
    //        IServerReliableChannelBinder binder)
    //    {
    //        binder.Open(InternalOpenTimeout);
    //        return new ReliableInputSessionChannelOverReply(this, binder, FaultHelper, id);
    //    }

    //    protected override void ProcessSequencedItemAsync(IReplySessionChannel channel, RequestContext context, ReliableInputSessionChannelOverReply reliableChannel, WsrmMessageInfo info, bool newChannel)
    //    {
    //        if (!newChannel)
    //        {
    //            IServerReliableChannelBinder binder = reliableChannel.Binder;

    //            if (!binder.UseNewChannel(channel))
    //            {
    //                context.RequestMessage.Close();
    //                context.Abort();
    //                channel.Abort();
    //                return;
    //            }
    //        }

    //        reliableChannel.ProcessDemuxedRequest(reliableChannel.Binder.WrapRequestContext(context), info);
    //    }
    //}

    internal class ReliableReplyServiceDispatcherOverReplySession : ReliableServiceDispatcherOverReplySession<IReplySessionChannel, ReliableReplySessionChannel>
    {
        public ReliableReplyServiceDispatcherOverReplySession(ReliableSessionBindingElement binding, BindingContext context, IServiceDispatcher innerDispatcher)
            : base(binding, context, innerDispatcher)
        {
        }

        protected override bool Duplex
        {
            get { return true; }
        }

        protected override async Task<ReliableReplySessionChannel> CreateChannelAsync(UniqueId id, CreateSequenceInfo createSequenceInfo, IServerReliableChannelBinder binder)
        {
            await binder.OpenAsync(new TimeoutHelper(InternalOpenTimeout).GetCancellationToken());
            return new ReliableReplySessionChannel(this, binder, FaultHelper, id, createSequenceInfo.OfferIdentifier);
        }

        protected override Task ProcessSequencedItemAsync(IReplySessionChannel channel, RequestContext context, ReliableReplySessionChannel reliableChannel, WsrmMessageInfo info, bool newChannel)
        {
            if (!newChannel)
            {
                IServerReliableChannelBinder binder = reliableChannel.Binder;

                if (!binder.UseNewChannel(channel))
                {
                    context.RequestMessage.Close();
                    context.Abort();
                    channel.Abort();
                    return Task.CompletedTask;
                }
            }

            return reliableChannel.ProcessDemuxedRequestAsync(reliableChannel.Binder.WrapRequestContext(context), info);
        }
    }
}
