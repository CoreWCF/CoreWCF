// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using CoreWCF.Dispatcher;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    internal class ChannelDemuxer
    {
        public static readonly TimeSpan UseDefaultReceiveTimeout = TimeSpan.MinValue;
        //private TypedChannelDemuxer _inputDemuxer;
        private TypedChannelDemuxer _replyDemuxer;
        private TimeSpan _peekTimeout;

        public ChannelDemuxer()
        {
            _peekTimeout = UseDefaultReceiveTimeout; //use the default receive timeout (original behavior)
            MaxPendingSessions = 10;
        }

        public TimeSpan PeekTimeout
        {
            get
            {
                return _peekTimeout;
            }
            set
            {
                _peekTimeout = value;
            }
        }

        public int MaxPendingSessions { get; set; }

        internal IServiceDispatcher CreaterServiceDispatcher<TChannel>(IServiceDispatcher innerDispatcher, ChannelDemuxerFilter filter)
        {
            return GetTypedServiceDispatcher<TChannel>().AddDispatcher(innerDispatcher, filter);
        }

        internal IServiceDispatcher CreaterServiceDispatcher<TChannel>(IServiceDispatcher innerDispatcher)
        {
            return GetTypedServiceDispatcher<TChannel>().AddDispatcher(innerDispatcher, new ChannelDemuxerFilter(new MatchAllMessageFilter(), 0));
        }

        internal void RemoveServiceDispatcher<TChannel>(MessageFilter filter)
        {
            GetTypedServiceDispatcher<TChannel>().RemoveDispatcher(filter);
        }

        internal TypedChannelDemuxer GetTypedServiceDispatcher<TChannel>()
        {
            TypedChannelDemuxer typeDemuxer = null;

            //if (typeof(TChannel) == typeof(IInputChannel))
            //{
            //    if (this.inputDemuxer == null)
            //    {
            //        if (context.CanBuildInnerChannelListener<IReplyChannel>())
            //            this.inputDemuxer = this.replyDemuxer = new ReplyChannelDemuxer(context);
            //        else
            //            this.inputDemuxer = new InputChannelDemuxer(context);
            //        createdDemuxer = true;
            //    }
            //    typeDemuxer = this.inputDemuxer;
            //}
            //else
            if (typeof(TChannel) == typeof(IReplyChannel))
            {
                if (_replyDemuxer == null)
                {
                    /*_inputDemuxer = */_replyDemuxer = new ReplyChannelDemuxer();
                }

                typeDemuxer = _replyDemuxer;
            }
            //else if (!this.typeDemuxers.TryGetValue(channelType, out typeDemuxer))
            //{
            //    typeDemuxer = this.CreateTypedDemuxer(channelType, context);
            //    this.typeDemuxers.Add(channelType, typeDemuxer);
            //    createdDemuxer = true;
            //}

            //if (!createdDemuxer)
            //{
            //    context.RemainingBindingElements.Clear();
            //}

            return typeDemuxer;
        }
    }

    internal abstract class TypedChannelDemuxer : IServiceDispatcher
    {
        public abstract Uri BaseAddress { get; }
        public abstract Binding Binding { get; }
        public abstract IList<Type> SupportedChannelTypes { get; }

        public ServiceHostBase Host => throw new NotImplementedException();

        public abstract Task<IServiceChannelDispatcher> CreateServiceChannelDispatcherAsync(IChannel channel);

        public abstract IServiceDispatcher AddDispatcher(IServiceDispatcher innerDispatcher, ChannelDemuxerFilter filter);

        public abstract void RemoveDispatcher(MessageFilter filter);
        internal static void AbortMessage(RequestContext request)
        {
            // RequestContext.RequestMessage can throw an AddressMismatch exception.
            try
            {
                AbortMessage(request.RequestMessage);
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }

                DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
            }
        }

        internal static void AbortMessage(Message message)
        {
            try
            {
                message.Close();
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
        }
    }

    //
    // Datagram demuxers
    //

    internal abstract class DatagramChannelDemuxer<TInnerChannel, TInnerItem> : TypedChannelDemuxer
        where TInnerChannel : class, IChannel
        where TInnerItem : class, IDisposable
    {
        private readonly MessageFilterTable<IServiceDispatcher> _filterTable;

        // since the OnOuterListenerOpen method will be called for every outer listener and we will open
        // the inner listener only once, we need to ensure that all the outer listeners wait till the 
        // inner listener is opened.
        public DatagramChannelDemuxer()
        {
            _filterTable = new MessageFilterTable<IServiceDispatcher>();
        }

        public DatagramChannelDemuxer(IChannelDemuxFailureHandler demuxFailureHandlerPassed)
        {
            _filterTable = new MessageFilterTable<IServiceDispatcher>();
            DemuxFailureHandler = demuxFailureHandlerPassed;
        }

        protected TInnerChannel InnerChannel { get; }

        protected IServiceDispatcher InnerDispatcher { get; }

        protected object ThisLock
        {
            get { return this; }
        }

        protected IChannelDemuxFailureHandler DemuxFailureHandler { get; }

        public override IServiceDispatcher AddDispatcher(IServiceDispatcher innerDispatcher, ChannelDemuxerFilter filter)
        {
            lock (ThisLock)
            {
                _filterTable.Add(filter.Filter, innerDispatcher, filter.Priority);
            }

            return this;
        }

        public override void RemoveDispatcher(MessageFilter filter)
        {
            lock (ThisLock)
            {
                _filterTable.Remove(filter);
            }
        }

        protected abstract void AbortItem(TInnerItem item);
        protected abstract void EndpointNotFound(TInnerItem item);
        protected abstract Message GetMessage(TInnerItem item);

        protected IServiceDispatcher MatchDispatcher(Message message)
        {
            IServiceDispatcher matchingDispatcher = null;
            lock (ThisLock)
            {
                if (_filterTable.GetMatchingValue(message, out matchingDispatcher))
                {
                    return matchingDispatcher;
                }
            }

            return null;
        }
    }

    //class InputChannelDemuxer : DatagramChannelDemuxer<IInputChannel, Message>
    //{
    //    public InputChannelDemuxer(BindingContext context)
    //        : base(context)
    //    {
    //    }

    //    protected override void AbortItem(Message message)
    //    {
    //        AbortMessage(message);
    //    }

    //    protected override IAsyncResult BeginReceive(TimeSpan timeout, AsyncCallback callback, object state)
    //    {
    //        return this.InnerChannel.BeginReceive(timeout, callback, state);
    //    }

    //    protected override LayeredChannelListener<IInputChannel> CreateListener<IInputChannel>(ChannelDemuxerFilter filter)
    //    {
    //        SingletonChannelListener<IInputChannel, InputChannel, Message> listener = new SingletonChannelListener<IInputChannel, InputChannel, Message>(filter, this);
    //        listener.Acceptor = (IChannelAcceptor<IInputChannel>)new InputChannelAcceptor(listener);
    //        return listener;
    //    }

    //    protected override void Dispatch(IChannelListener listener)
    //    {
    //        SingletonChannelListener<IInputChannel, InputChannel, Message> singletonListener = (SingletonChannelListener<IInputChannel, InputChannel, Message>)listener;
    //        singletonListener.Dispatch();
    //    }

    //    protected override void EndpointNotFound(Message message)
    //    {
    //        if (this.DemuxFailureHandler != null)
    //        {
    //            this.DemuxFailureHandler.HandleDemuxFailure(message);
    //        }
    //        this.AbortItem(message);
    //    }

    //    protected override Message EndReceive(IAsyncResult result)
    //    {
    //        return this.InnerChannel.EndReceive(result);
    //    }

    //    protected override void EnqueueAndDispatch(IChannelListener listener, Message message, Action dequeuedCallback, bool canDispatchOnThisThread)
    //    {
    //        SingletonChannelListener<IInputChannel, InputChannel, Message> singletonListener = (SingletonChannelListener<IInputChannel, InputChannel, Message>)listener;
    //        singletonListener.EnqueueAndDispatch(message, dequeuedCallback, canDispatchOnThisThread);
    //    }

    //    protected override void EnqueueAndDispatch(IChannelListener listener, Exception exception, Action dequeuedCallback, bool canDispatchOnThisThread)
    //    {
    //        SingletonChannelListener<IInputChannel, InputChannel, Message> singletonListener = (SingletonChannelListener<IInputChannel, InputChannel, Message>)listener;
    //        singletonListener.EnqueueAndDispatch(exception, dequeuedCallback, canDispatchOnThisThread);
    //    }

    //    protected override Message GetMessage(Message message)
    //    {
    //        return message;
    //    }
    //}

    //class DuplexChannelDemuxer : DatagramChannelDemuxer<IDuplexChannel, Message>
    //{
    //    public DuplexChannelDemuxer(BindingContext context)
    //        : base(context)
    //    {
    //    }

    //    protected override void AbortItem(Message message)
    //    {
    //        AbortMessage(message);
    //    }

    //    protected override IAsyncResult BeginReceive(TimeSpan timeout, AsyncCallback callback, object state)
    //    {
    //        return this.InnerChannel.BeginReceive(timeout, callback, state);
    //    }

    //    protected override LayeredChannelListener<IDuplexChannel> CreateListener<IDuplexChannel>(ChannelDemuxerFilter filter)
    //    {
    //        SingletonChannelListener<IDuplexChannel, DuplexChannel, Message> listener = new SingletonChannelListener<IDuplexChannel, DuplexChannel, Message>(filter, this);
    //        listener.Acceptor = (IChannelAcceptor<IDuplexChannel>)new DuplexChannelAcceptor(listener, this);
    //        return listener;
    //    }

    //    protected override void Dispatch(IChannelListener listener)
    //    {
    //        SingletonChannelListener<IDuplexChannel, DuplexChannel, Message> singletonListener = (SingletonChannelListener<IDuplexChannel, DuplexChannel, Message>)listener;
    //        singletonListener.Dispatch();
    //    }

    //    protected override void EndpointNotFound(Message message)
    //    {
    //        if (this.DemuxFailureHandler != null)
    //        {
    //            this.DemuxFailureHandler.HandleDemuxFailure(message);
    //        }
    //        this.AbortItem(message);
    //    }

    //    protected override Message EndReceive(IAsyncResult result)
    //    {
    //        return this.InnerChannel.EndReceive(result);
    //    }

    //    protected override void EnqueueAndDispatch(IChannelListener listener, Message message, Action dequeuedCallback, bool canDispatchOnThisThread)
    //    {
    //        SingletonChannelListener<IDuplexChannel, DuplexChannel, Message> singletonListener = (SingletonChannelListener<IDuplexChannel, DuplexChannel, Message>)listener;
    //        singletonListener.EnqueueAndDispatch(message, dequeuedCallback, canDispatchOnThisThread);
    //    }

    //    protected override void EnqueueAndDispatch(IChannelListener listener, Exception exception, Action dequeuedCallback, bool canDispatchOnThisThread)
    //    {
    //        SingletonChannelListener<IDuplexChannel, DuplexChannel, Message> singletonListener = (SingletonChannelListener<IDuplexChannel, DuplexChannel, Message>)listener;
    //        singletonListener.EnqueueAndDispatch(exception, dequeuedCallback, canDispatchOnThisThread);
    //    }

    //    protected override Message GetMessage(Message message)
    //    {
    //        return message;
    //    }

    //    class DuplexChannelAcceptor : SingletonChannelAcceptor<IDuplexChannel, DuplexChannel, Message>
    //    {
    //        DuplexChannelDemuxer demuxer;

    //        public DuplexChannelAcceptor(ChannelManagerBase channelManager, DuplexChannelDemuxer demuxer)
    //            : base(channelManager)
    //        {
    //            this.demuxer = demuxer;
    //        }

    //        protected override DuplexChannel OnCreateChannel()
    //        {
    //            return new DuplexChannelWrapper(this.ChannelManager, demuxer.InnerChannel);
    //        }

    //        protected override void OnTraceMessageReceived(Message message)
    //        {
    //            if (DiagnosticUtility.ShouldTraceInformation)
    //            {
    //                TraceUtility.TraceEvent(TraceEventType.Information, TraceCode.MessageReceived, SR.GetString(SR.TraceCodeMessageReceived),
    //                    MessageTransmitTraceRecord.CreateReceiveTraceRecord(message), this, null);
    //            }
    //        }
    //    }

    //    class DuplexChannelWrapper : DuplexChannel
    //    {
    //        IDuplexChannel innerChannel;

    //        public DuplexChannelWrapper(ChannelManagerBase channelManager, IDuplexChannel innerChannel)
    //            : base(channelManager, innerChannel.LocalAddress)
    //        {
    //            this.innerChannel = innerChannel;
    //        }

    //        public override EndpointAddress RemoteAddress
    //        {
    //            get { return this.innerChannel.RemoteAddress; }
    //        }

    //        public override Uri Via
    //        {
    //            get { return this.innerChannel.Via; }
    //        }

    //        protected override void OnSend(Message message, TimeSpan timeout)
    //        {
    //            this.innerChannel.Send(message, timeout);
    //        }

    //        protected override IAsyncResult OnBeginSend(Message message, TimeSpan timeout, AsyncCallback callback, object state)
    //        {
    //            return this.innerChannel.BeginSend(message, timeout, callback, state);
    //        }

    //        protected override void OnEndSend(IAsyncResult result)
    //        {
    //            this.innerChannel.EndSend(result);
    //        }

    //        protected override IAsyncResult OnBeginOpen(TimeSpan timeout, AsyncCallback callback, object state)
    //        {
    //            return new CompletedAsyncResult(callback, state);
    //        }

    //        protected override void OnEndOpen(IAsyncResult result)
    //        {
    //            CompletedAsyncResult.End(result);
    //        }

    //        protected override void OnOpen(TimeSpan timeout)
    //        {
    //        }
    //    }
    //}

    internal class ReplyChannelDemuxer : DatagramChannelDemuxer<IReplyChannel, RequestContext>
    {
        private static readonly IList<Type> s_supportedChannelTypes = new List<Type> { typeof(IReplyChannel) };

        public override Uri BaseAddress => throw new NotImplementedException();

        public override Binding Binding => throw new NotImplementedException();

        public override IList<Type> SupportedChannelTypes => s_supportedChannelTypes;

        public ReplyChannelDemuxer()
        {
        }

        public override Task<IServiceChannelDispatcher> CreateServiceChannelDispatcherAsync(IChannel channel)
        {
            return Task.FromResult<IServiceChannelDispatcher>(new ReplyChannelDispatcher(this, channel));
        }

        protected override void AbortItem(RequestContext request)
        {
            AbortMessage(request);
            request.Abort();
        }

        protected override void EndpointNotFound(RequestContext request)
        {
            //bool abortItem = true;
            //try
            //{
            //    if (this.DemuxFailureHandler != null)
            //    {
            //        try
            //        {
            //            ReplyChannelDemuxFailureAsyncResult result = new ReplyChannelDemuxFailureAsyncResult(this.DemuxFailureHandler, request, Fx.ThunkCallback(new AsyncCallback(this.EndpointNotFoundCallback)), request);
            //            result.Start();
            //            if (!result.CompletedSynchronously)
            //            {
            //                abortItem = false;
            //                return;
            //            }
            //            ReplyChannelDemuxFailureAsyncResult.End(result);
            //            abortItem = false;
            //        }
            //        catch (CommunicationException e)
            //        {
            //            DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
            //        }
            //        catch (TimeoutException e)
            //        {
            //            if (TD.SendTimeoutIsEnabled())
            //            {
            //                TD.SendTimeout(e.Message);
            //            }
            //            DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
            //        }
            //        catch (ObjectDisposedException e)
            //        {
            //            DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
            //        }
            //        catch (Exception e)
            //        {
            //            if (Fx.IsFatal(e)) throw;
            //            this.HandleUnknownException(e);
            //        }
            //    }
            //}
            //finally
            //{
            //    if (abortItem)
            //    {
            //        this.AbortItem(request);
            //    }
            //}
        }


        protected override Message GetMessage(RequestContext request)
        {
            return request.RequestMessage;
        }

        internal class ReplyChannelDispatcher : IServiceChannelDispatcher
        {
            private readonly ReplyChannelDemuxer _demuxer;
            private readonly IChannel _channel;

            public ReplyChannelDispatcher(ReplyChannelDemuxer replyChannelDemuxer, IChannel channel)
            {
                _demuxer = replyChannelDemuxer;
                _channel = channel;
            }

            public async Task DispatchAsync(RequestContext context)
            {
                // TODO: Find way to avoid instantiating a new ServiceChannelDispatcher each time
                IServiceDispatcher serviceDispatcher = _demuxer.MatchDispatcher(context.RequestMessage);
                if (serviceDispatcher == null)
                {
                    ErrorBehavior.ThrowAndCatch(
                        new EndpointNotFoundException(SR.Format(SR.UnableToDemuxChannel, context.RequestMessage.Headers.Action)), context.RequestMessage);
                }
                // TODO: if serviceDispatcher == null, use the EndpointNotFound code path
                IServiceChannelDispatcher serviceChannelDispatcher = await serviceDispatcher.CreateServiceChannelDispatcherAsync(_channel);
                await serviceChannelDispatcher.DispatchAsync(context);
            }

            public Task DispatchAsync(Message message)
            {
                throw new NotImplementedException();
            }
        }
    }

    internal interface IChannelDemuxerFilter
    {
        ChannelDemuxerFilter Filter { get; }
    }

    internal class ChannelDemuxerFilter
    {
        public ChannelDemuxerFilter(MessageFilter filter, int priority)
        {
            Filter = filter;
            Priority = priority;
        }

        public MessageFilter Filter { get; }

        public int Priority { get; }
    }

    internal interface IChannelDemuxFailureHandler
    {
        Task HandleDemuxFailureAsync(Message message);
        Task HandleDemuxFailureAsync(Message message, RequestContext faultContext);
    }
}
