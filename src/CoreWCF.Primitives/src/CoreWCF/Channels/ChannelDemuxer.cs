// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Web.Services.Description;
using CoreWCF.Configuration;
using CoreWCF.Diagnostics;
using CoreWCF.Dispatcher;
using CoreWCF.Runtime;
using static CoreWCF.Channels.ReplyChannelDemuxer;
using static CoreWCF.Security.SecuritySessionServerSettings;

namespace CoreWCF.Channels
{
    internal class ChannelDemuxer
    {
        public static readonly TimeSpan UseDefaultReceiveTimeout = TimeSpan.MinValue;
        //private TypedChannelDemuxer _inputDemuxer;
        private TypedChannelDemuxer _replyDemuxer;
        private Dictionary<Type, TypedChannelDemuxer> _typeDemuxers;
        private object _thisLock = new object(); // Used to protect acces to _typedDemuxers and _replyDemuxer

        private TimeSpan _peekTimeout;

        public ChannelDemuxer()
        {
            _peekTimeout = UseDefaultReceiveTimeout; //use the default receive timeout (original behavior)
            MaxPendingSessions = 10;
            _typeDemuxers = new Dictionary<Type, TypedChannelDemuxer>();
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

        internal IServiceDispatcher CreateServiceDispatcher<TChannel>(IServiceDispatcher innerDispatcher, ChannelDemuxerFilter filter, BindingParameterCollection bindingParameters)
        {
            return GetTypedServiceDispatcher<TChannel>(bindingParameters).AddDispatcher(innerDispatcher, filter);
        }

        internal IServiceDispatcher CreateServiceDispatcher<TChannel>(IServiceDispatcher innerDispatcher, BindingParameterCollection bindingParameters)
        {
            return GetTypedServiceDispatcher<TChannel>(bindingParameters).AddDispatcher(innerDispatcher, new ChannelDemuxerFilter(new MatchAllMessageFilter(), 0));
        }

        internal void RemoveServiceDispatcher<TChannel>(MessageFilter filter, BindingParameterCollection bindingParameters)
        {
            // Don't create if it doesn't already exist as the filter can't be held by a non-existent demuxer
            TryGetTypedServiceDispatcher(typeof(TChannel), bindingParameters)?.RemoveDispatcher(filter);
        }

        internal TypedChannelDemuxer GetTypedServiceDispatcher<TChannel>(BindingParameterCollection bindingParameters)
        {
            return GetTypedServiceDispatcher(typeof(TChannel), bindingParameters);
        }

        internal TypedChannelDemuxer TryGetTypedServiceDispatcher(Type channelType, BindingParameterCollection bindingParameters)
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
            Type parentType = GetParentType(channelType);
            if (parentType == typeof(IReplyChannel))
            {
                typeDemuxer = _replyDemuxer;
            }
            else
            {
                lock (_thisLock)
                {
                    if (!_typeDemuxers.TryGetValue(parentType, out typeDemuxer))
                    {
                        typeDemuxer = null; // When not found, technically typeDemuxer will be undefined
                    }
                }
            }

            //if (!createdDemuxer)
            //{
            //    context.RemainingBindingElements.Clear();
            //}

            return typeDemuxer;
        }

        internal TypedChannelDemuxer GetTypedServiceDispatcher(Type channelType, BindingParameterCollection bindingParameters)
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
            Type parentType = GetParentType(channelType);
            if (parentType == typeof(IReplyChannel))
            {
                if (_replyDemuxer == null)
                {
                    lock (_thisLock)
                    {
                        if (_replyDemuxer == null)
                        {
                            _replyDemuxer = new ReplyChannelDemuxer(bindingParameters);
                        }
                    }
                }

                typeDemuxer = _replyDemuxer;
            }
            else
            {
                lock (_thisLock)
                {
                    if (!_typeDemuxers.TryGetValue(parentType, out typeDemuxer))
                    {
                        typeDemuxer = CreateTypedDemuxer(channelType, bindingParameters);
                        _typeDemuxers.Add(channelType, typeDemuxer);
                    }
                }
            }

            //if (!createdDemuxer)
            //{
            //    context.RemainingBindingElements.Clear();
            //}

            return typeDemuxer;
        }

        private TypedChannelDemuxer CreateTypedDemuxer(Type channelType, BindingParameterCollection bindingParameters)
        {
            /* if (channelType == typeof(IInputSessionChannel))
                 return (TypedChannelDemuxer)(object)new InputSessionChannelDemuxer(context, this.peekTimeout, this.maxPendingSessions);
             if (channelType == typeof(IReplySessionChannel))
                 return (TypedChannelDemuxer)(object)new ReplySessionChannelDemuxer(context, this.peekTimeout, this.maxPendingSessions);*/
            if (channelType == typeof(IDuplexChannel))
                return (TypedChannelDemuxer)(object)new DuplexChannelDemuxer(bindingParameters);//, this.peekTimeout, this.maxPendingSessions);
            if (channelType == typeof(IDuplexSessionChannel))
                return (TypedChannelDemuxer)(object)new DuplexSessionChannelDemuxer(bindingParameters);//, this.peekTimeout, this.maxPendingSessions);
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException());
        }

        private Type GetParentType(Type originalType)
        {
            if (typeof(IDuplexSessionChannel).IsAssignableFrom(originalType))
                return typeof(IDuplexSessionChannel);
            if (typeof(IReplyChannel).IsAssignableFrom(originalType))
                return typeof(IReplyChannel);
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException());
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
    // Session demuxers
    //

    internal abstract class SessionChannelDemuxer<TInnerChannel, TInnerItem> : TypedChannelDemuxer
        where TInnerChannel : class, IChannel
        where TInnerItem : class, IDisposable
    {
        private readonly MessageFilterTable<IServiceDispatcher> _filterTable;
        public SessionChannelDemuxer(BindingParameterCollection bindingParameters)//, TimeSpan peekTimeout, int maxPendingSessions)
        {
            _filterTable = new MessageFilterTable<IServiceDispatcher>();
            DemuxFailureHandler = bindingParameters.Find<IChannelDemuxFailureHandler>();
        }

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
        protected abstract Task EndpointNotFoundAsync(TInnerChannel channel, TInnerItem item);
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

    internal class DuplexSessionChannelDemuxer : SessionChannelDemuxer<IDuplexSessionChannel, Message>
    {
        public DuplexSessionChannelDemuxer(BindingParameterCollection bindingParameters)//, TimeSpan peekTimeout, int maxPendingSessions)
            : base(bindingParameters)//, peekTimeout, maxPendingSessions)
        {
        }

        public override Uri BaseAddress => throw new NotImplementedException();

        public override Binding Binding => throw new NotImplementedException();

        public override IList<Type> SupportedChannelTypes => throw new NotImplementedException();

        public override Task<IServiceChannelDispatcher> CreateServiceChannelDispatcherAsync(IChannel channel)
        {
            return Task.FromResult<IServiceChannelDispatcher>(new DuplexSessionChannelDispatcher(this, (IDuplexSessionChannel)channel));
        }

        protected override void AbortItem(Message message)
        {
            AbortMessage(message);
        }

        protected override async Task EndpointNotFoundAsync(IDuplexSessionChannel channel, Message message)
        {
            bool abortItem = true;
            try
            {
                if (DemuxFailureHandler != null)
                {
                    var duplexSessionRequestContext = new DuplexSessionRequestContext(channel, message);
                    await DemuxFailureHandler.HandleDemuxFailureAsync(message, duplexSessionRequestContext);
                    abortItem = false;
                }

            }
            catch (CommunicationException e)
            {
                DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
            }
            catch (TimeoutException e)
            {
                DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
            }
            catch (ObjectDisposedException e)
            {
                DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e)) throw;
                throw;
            }
            finally
            {
                if (abortItem)
                {
                    AbortMessage(message);
                    channel.Abort();
                }
            }
        }

        protected override Message GetMessage(Message message)
        {
            return message;
        }

        internal class DuplexSessionChannelDispatcher : IServiceChannelDispatcher
        {
            private readonly DuplexSessionChannelDemuxer _demuxer;
            private readonly IDuplexSessionChannel _channel;
            private IServiceChannelDispatcher _serviceChannelDispatcher;

            public DuplexSessionChannelDispatcher(DuplexSessionChannelDemuxer replyChannelDemuxer, IDuplexSessionChannel channel)
            {
                _demuxer = replyChannelDemuxer;
                _channel = channel;
                channel.OpenAsync();
            }

            public Task DispatchAsync(RequestContext context)
            {
                return Task.FromException(new NotImplementedException());
            }

            public async Task DispatchAsync(Message message)
            {
                if (message == null) //0 bytes
                {
                    //We have already closed all channels, return. (Couldn't use DoneReceivingInCurrentState())
                    if (_channel.State == CommunicationState.Closed
                        || _channel.State == CommunicationState.Closing
                        || _channel.State == CommunicationState.Closed)
                    {
                        return;
                    }
                    else
                    {
                        await _serviceChannelDispatcher.DispatchAsync(message);
                        return;
                    }
                }
                IServiceDispatcher serviceDispatcher = _demuxer.MatchDispatcher(message);
                if (serviceDispatcher == null)
                {
                    ErrorBehavior.ThrowAndCatch(
                        new EndpointNotFoundException(SR.Format(SR.UnableToDemuxChannel, message.Headers.Action)), message);
                    await _demuxer.EndpointNotFoundAsync((IDuplexSessionChannel) _channel, message);
                    return;
                }
                _serviceChannelDispatcher = await serviceDispatcher.CreateServiceChannelDispatcherAsync(_channel);
                await _serviceChannelDispatcher.DispatchAsync(message);
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
        public DatagramChannelDemuxer(BindingParameterCollection bindingParameters)
        {
            _filterTable = new MessageFilterTable<IServiceDispatcher>();
            DemuxFailureHandler = bindingParameters.Find<IChannelDemuxFailureHandler>();
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
        protected abstract Task EndpointNotFoundAsync(TInnerItem item);
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

    internal class DuplexChannelDemuxer : DatagramChannelDemuxer<IDuplexChannel, Message>
    {
        private static readonly IList<Type> s_supportedChannelTypes = new List<Type> { typeof(IDuplexChannel) };

        public override Uri BaseAddress => throw new NotImplementedException();

        public override Binding Binding => throw new NotImplementedException();

        public override IList<Type> SupportedChannelTypes => s_supportedChannelTypes;

        public DuplexChannelDemuxer(BindingParameterCollection bindingParameters) : base(bindingParameters)
        {
        }

        public override Task<IServiceChannelDispatcher> CreateServiceChannelDispatcherAsync(IChannel channel)
        {
            return Task.FromResult<IServiceChannelDispatcher>(new DuplexChannelDispatcher(this, channel));
        }

        protected override void AbortItem(Message message)
        {
            AbortMessage(message);
        }

        protected override async Task EndpointNotFoundAsync(Message message)
        {
            bool abortItem = true;
            try
            {
                if (DemuxFailureHandler != null)
                {
                    await DemuxFailureHandler.HandleDemuxFailureAsync(message);
                    abortItem = false;
                }
            }
            catch (CommunicationException e)
            {
                DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
            }
            catch (TimeoutException e)
            {
                DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
            }
            catch (ObjectDisposedException e)
            {
                DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e)) throw;
                throw;
            }
            finally
            {
                if (abortItem)
                {
                    AbortItem(message);
                }
            }
        }

        protected override Message GetMessage(Message message)
        {
            return message;
        }

        internal class DuplexChannelDispatcher : IServiceChannelDispatcher
        {
            private readonly DuplexChannelDemuxer _demuxer;
            private readonly IChannel _channel;

            public DuplexChannelDispatcher(DuplexChannelDemuxer duplexChannelDemuxer, IChannel channel)
            {
                _demuxer = duplexChannelDemuxer;
                _channel = channel;
            }

            public Task DispatchAsync(RequestContext context)
            {
                return Task.FromException(new NotImplementedException());
            }

            public async Task DispatchAsync(Message message)
            {
                // TODO: Find way to avoid instantiating a new ServiceChannelDispatcher each time
                IServiceDispatcher serviceDispatcher = _demuxer.MatchDispatcher(message);
                if (serviceDispatcher == null)
                {
                    ErrorBehavior.ThrowAndCatch(
                        new EndpointNotFoundException(SR.Format(SR.UnableToDemuxChannel, message.Headers.Action)), message);
                    await _demuxer.EndpointNotFoundAsync(message);
                    return;
                }
                // TODO: if serviceDispatcher == null, use the EndpointNotFound code path
                IServiceChannelDispatcher serviceChannelDispatcher = await serviceDispatcher.CreateServiceChannelDispatcherAsync(_channel);
                await serviceChannelDispatcher.DispatchAsync(message);
            }
        }
    }

    internal class ReplyChannelDemuxer : DatagramChannelDemuxer<IReplyChannel, RequestContext>
    {
        private static readonly IList<Type> s_supportedChannelTypes = new List<Type> { typeof(IReplyChannel) };

        public override Uri BaseAddress => throw new NotImplementedException();

        public override Binding Binding => throw new NotImplementedException();

        public override IList<Type> SupportedChannelTypes => s_supportedChannelTypes;

        public ReplyChannelDemuxer(BindingParameterCollection bindingParameters) : base(bindingParameters)
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

        protected override async Task EndpointNotFoundAsync(RequestContext request)
        {
            bool abortItem = true;
            try
            {
                if (DemuxFailureHandler != null)
                {
                    try
                    {
                       await DemuxFailureHandler.HandleDemuxFailureAsync(request.RequestMessage, request);
                       abortItem = false;
                    }
                    catch (CommunicationException e)
                    {
                        DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                    }
                    catch (TimeoutException e)
                    {
                        DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                    }
                    catch (ObjectDisposedException e)
                    {
                        DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                    }
                    catch (Exception e)
                    {
                        if (Fx.IsFatal(e)) throw;
                        throw e;
                    }
                }
            }
            finally
            {
                if (abortItem)
                {
                    AbortItem(request);
                }
            }
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
                    await _demuxer.EndpointNotFoundAsync(context);
                    return;
                }
                // TODO: if serviceDispatcher == null, use the EndpointNotFound code path
                IServiceChannelDispatcher serviceChannelDispatcher = await serviceDispatcher.CreateServiceChannelDispatcherAsync(_channel);
                await serviceChannelDispatcher.DispatchAsync(context);
            }

            public Task DispatchAsync(Message message)
            {
               return Task.FromException(new NotImplementedException());
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
