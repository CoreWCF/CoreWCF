// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Runtime;
using CoreWCF.Security;
using Microsoft.Extensions.DependencyInjection;

namespace CoreWCF.Dispatcher
{
    /// <summary>
    /// This is equivalent of SecurityChannelListener present in WCF codebase
    /// </summary>
    internal class SecurityServiceDispatcher : CommunicationObject, IServiceDispatcher, IDisposable
    {
        private readonly BindingContext _bindingContext;
        private ChannelBuilder _channelBuilder;
        // TODO Investigate is we need to implement ComputeEndpointIdentity, see issue #284
        //private readonly EndpointIdentity _identity;
        private SecurityProtocolFactory _securityProtocolFactory;
        private SecuritySessionServerSettings _sessionServerSettings;
        private SecurityListenerSettingsLifetimeManager _settingsLifetimeManager;
        private bool _hasSecurityStateReference;

        //ServiceChannelDispatcher to call SCT (just keep one instance)
        private volatile IServiceChannelDispatcher _securityAuthServiceChannelDispatcher;
        private Task<IServiceChannelDispatcher> _channelTask;
        private bool _disposed = false;

        private TimeSpan _closeTimeout = ServiceDefaults.CloseTimeout;
        private TimeSpan _openTimeout = ServiceDefaults.OpenTimeout;
        private TimeSpan _sendTimeout = ServiceDefaults.SendTimeout;

        public SecurityServiceDispatcher(BindingContext context, IServiceDispatcher serviceDispatcher)
        {
            InnerServiceDispatcher = serviceDispatcher;
            _bindingContext = context;
            // this.securityProtocolFactory =  securityProtocolFactory; // we set it later from TransportSecurityBindingElement
            //  this.settingsLifetimeManager = new SecurityListenerSettingsLifetimeManager(this.securityProtocolFactory, this.sessionServerSettings, this.sessionMode, this.InnerChannelListener);
            _closeTimeout = context.Binding.CloseTimeout;
            _openTimeout = context.Binding.CloseTimeout;
            _sendTimeout = context.Binding.SendTimeout;
        }

        internal ChannelBuilder ChannelBuilder
        {
            get
            {
                ThrowIfDisposed();
                return _channelBuilder;
            }
        }

        public Uri BaseAddress => InnerServiceDispatcher.BaseAddress;

        public Binding Binding => _bindingContext.Binding;

        public IServiceDispatcher InnerServiceDispatcher { get; set; }

        public IServiceDispatcher SecurityAuthServiceDispatcher { get; set; }

        public ICollection<Type> SupportedChannelTypes => InnerServiceDispatcher.SupportedChannelTypes;

        private AsyncLock AsyncLock { get; } = new AsyncLock();

        protected override TimeSpan DefaultCloseTimeout => _closeTimeout;

        protected override TimeSpan DefaultOpenTimeout => _openTimeout;

        internal TimeSpan DefaultSendTimeout => _sendTimeout;

        public SecurityProtocolFactory SecurityProtocolFactory
        {
            get
            {
                ThrowIfDisposed();
                return _securityProtocolFactory;
            }
            set
            {
                ThrowIfDisposed();
                _securityProtocolFactory = value ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
            }
        }

        public bool SessionMode { get; set; }

        internal SecuritySessionServerSettings SessionServerSettings
        {
            get
            {
                if (_sessionServerSettings == null)
                {
                    using (AsyncLock.TakeLock())
                    {
                        if (_sessionServerSettings == null)
                        {
                            SecuritySessionServerSettings tmp = new SecuritySessionServerSettings();
                            Thread.MemoryBarrier();
                            tmp.SecurityServiceDispatcher = this;
                            _sessionServerSettings = tmp;
                        }
                    }
                }
                return _sessionServerSettings;
            }
        }

        public bool SendUnsecuredFaults { get; set; } = true;

        public IChannel OuterChannel { get; set; }

        public Type AcceptorChannelType { get; set; }

        IList<Type> IServiceDispatcher.SupportedChannelTypes => throw new NotImplementedException();

        public ServiceHostBase Host => InnerServiceDispatcher.Host;

        internal void InitializeSecurityDispatcher<TChannel>(ChannelBuilder channelBuilder) where TChannel : class, IChannel
        {
            _channelBuilder = channelBuilder;

            if (SessionMode)
            {
                _sessionServerSettings.ChannelBuilder = ChannelBuilder;
                channelBuilder.BuildServiceDispatcher<TChannel>(this);
                //this.InnerChannelListener = this.sessionServerSettings.CreateInnerChannelListener();
                // this.Acceptor = this.sessionServerSettings.CreateAcceptor<TChannel>();
                AcceptorChannelType = typeof(TChannel);
                _sessionServerSettings.AcceptorChannelType = typeof(TChannel);
            }
            else
            {
                //  throw new PlatformNotSupportedException();
                //TODO later

                channelBuilder.BuildServiceDispatcher<TChannel>(this);
                // this.InnerChannelListener = this.ChannelBuilder.BuildChannelListener<TChannel>();
                // this.Acceptor = (IChannelAcceptor<TChannel>)new SecurityChannelAcceptor(this,
                //     (IChannelListener<TChannel>)InnerChannelListener, this.securityProtocolFactory.CreateListenerSecurityState());
            }
        }

        private void InitializeServiceDispatcherSecurityState()
        {
            if (SessionMode)
            {
                SessionServerSettings.SessionProtocolFactory.ListenUri = InnerServiceDispatcher.BaseAddress;
                SessionServerSettings.SecurityServiceDispatcher = this;
            }
            else
            {
                ThrowIfProtocolFactoryNotSet();
                _securityProtocolFactory.ListenUri = InnerServiceDispatcher.BaseAddress;
            }

            _settingsLifetimeManager = new SecurityListenerSettingsLifetimeManager(_securityProtocolFactory, _sessionServerSettings, SessionMode);//, this.InnerChannelListener);
            if (_sessionServerSettings != null)
            {
                _sessionServerSettings.SettingsLifetimeManager = _settingsLifetimeManager;
            }

            _hasSecurityStateReference = true;
        }

        protected override void OnAbort()
        {
            using (AsyncLock.TakeLock())
            {
                if (_hasSecurityStateReference)
                {
                    _hasSecurityStateReference = false;
                    if (_settingsLifetimeManager != null)
                    {
                        _settingsLifetimeManager.Abort();
                    }
                }
            }

            if (InnerServiceDispatcher is CommunicationObject co) co.Abort();
        }

        protected override async Task OnCloseAsync(CancellationToken token)
        {
            if (_sessionServerSettings != null)
            {
                _sessionServerSettings.StopAcceptingNewWork();
            }
            await using (await AsyncLock.TakeLockAsync())
            {
                if (_hasSecurityStateReference)
                {
                    _hasSecurityStateReference = false;
                    await _settingsLifetimeManager.CloseAsync(token);
                }
            }

            if (InnerServiceDispatcher is CommunicationObject co) await co.CloseAsync(token);
        }

        protected override async Task OnOpenAsync(CancellationToken token)
        {
            // TODO: Work out how to get the ExtendedProtectionPolicy settings and then light up this code path
            //EnableChannelBindingSupport();
            await using (await AsyncLock.TakeLockAsync())
            {
                // if an abort happened before the Open, return
                if (State == CommunicationState.Closing && State == CommunicationState.Closed)
                {
                    return;
                }

                InitializeServiceDispatcherSecurityState();
            }

            await _settingsLifetimeManager.OpenAsync(token);
        }

        //private 

        // This method should only be called at Open time, since it looks up the identity based on the 
        // thread token
        //void ComputeEndpointIdentity()
        //{
        //    EndpointIdentity result = null;
        //    if (this.State == CommunicationState.Opened)
        //    {
        //        if (this.SecurityProtocolFactory != null)
        //        {
        //            result = this.SecurityProtocolFactory.GetIdentityOfSelf();
        //        }
        //        else if (this.SessionServerSettings != null && this.SessionServerSettings.SessionProtocolFactory != null)
        //        {
        //            result = this.SessionServerSettings.SessionProtocolFactory.GetIdentityOfSelf();
        //        }
        //    }
        //    if (result == null)
        //    {
        //        result = base.GetProperty<EndpointIdentity>();
        //    }
        //    this.identity = result;
        //}

        private void ThrowIfProtocolFactoryNotSet()
        {
            if (_securityProtocolFactory == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SecurityProtocolFactoryShouldBeSetBeforeThisOperation)));
            }
        }

        public async Task<IServiceChannelDispatcher> CreateServiceChannelDispatcherAsync(IChannel outerChannel)
        {
            //TODO should have better logic
            //Initialization path start
            if (outerChannel.ChannelDispatcher == null && SessionMode)
            {
                TypedChannelDemuxer typedChannelDemuxer = ChannelBuilder.GetTypedChannelDemuxer(outerChannel.GetType());
                IServiceChannelDispatcher channelDispatcher = await typedChannelDemuxer.CreateServiceChannelDispatcherAsync(outerChannel);
                return channelDispatcher;
            }
            //initialization end
            //Below dispatches all SCT call for first time for all clients
            else
            {
                IServiceChannelDispatcher securityReplyChannelDispatcher = await GetInnerChannelDispatcherAsync(outerChannel);
                return securityReplyChannelDispatcher;
            }
        }

        internal async Task<IServiceChannelDispatcher> GetAuthChannelDispatcher(IChannel outerChannel)
        {
            if (_securityAuthServiceChannelDispatcher == null)
            {
                using (AsyncLock.TakeLock())
                {
                    if (_channelTask == null)
                    {
                        _channelTask = SecurityAuthServiceDispatcher.CreateServiceChannelDispatcherAsync(outerChannel);
                    }
                }
                _securityAuthServiceChannelDispatcher = await _channelTask;
                Thread.MemoryBarrier();
            }
            return _securityAuthServiceChannelDispatcher;
        }


        /// <summary>
        /// Return same instance of real service channel dispatcher.
        /// This method is called by SecurityReplySessionServiceChannelDispatcher(SecuritySessionServerSettings) to dispatch real service 
        /// </summary>
        /// <param name="outerChannel"></param>
        /// <returns></returns>
        internal async Task<IServiceChannelDispatcher> GetInnerServiceChannelDispatcher(IChannel outerChannel)
        {
            await using (await AsyncLock.TakeLockAsync())
            {
                return await InnerServiceDispatcher.CreateServiceChannelDispatcherAsync(outerChannel);
            }
        }

        //Reference OnAcceptChannel/SecurityChannelListner
        private async Task<IServiceChannelDispatcher> GetInnerChannelDispatcherAsync(IChannel outerChannel)
        {
            IServiceChannelDispatcher securityChannelDispatcher = null;
            SecurityProtocol securityProtocol = SecurityProtocolFactory.CreateSecurityProtocol(null, null,
            (outerChannel is IReplyChannel || outerChannel is IReplySessionChannel), TimeSpan.Zero);
            await securityProtocol.OpenAsync(TimeSpan.Zero);
            /* TODO once we add more features
            if (outerChannel is IInputChannel)
            {
                securityChannel = new SecurityInputChannel(listener, (IInputChannel)innerChannel, securityProtocol, listener.settingsLifetimeManager);
            }
            else if (outerChannel is IInputSessionChannel))
            {
                securityChannel = new SecurityInputSessionChannel(listener, (IInputSessionChannel)innerChannel, securityProtocol, listener.settingsLifetimeManager);
            }
            else if (outerChannel is IDuplexChannel))
            {
                securityChannel = new SecurityDuplexChannel(listener, (IDuplexChannel)innerChannel, securityProtocol, listener.settingsLifetimeManager);
            }
            else*/
            if (outerChannel is IDuplexSessionChannel duplexSessionChannel)
            {
                securityChannelDispatcher = new SecurityDuplexSessionChannelDispatcher(this, duplexSessionChannel, securityProtocol, _settingsLifetimeManager);
            }
            else if (outerChannel is IReplyChannel replyChannel)
            {
                securityChannelDispatcher = new SecurityReplyChannelDispatcher(this, replyChannel, securityProtocol, _settingsLifetimeManager);
            }
            /* else if (listener.SupportsRequestReply && typeof(TChannel) == typeof(IReplySessionChannel))
             {
                 securityChannel = new SecurityReplySessionChannel(listener, (IReplySessionChannel)innerChannel, securityProtocol, listener.settingsLifetimeManager);
             }
             else
             {
                 throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.GetString(SR.UnsupportedChannelInterfaceType, typeof(TChannel))));
             }*/
            return securityChannelDispatcher;
        }

        public void Dispose()
        {
            CloseAsync().GetAwaiter().GetResult();
        }
    }

    internal abstract class ServerSecurityChannelDispatcher<UChannel> : IServiceChannelDispatcher where UChannel : class
    {
        private static MessageFault s_secureConversationCloseNotSupportedFault;
        private readonly IServiceProvider _serviceProvider;
        private readonly string _secureConversationCloseAction;

        protected ServerSecurityChannelDispatcher(SecurityServiceDispatcher securityServiceDispatcher, UChannel innerChannel, SecurityProtocol securityProtocol, SecurityListenerSettingsLifetimeManager settingsLifetimeManager)
        {
            SecurityProtocol = securityProtocol;
            OuterChannel = (IChannel)innerChannel;
            _serviceProvider = OuterChannel.GetProperty<IServiceScopeFactory>().CreateScope().ServiceProvider;
            _secureConversationCloseAction = securityProtocol.SecurityProtocolFactory.StandardsManager.SecureConversationDriver.CloseAction.Value;
        }

        internal SecurityProtocol SecurityProtocol { get; set; }

        public IChannel OuterChannel { get; private set; }

        public T GetProperty<T>() where T : class
        {
            T tObj = _serviceProvider.GetService<T>();
            if (tObj == null)
                return OuterChannel.GetProperty<T>();
            else return tObj;
        }

        private static MessageFault GetSecureConversationCloseNotSupportedFault()
        {
            if (s_secureConversationCloseNotSupportedFault == null)
            {
                FaultCode faultCode = FaultCode.CreateSenderFaultCode(DotNetSecurityStrings.SecureConversationCancelNotAllowedFault, DotNetSecurityStrings.Namespace);
                FaultReason faultReason = new FaultReason(SR.Format(SR.SecureConversationCancelNotAllowedFaultReason), System.Globalization.CultureInfo.InvariantCulture);
                s_secureConversationCloseNotSupportedFault = MessageFault.CreateFault(faultCode, faultReason);
            }
            return s_secureConversationCloseNotSupportedFault;
        }

        private void ThrowIfSecureConversationCloseMessage(Message message)
        {
            if (message.Headers.Action == _secureConversationCloseAction)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.SecureConversationCancelNotAllowedFaultReason), null, GetSecureConversationCloseNotSupportedFault()));
            }
        }

        internal async ValueTask<(Message, SecurityProtocolCorrelationState)> VerifyIncomingMessageAsync(Message message, TimeSpan timeout, params SecurityProtocolCorrelationState[] correlationState)
        {
            if (message == null)
            {
                return (null, null);
            }
            Fx.Assert(SecurityProtocol != null, "SecurityProtocol can't be null");
            ThrowIfSecureConversationCloseMessage(message);
            return await SecurityProtocol.VerifyIncomingMessageAsync(message, correlationState);
        }

        internal ValueTask<Message> VerifyIncomingMessageAsync(Message message, TimeSpan timeout)
        {
            if (message == null)
            {
                return new ValueTask<Message>((Message)null);
            }
            ThrowIfSecureConversationCloseMessage(message);
            return SecurityProtocol.VerifyIncomingMessageAsync(message);
        }

        public abstract Task DispatchAsync(RequestContext context);
        public abstract Task DispatchAsync(Message message);
    }

    internal class SecurityReplyChannelDispatcher : ServerSecurityChannelDispatcher<IReplyChannel>, IReplyChannel
    {
        private readonly bool _sendUnsecuredFaults;
        internal static readonly SecurityStandardsManager s_defaultStandardsManager = SecurityStandardsManager.DefaultInstance;

#pragma warning disable CS0067
        public event EventHandler Closed;
        public event EventHandler Closing;
        public event EventHandler Faulted;
        public event EventHandler Opened;
        public event EventHandler Opening;
#pragma warning restore CS0067

        public SecurityReplyChannelDispatcher(SecurityServiceDispatcher securityServiceDispatcher, IReplyChannel innerChannel, SecurityProtocol securityProtocol, SecurityListenerSettingsLifetimeManager settingsLifetimeManager)
                : base(securityServiceDispatcher, innerChannel, securityProtocol, settingsLifetimeManager)
        {
            _sendUnsecuredFaults = securityServiceDispatcher.SendUnsecuredFaults;
            SecurityServiceDispatcher = securityServiceDispatcher;
        }

        public EndpointAddress LocalAddress => throw new NotImplementedException();

        public IServiceChannelDispatcher ChannelDispatcher { get; set; }

        public SecurityServiceDispatcher SecurityServiceDispatcher { get; }

        public CommunicationState State => OuterChannel.State;


        internal async ValueTask<RequestContext> ProcessReceivedRequestAsync(RequestContext requestContext)
        {
            if (requestContext == null)
            {
                return null;
            }
            TimeSpan timeout = ServiceDefaults.ReceiveTimeout;
            Message message = requestContext.RequestMessage;
            TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CommunicationException(SR.Format(SR.ReceivedMessageInRequestContextNull)));
            }
            try
            {
                (Message message, SecurityProtocolCorrelationState correlationState) verifiedIncomingMessage = await VerifyIncomingMessageAsync(message, timeoutHelper.RemainingTime(), null);
                message = verifiedIncomingMessage.message;
                SecurityProtocolCorrelationState correlationState = verifiedIncomingMessage.correlationState;

                if (message.Headers.RelatesTo == null && message.Headers.MessageId != null)
                {
                    message.Headers.RelatesTo = message.Headers.MessageId;
                }
                return new SecurityRequestContext(message, requestContext, SecurityProtocol, correlationState, ServiceDefaults.SendTimeout, ServiceDefaults.CloseTimeout);
            }
            catch (Exception securityException)
            {
                await SendFaultIfRequiredAsync(securityException, requestContext, timeoutHelper.RemainingTime());
                throw;
            }
        }

        private async Task SendFaultIfRequiredAsync(Exception e, RequestContext innerContext, TimeSpan timeout)
        {
            if (!_sendUnsecuredFaults)
            {
                return;
            }
            MessageFault fault = SecurityUtils.CreateSecurityMessageFault(e, SecurityProtocol.SecurityProtocolFactory.StandardsManager);
            if (fault == null)
            {
                return;
            }
            Message requestMessage = innerContext.RequestMessage;
            Message faultMessage = Message.CreateMessage(requestMessage.Version, fault, requestMessage.Version.Addressing.DefaultFaultAction);
            try
            {
                TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
                await innerContext.ReplyAsync(faultMessage);
                await innerContext.CloseAsync(timeoutHelper.GetCancellationToken());
            }
            catch (Exception ex)
            {
                if (Fx.IsFatal(ex))
                {
                    throw;
                }
            }
            finally
            {
                faultMessage.Close();
                innerContext.Abort();
            }
        }

        public override async Task DispatchAsync(RequestContext context)
        {
            SecurityRequestContext securedMessage = (SecurityRequestContext)(await ProcessReceivedRequestAsync(context));
            if (SecurityServiceDispatcher.SessionMode) // for SCT, sessiontoken is created so we channel the call to SecurityAuthentication and evevntually SecurityServerSession.
            {
                IServiceChannelDispatcher serviceChannelDispatcher =
                   await SecurityServiceDispatcher.GetAuthChannelDispatcher(this);
                await serviceChannelDispatcher.DispatchAsync(securedMessage);
            }
            else
            {
                    IServiceChannelDispatcher serviceChannelDispatcher =
                    await SecurityServiceDispatcher.GetInnerServiceChannelDispatcher(this);
                    await serviceChannelDispatcher.DispatchAsync(securedMessage);
            }
        }

        public override Task DispatchAsync(Message message)
        {
            return Task.FromException(new NotImplementedException());
        }

        public void Abort()
        {
            throw new NotImplementedException();
        }

        public Task CloseAsync()
        {
            throw new NotImplementedException();
        }

        public Task CloseAsync(CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public Task OpenAsync()
        {
            return Task.CompletedTask;
        }

        public Task OpenAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }
    }

    internal abstract class SecurityDuplexChannel<UChannel> : ServerSecurityChannelDispatcher<UChannel> where UChannel : class, IDuplexChannel
    {
        private readonly IServiceProvider _serviceProvider;
        public SecurityDuplexChannel(SecurityServiceDispatcher serviceDispatcher, UChannel innerChannel, SecurityProtocol securityProtocol, SecurityListenerSettingsLifetimeManager settingsLifetimeManager)
          : base(serviceDispatcher, innerChannel, securityProtocol, settingsLifetimeManager)
        {
            InnerDuplexChannel = innerChannel;
            SecurityProtocol = securityProtocol;
            _serviceProvider = InnerDuplexChannel.GetProperty<IServiceScopeFactory>().CreateScope().ServiceProvider;
        }

        public EndpointAddress RemoteAddress
        {
            get { return InnerDuplexChannel.RemoteAddress; }
        }

        public Uri Via
        {
            get { return InnerDuplexChannel.Via; }
        }

        protected IDuplexChannel InnerDuplexChannel { get; }

        public Task SendAsync(Message message, CancellationToken token)
        {
            SecurityProtocol.SecureOutgoingMessage(ref message);
            return InnerDuplexChannel.SendAsync(message, token);
        }
    }

    internal sealed class SecurityDuplexSessionChannelDispatcher : SecurityDuplexChannel<IDuplexSessionChannel>, IDuplexSessionChannel
    {
        private bool _sendUnsecuredFaults;
        private IServiceChannelDispatcher _serviceChannelDispatcher;
        public SecurityDuplexSessionChannelDispatcher(SecurityServiceDispatcher serviceDispatcher, IDuplexSessionChannel innerChannel, SecurityProtocol securityProtocol, SecurityListenerSettingsLifetimeManager settingsLifetimeManager)
            : base(serviceDispatcher, innerChannel, securityProtocol, settingsLifetimeManager)
        {
            _sendUnsecuredFaults = serviceDispatcher.SendUnsecuredFaults;
            SecurityServiceDispatcher = serviceDispatcher;
        }

        public IDuplexSession Session
        {
            get { return ((IDuplexSessionChannel)InnerDuplexChannel).Session; }
        }

        public EndpointAddress LocalAddress => throw new NotImplementedException();

        public IServiceChannelDispatcher ChannelDispatcher { get; set; }

        public SecurityServiceDispatcher SecurityServiceDispatcher { get; }

        public CommunicationState State => InnerDuplexChannel.State;

#pragma warning disable CS0067 // The event is never used
        public event EventHandler Closing;
        public event EventHandler Faulted;
        public event EventHandler Opened;
        public event EventHandler Opening;
        public event EventHandler Closed;
#pragma warning restore CS0067 // The event is never used

        public void Abort()
        {
            return;
        }

        public async Task CloseAsync()
        {
            await InnerDuplexChannel.CloseAsync();
        }

        public Task CloseAsync(CancellationToken token)
        {
            return CloseAsync();
        }

        public Task OpenAsync()
        {
            return Task.CompletedTask;
        }

        public Task OpenAsync(CancellationToken token)
        {
            return OpenAsync();
        }

        public override Task DispatchAsync(RequestContext context)
        {
            return DispatchAsync(context.RequestMessage);
        }

        public override async Task DispatchAsync(Message message)
        {
            Fx.Assert(State == CommunicationState.Opened, "Expected dispatcher state to be Opened, instead it's " + State.ToString());
            message = await ProcessInnerItemAsync(message, ServiceDefaults.SendTimeout);
            if (_serviceChannelDispatcher == null)
            {
                _serviceChannelDispatcher = await SecurityServiceDispatcher.
                 SecurityAuthServiceDispatcher.CreateServiceChannelDispatcherAsync(this);
            }
            await _serviceChannelDispatcher.DispatchAsync(message);
        }

        public Task SendAsync(Message message)
        {
            return SendAsync(message, TimeoutHelper.GetCancellationToken(ServiceDefaults.SendTimeout));
        }

        private async ValueTask<Message> ProcessInnerItemAsync(Message innerItem, TimeSpan timeout)
        {
            if (innerItem == null)
            {
                return null;
            }
            TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
            Exception securityException = null;
            Message unverifiedMessage = innerItem;
            try
            {
                innerItem = await VerifyIncomingMessageAsync(innerItem, timeout);
            }
            catch (MessageSecurityException e)
            {
                securityException = e;
            }
            if (securityException != null)
            {
                await SendFaultIfRequiredAsync(securityException, unverifiedMessage, timeoutHelper.RemainingTime());
                return null;
            }
            return innerItem;
        }

        private async Task SendFaultIfRequiredAsync(Exception e, Message unverifiedMessage, TimeSpan timeout)
        {
            if (!_sendUnsecuredFaults)
            {
                return;
            }
            MessageFault fault = SecurityUtils.CreateSecurityMessageFault(e, SecurityProtocol.SecurityProtocolFactory.StandardsManager);
            if (fault == null)
            {
                return;
            }
            try
            {
                using (Message faultMessage = Message.CreateMessage(unverifiedMessage.Version, fault, unverifiedMessage.Version.Addressing.DefaultFaultAction))
                {
                    if (unverifiedMessage.Headers.MessageId != null)
                        faultMessage.InitializeReply(unverifiedMessage);
                    TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
                    await ((IDuplexChannel)InnerDuplexChannel).SendAsync(faultMessage);
                }
            }
            catch (Exception ex)
            {
                if (Fx.IsFatal(ex))
                    throw;
            }
        }

        public Task<(Message message, bool success)> TryReceiveAsync(CancellationToken token) => throw new NotImplementedException();
        public Task<Message> ReceiveAsync(CancellationToken token) => throw new NotImplementedException();
    }

    internal sealed class SecurityRequestContext : RequestContextBase
    {
        private readonly RequestContext _innerContext;
        private readonly SecurityProtocol _securityProtocol;
        private readonly SecurityProtocolCorrelationState _correlationState;

        public SecurityRequestContext(Message requestMessage, RequestContext innerContext,
            SecurityProtocol securityProtocol, SecurityProtocolCorrelationState correlationState,
            TimeSpan defaultSendTimeout, TimeSpan defaultCloseTimeout)
            : base(requestMessage, defaultCloseTimeout, defaultSendTimeout)
        {
            _innerContext = innerContext;
            _securityProtocol = securityProtocol;
            _correlationState = correlationState;
        }

        protected override void OnAbort()
        {
            _innerContext.Abort();
        }

        protected override Task OnCloseAsync(CancellationToken token)
        {
            return _innerContext.CloseAsync(token);
        }

        protected override Task OnReplyAsync(Message message, CancellationToken token)
        {
            if (message != null)
            {
                (_, message) = _securityProtocol.SecureOutgoingMessage(message, _correlationState, token);
                return _innerContext.ReplyAsync(message, token);
            }
            else
            {
                return Task.CompletedTask;
            }
        }
    }
}
