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

namespace CoreWCF.Dispatcher
{
    /// <summary>
    /// This is equivalent of SecurityChannelListener present in WCF codebase
    /// </summary>
    public class SecurityServiceDispatcher : IServiceDispatcher, IDisposable
    {
        private readonly IRequestReplyCorrelator _requestReplyCorrelator;
        private readonly BindingContext bindingContext;
        private ChannelBuilder channelBuilder;
        private readonly EndpointIdentity identity;
        private readonly SecurityBindingElement transportSecurityBinding;
        private SecurityProtocolFactory securityProtocolFactory;
        private SecuritySessionServerSettings sessionServerSettings;
        private SecurityListenerSettingsLifetimeManager settingsLifetimeManager;
        private bool sendUnsecuredFaults;
        //ServiceChannelDispatcher to call SCT (just keep one instance)
        private volatile IServiceChannelDispatcher securityAuthServiceChannelDispatcher;
        private Task<IServiceChannelDispatcher> channelTask;
        //ServiceChannelDispatcher to call real service (just keep one instance)
        private readonly IServiceChannelDispatcher innerServiceChanelDispatcher;
        private bool _disposed = false;

        public SecurityServiceDispatcher(SecurityBindingElement transportSecurityBindingElement, BindingContext context, IServiceDispatcher serviceDispatcher)
        {
            InnerServiceDispatcher = serviceDispatcher;
            bindingContext = context;
            transportSecurityBinding = transportSecurityBindingElement;
            _requestReplyCorrelator = new RequestReplyCorrelator();
            // this.securityProtocolFactory =  securityProtocolFactory; // we set it later from TransportSecurityBindingElement
            //  this.settingsLifetimeManager = new SecurityListenerSettingsLifetimeManager(this.securityProtocolFactory, this.sessionServerSettings, this.sessionMode, this.InnerChannelListener);
        }

        internal ChannelBuilder ChannelBuilder
        {
            get
            {
                ThrowIfDisposed();
                return channelBuilder;
            }
        }

        public Uri BaseAddress => InnerServiceDispatcher.BaseAddress;

        public Binding Binding => bindingContext.Binding;

        public IServiceDispatcher InnerServiceDispatcher { get; set; }

        public IServiceDispatcher SecurityAuthServiceDispatcher { get; set; }

        public ICollection<Type> SupportedChannelTypes => InnerServiceDispatcher.SupportedChannelTypes;

        public object ThisLock { get; } = new object();

        public SecurityProtocolFactory SecurityProtocolFactory
        {
            get
            {
                ThrowIfDisposed();
                return securityProtocolFactory;
            }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("value");
                }
                ThrowIfDisposed();
                securityProtocolFactory = value;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ObjectDisposedException(GetType().FullName));
            }
        }

        public bool SessionMode { get; set; }

        internal SecuritySessionServerSettings SessionServerSettings
        {
            get
            {
                if (sessionServerSettings == null)
                {
                    lock (ThisLock)
                    {
                        if (sessionServerSettings == null)
                        {
                            SecuritySessionServerSettings tmp = new SecuritySessionServerSettings();
                            System.Threading.Thread.MemoryBarrier();
                            tmp.SecurityServiceDispatcher = this;
                            sessionServerSettings = tmp;
                        }
                    }
                }
                return sessionServerSettings;
            }
        }

        private bool SupportsDuplex
        {
            get
            {
                ThrowIfProtocolFactoryNotSet();
                return securityProtocolFactory.SupportsDuplex;
            }
        }

        private bool SupportsRequestReply
        {
            get
            {
                ThrowIfProtocolFactoryNotSet();
                return securityProtocolFactory.SupportsRequestReply;
            }
        }

        public bool SendUnsecuredFaults
        {
            get
            {
                return sendUnsecuredFaults;
            }
            set
            {
                sendUnsecuredFaults = value;
            }
        }

        public IChannel OuterChannel { get; set; }
        public Type AcceptorChannelType { get; set; }

        IList<Type> IServiceDispatcher.SupportedChannelTypes => throw new NotImplementedException();

        public ServiceHostBase Host => InnerServiceDispatcher.Host;

        internal void InitializeSecurityDispatcher(ChannelBuilder channelBuilder, Type type)
        {
            this.channelBuilder = channelBuilder;

            if (SessionMode)
            {
                sessionServerSettings.ChannelBuilder = ChannelBuilder;
                //this.InnerChannelListener = this.sessionServerSettings.CreateInnerChannelListener();
                // this.Acceptor = this.sessionServerSettings.CreateAcceptor<TChannel>();
                AcceptorChannelType = type;
                sessionServerSettings.AcceptorChannelType = type;
            }
            else
            {
                throw new PlatformNotSupportedException();
                //TODO later
                // this.InnerChannelListener = this.ChannelBuilder.BuildChannelListener<TChannel>();
                // this.Acceptor = (IChannelAcceptor<TChannel>)new SecurityChannelAcceptor(this,
                //     (IChannelListener<TChannel>)InnerChannelListener, this.securityProtocolFactory.CreateListenerSecurityState());
            }
            //Called below method in the initialization path, in WCF it's called in Open of ServiceHost.
            InitializeServiceDispatcherSecurityState();
        }

        private void InitializeServiceDispatcherSecurityState()
        {
            if (SessionMode)
            {
                // this.SessionServerSettings.SessionProtocolFactory.ListenUri = this.Uri;
                SessionServerSettings.SecurityServiceDispatcher = this;
            }
            else
            {
                ThrowIfProtocolFactoryNotSet();
                //  this.securityProtocolFactory.ListenUri = this.Uri;
            }
            settingsLifetimeManager = new SecurityListenerSettingsLifetimeManager(securityProtocolFactory, sessionServerSettings, SessionMode);//, this.InnerChannelListener);
            if (sessionServerSettings != null)
            {
                sessionServerSettings.SettingsLifetimeManager = settingsLifetimeManager;
            }
            //this.hasSecurityStateReference = true;
            sessionServerSettings.SettingsLifetimeManager.OpenAsync(ServiceDefaults.OpenTimeout);
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
            if (securityProtocolFactory == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SecurityProtocolFactoryShouldBeSetBeforeThisOperation)));
            }
        }

        public async Task<IServiceChannelDispatcher> CreateServiceChannelDispatcherAsync(IChannel outerChannel)
        {
            //TODO should have better logic
            //Initialization path start
            if (outerChannel.ChannelDispatcher == null)
            {
                TypedChannelDemuxer typedChannelDemuxer = ChannelBuilder.ChannelDemuxer.GetTypedServiceDispatcher<IReplyChannel>();
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

        internal async Task<IServiceChannelDispatcher> GetAuthChannelDispatcher(IReplyChannel outerChannel)
        {
            if (securityAuthServiceChannelDispatcher == null)
            {
                lock (ThisLock)
                {
                    if (channelTask == null)
                    {
                        channelTask = SecurityAuthServiceDispatcher.CreateServiceChannelDispatcherAsync(outerChannel);
                    }
                }
                securityAuthServiceChannelDispatcher = await channelTask;
                Thread.MemoryBarrier();
            }
            return securityAuthServiceChannelDispatcher;
        }


        /// <summary>
        /// Return same instance of real service channel dispatcher.
        /// This method is called by SecurityReplySessionServiceChannelDispatcher(SecuritySessionServerSettings) to dispatch real service 
        /// </summary>
        /// <param name="outerChannel"></param>
        /// <returns></returns>
        internal Task<IServiceChannelDispatcher> GetInnerServiceChannelDispatcher(IReplyChannel outerChannel)
        {
            return InnerServiceDispatcher.CreateServiceChannelDispatcherAsync(outerChannel);
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
            else if (outerChannel is IDuplexSessionChannel))
            {
                securityChannel = new SecurityDuplexSessionChannel(listener, (IDuplexSessionChannel)innerChannel, securityProtocol, listener.settingsLifetimeManager);
            }
            else*/
            if (outerChannel is IReplyChannel)
            {
                securityChannelDispatcher = new SecurityReplyChannelDispatcher(this, (IReplyChannel)outerChannel, securityProtocol, settingsLifetimeManager);
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
            if (!_disposed)
            {
                _disposed = true;
            }
        }
    }

    internal abstract class ServerSecurityChannelDispatcher<UChannel> : IServiceChannelDispatcher where UChannel : class
    {
        private static MessageFault secureConversationCloseNotSupportedFault;
        private readonly string secureConversationCloseAction;
        private readonly SecurityListenerSettingsLifetimeManager settingsLifetimeManager;
        private readonly bool hasSecurityStateReference;

        protected ServerSecurityChannelDispatcher(SecurityServiceDispatcher securityServiceDispatcher, UChannel innerChannel, SecurityProtocol securityProtocol, SecurityListenerSettingsLifetimeManager settingsLifetimeManager)
        // : base(channelManager, innerChannel, securityProtocol)
        {
            if (settingsLifetimeManager == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("settingsLifetimeManager");
            }
            this.settingsLifetimeManager = settingsLifetimeManager;
            SecurityProtocol = securityProtocol;
            OuterChannel = (IReplyChannel)innerChannel;
        }

        internal SecurityProtocol SecurityProtocol { get; set; }
        public IReplyChannel OuterChannel { get; private set; }

        private static MessageFault GetSecureConversationCloseNotSupportedFault()
        {
            if (secureConversationCloseNotSupportedFault == null)
            {
                FaultCode faultCode = FaultCode.CreateSenderFaultCode(DotNetSecurityStrings.SecureConversationCancelNotAllowedFault, DotNetSecurityStrings.Namespace);
                FaultReason faultReason = new FaultReason(SR.Format(SR.SecureConversationCancelNotAllowedFaultReason), System.Globalization.CultureInfo.InvariantCulture);
                secureConversationCloseNotSupportedFault = MessageFault.CreateFault(faultCode, faultReason);
            }
            return secureConversationCloseNotSupportedFault;
        }

        private void ThrowIfSecureConversationCloseMessage(Message message)
        {
            if (message.Headers.Action == secureConversationCloseAction)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.SecureConversationCancelNotAllowedFaultReason), null, GetSecureConversationCloseNotSupportedFault()));
            }
        }

        internal SecurityProtocolCorrelationState VerifyIncomingMessage(ref Message message, TimeSpan timeout, params SecurityProtocolCorrelationState[] correlationState)
        {
            if (message == null)
            {
                return null;
            }
            Fx.Assert(SecurityProtocol != null, "SecurityProtocol can't be null");
            ThrowIfSecureConversationCloseMessage(message);
            return SecurityProtocol.VerifyIncomingMessage(ref message, timeout, correlationState);
        }

        internal void VerifyIncomingMessage(ref Message message, TimeSpan timeout)
        {
            if (message == null)
            {
                return;
            }
            ThrowIfSecureConversationCloseMessage(message);
            SecurityProtocol.VerifyIncomingMessage(ref message, timeout);
        }

        public abstract Task DispatchAsync(RequestContext context);
        public abstract Task DispatchAsync(Message message);
    }

    internal class SecurityReplyChannelDispatcher : ServerSecurityChannelDispatcher<IReplyChannel>  //, IChannel
    {
        private readonly bool sendUnsecuredFaults;
        internal static readonly SecurityStandardsManager defaultStandardsManager = SecurityStandardsManager.DefaultInstance;

        public SecurityReplyChannelDispatcher(SecurityServiceDispatcher securityServiceDispatcher, IReplyChannel innerChannel, SecurityProtocol securityProtocol, SecurityListenerSettingsLifetimeManager settingsLifetimeManager)
                : base(securityServiceDispatcher, innerChannel, securityProtocol, settingsLifetimeManager)
        {
            sendUnsecuredFaults = securityServiceDispatcher.SendUnsecuredFaults;
            SecurityServiceDispatcher = securityServiceDispatcher;
        }

        public EndpointAddress LocalAddress => throw new NotImplementedException();

        public IServiceChannelDispatcher ChannelDispatcher { get; set; }

        public SecurityServiceDispatcher SecurityServiceDispatcher { get; }

        public CommunicationState State => OuterChannel.State;

        public T GetProperty<T>() where T : class
        {
            return OuterChannel.GetProperty<T>();
        }

        internal RequestContext ProcessReceivedRequest(RequestContext requestContext)
        {
            if (requestContext == null)
            {
                return null;
            }
            Exception securityException = null;
            TimeSpan timeout = ServiceDefaults.ReceiveTimeout;
            Message message = requestContext.RequestMessage;
            TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CommunicationException(SR.Format(SR.ReceivedMessageInRequestContextNull)));
            }
            try
            {
                SecurityProtocolCorrelationState correlationState = VerifyIncomingMessage(ref message, timeoutHelper.RemainingTime(), null);
                if (message.Headers.RelatesTo == null && message.Headers.MessageId != null)
                {
                    message.Headers.RelatesTo = message.Headers.MessageId;
                }
                return new SecurityRequestContext(message, requestContext, SecurityProtocol, correlationState, ServiceDefaults.SendTimeout, ServiceDefaults.CloseTimeout);
            }
            catch (Exception e)
            {
                securityException = e;
                SendFaultIfRequired(securityException, requestContext, timeoutHelper.RemainingTime());
                throw securityException;
            }
        }

        private void SendFaultIfRequired(Exception e, RequestContext innerContext, TimeSpan timeout)
        {
            if (!sendUnsecuredFaults)
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
                innerContext.ReplyAsync(faultMessage);
                innerContext.CloseAsync(timeoutHelper.GetCancellationToken());
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
            SecurityRequestContext securedMessage = (SecurityRequestContext)ProcessReceivedRequest(context);
            IServiceChannelDispatcher serviceChannelDispatcher =
               await SecurityServiceDispatcher.GetAuthChannelDispatcher(OuterChannel);
            await serviceChannelDispatcher.DispatchAsync(securedMessage);
        }

        public override Task DispatchAsync(Message message)
        {
            throw new NotImplementedException();
        }
    }

    internal sealed class SecurityRequestContext : RequestContextBase
    {
        private readonly RequestContext innerContext;
        private readonly SecurityProtocol securityProtocol;
        private readonly SecurityProtocolCorrelationState correlationState;

        public SecurityRequestContext(Message requestMessage, RequestContext innerContext,
            SecurityProtocol securityProtocol, SecurityProtocolCorrelationState correlationState,
            TimeSpan defaultSendTimeout, TimeSpan defaultCloseTimeout)
            : base(requestMessage, defaultCloseTimeout, defaultSendTimeout)
        {
            this.innerContext = innerContext;
            this.securityProtocol = securityProtocol;
            this.correlationState = correlationState;
        }

        protected override void OnAbort()
        {
            innerContext.Abort();
        }

        protected override Task OnCloseAsync(CancellationToken token)
        {
            return innerContext.CloseAsync(token);
        }

        protected override Task OnReplyAsync(Message message, CancellationToken token)
        {
            if (message != null)
            {
                Message appLiedMessage = securityProtocol.SecureOutgoingMessage(message, token);
                return innerContext.ReplyAsync(appLiedMessage, token);
            }
            else
            {
                return Task.CompletedTask;
            }
        }


    }
}
