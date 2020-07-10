using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Description;
using CoreWCF.Runtime;
using CoreWCF.Security;
using CoreWCF.Security.Tokens;
using System;
using System.Collections.Generic;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;

namespace CoreWCF.Dispatcher
{
    public class SecurityServiceDispatcher : IServiceDispatcher
    {
        private IRequestReplyCorrelator _requestReplyCorrelator;
        private BindingContext bindingContext;
        TransportSecurityBindingElement transportSecurityBinding;
        private SecurityProtocolFactory securityProtocolFactory;
        private SecuritySessionServerSettings sessionServerSettings;
        // Do we need it ?
        SecurityListenerSettingsLifetimeManager settingsLifetimeManager;
        private bool sessionMode;
        private bool sendUnsecuredFaults;
        private ChannelDispatcher securityAuthChannelDispatcher;
        IChannel outerChannel;

        public SecurityServiceDispatcher(TransportSecurityBindingElement transportSecurityBindingElement, BindingContext context, IServiceDispatcher serviceDispatcher)
        {
            this.InnerServiceDispatcher = serviceDispatcher;
            this.bindingContext = context;
            this.transportSecurityBinding = transportSecurityBindingElement;
            _requestReplyCorrelator = new RequestReplyCorrelator();
            // this.securityProtocolFactory =  securityProtocolFactory; // we set it later from TransportSecurityBindingElement
            //  this.settingsLifetimeManager = new SecurityListenerSettingsLifetimeManager(this.securityProtocolFactory, this.sessionServerSettings, this.sessionMode, this.InnerChannelListener);
        }

        public Uri BaseAddress => InnerServiceDispatcher.BaseAddress;

        public Binding Binding => bindingContext.Binding;

        public IServiceDispatcher InnerServiceDispatcher { get; }

        public ChannelDispatcher SecurityAuthChannelDispatcher
        {
            get
            {
                return this.securityAuthChannelDispatcher;
            }
            set
            {
                this.securityAuthChannelDispatcher = value;
            }
        }

        // public EndpointDispatcherTable Endpoints => ChannelDispatcher.EndpointDispatcherTable;

        public ICollection<Type> SupportedChannelTypes => InnerServiceDispatcher.SupportedChannelTypes;

        public object ThisLock { get; } = new object();

        public SecurityProtocolFactory SecurityProtocolFactory
        {
            get
            {

                //  ThrowIfDisposed();
                return this.securityProtocolFactory;
            }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("value");
                }
                //   ThrowIfDisposedOrImmutable();
                this.securityProtocolFactory = value;
            }
        }

        public bool SessionMode
        {
            get
            {
                return this.sessionMode;
            }
            set
            {
                this.sessionMode = value;
            }
        }

        public SecuritySessionServerSettings SessionServerSettings
        {
            get
            {
                if (this.sessionServerSettings == null)
                {
                    lock (ThisLock)
                    {
                        if (this.sessionServerSettings == null)
                        {
                            SecuritySessionServerSettings tmp = new SecuritySessionServerSettings();
                            System.Threading.Thread.MemoryBarrier();
                            tmp.SecurityServiceDispatcher = this;
                            this.sessionServerSettings = tmp;
                        }
                    }
                }
                return this.sessionServerSettings;
            }
        }

        bool SupportsDuplex
        {
            get
            {
                ThrowIfProtocolFactoryNotSet();
                return this.securityProtocolFactory.SupportsDuplex;
            }
        }

        bool SupportsRequestReply
        {
            get
            {
                ThrowIfProtocolFactoryNotSet();
                return this.securityProtocolFactory.SupportsRequestReply;
            }
        }

        public bool SendUnsecuredFaults
        {
            get
            {
                return this.sendUnsecuredFaults;
            }
            set
            {
                this.sendUnsecuredFaults = value;
            }
        }

        public IChannel OuterChannel
        {
            get { return this.outerChannel; }
            set { this.outerChannel = value; }
        }

        IList<Type> IServiceDispatcher.SupportedChannelTypes => throw new NotImplementedException();

        public ServiceHostBase Host =>  InnerServiceDispatcher.Host;

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



        void ThrowIfProtocolFactoryNotSet()
        {
            if (this.securityProtocolFactory == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SecurityProtocolFactoryShouldBeSetBeforeThisOperation)));
            }
        }
        public async Task<IServiceChannelDispatcher> CreateServiceChannelDispatcherAsync(IChannel outerChannel)
        {
            this.outerChannel = outerChannel;
            IChannel securityChannel = GetInnerChannel(outerChannel);
            var sessionIdleManager = securityChannel.GetProperty<ServiceChannel.SessionIdleManager>();
            IChannelBinder binder = null;
            if (securityChannel is IReplyChannel)
            {
                var rcbinder = securityChannel.GetProperty<SecurityReplyChannelBinder>();
                rcbinder.Init(securityChannel as IReplyChannel, BaseAddress);
                binder = rcbinder;
            }
            else
            {
                throw new NotImplementedException();
            }
            //Open the session server 
            await this.SessionServerSettings.OnOpenAsync(ServiceDefaults.OpenTimeout);

            // TODO: MUST add the proper messsage
            if (this.SecurityAuthChannelDispatcher == null)
                throw new Exception("SecurityAuthChannelDispatcher can't be null");

            ServiceDispatcher securitySessionAuthDispatcher = new ServiceDispatcher(this.SecurityAuthChannelDispatcher);
            var channelHandler = new ChannelHandler(Binding.MessageVersion, binder, securityChannel.GetProperty<ServiceThrottle>(),
            securitySessionAuthDispatcher, /*wasChannelThrottled*/ false, sessionIdleManager);
            var channelDispatcher = channelHandler.GetDispatcher();
            outerChannel.ChannelDispatcher = securityChannel.ChannelDispatcher = channelDispatcher;
            await channelHandler.OpenAsync();
            return channelDispatcher;
        }

        private IChannel GetInnerChannel(IChannel outerChannel)
        {
            IChannel securityChannel = null;
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
                securityChannel = new SecurityReplyChannel(this, (IReplyChannel)outerChannel);
            }
            /* else if (listener.SupportsRequestReply && typeof(TChannel) == typeof(IReplySessionChannel))
             {
                 securityChannel = new SecurityReplySessionChannel(listener, (IReplySessionChannel)innerChannel, securityProtocol, listener.settingsLifetimeManager);
             }
             else
             {
                 throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.GetString(SR.UnsupportedChannelInterfaceType, typeof(TChannel))));
             }*/

             return (IChannel)securityChannel;
        }

    }

   abstract class ServerSecurityChannel<UChannel>  where UChannel : class, IChannel
    {
        static MessageFault secureConversationCloseNotSupportedFault;
        string secureConversationCloseAction;
        SecurityListenerSettingsLifetimeManager settingsLifetimeManager;
        bool hasSecurityStateReference;

        internal SecurityProtocol SecurityProtocol { get;  set; }
        public IReplyChannel OuterChannel { get; private set; }

        protected ServerSecurityChannel( IReplyChannel outerChannel)
            //: base(channelManager, innerChannel, securityProtocol) // not sure use of channel manager at this time
        {
            this.OuterChannel = outerChannel;
        }


        static MessageFault GetSecureConversationCloseNotSupportedFault()
        {
            if (secureConversationCloseNotSupportedFault == null)
            {
                FaultCode faultCode = FaultCode.CreateSenderFaultCode(DotNetSecurityStrings.SecureConversationCancelNotAllowedFault, DotNetSecurityStrings.Namespace);
                FaultReason faultReason = new FaultReason(SR.Format(SR.SecureConversationCancelNotAllowedFaultReason), System.Globalization.CultureInfo.InvariantCulture);
                secureConversationCloseNotSupportedFault = MessageFault.CreateFault(faultCode, faultReason);
            }
            return secureConversationCloseNotSupportedFault;
        }

        void ThrowIfSecureConversationCloseMessage(Message message)
        {
            if (message.Headers.Action == this.secureConversationCloseAction)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.SecureConversationCancelNotAllowedFaultReason), null, GetSecureConversationCloseNotSupportedFault()));
            }
        }
       
        [SecuritySafeCritical]
        internal SecurityProtocolCorrelationState VerifyIncomingMessage(ref Message message, TimeSpan timeout, params SecurityProtocolCorrelationState[] correlationState)
        {
            if (message == null)
            {
                return null;
            }
            if (this.SecurityProtocol == null)
                throw new Exception("SecurityProtocol is null");
            ThrowIfSecureConversationCloseMessage(message);
           // using (this.ApplyHostingIntegrationContext(message))
           // {
                return this.SecurityProtocol.VerifyIncomingMessage(ref message, timeout, correlationState);
           // }
        }


        [SecuritySafeCritical]
        internal void VerifyIncomingMessage(ref Message message, TimeSpan timeout)
        {
            if (message == null)
            {
                return;
            }
            ThrowIfSecureConversationCloseMessage(message);
           // using (this.ApplyHostingIntegrationContext(message))
           // {
                this.SecurityProtocol.VerifyIncomingMessage(ref message, timeout);
           // }
        }
    }
    class SecurityReplyChannel : ServerSecurityChannel<IReplyChannel>, IReplyChannel
    {
        bool sendUnsecuredFaults;
        internal static readonly SecurityStandardsManager defaultStandardsManager = SecurityStandardsManager.DefaultInstance;

        public SecurityReplyChannel(SecurityServiceDispatcher securityDispatcher, IReplyChannel outerChannel)
            : base(outerChannel)
        {
            this.SecurityServiceDispatcher = securityDispatcher;
        }

        public EndpointAddress LocalAddress => throw new NotImplementedException();

        public IServiceChannelDispatcher ChannelDispatcher { get; set; }

        public SecurityServiceDispatcher SecurityServiceDispatcher { get; }
        public CommunicationState State => this.OuterChannel.State;
       
        public virtual XmlDictionaryString RenewAction
        {
            get
            {
                return defaultStandardsManager.SecureConversationDriver.RenewAction;
            }
        }

        public virtual XmlDictionaryString IssueAction
        {
            get
            {
                return defaultStandardsManager.SecureConversationDriver.IssueAction;
            }
        }

        public event EventHandler Closed;
        public event EventHandler Closing;
        public event EventHandler Faulted;
        public event EventHandler Opened;
        public event EventHandler Opening;

        public void Abort()
        {
            
        }

        public Task CloseAsync()
        {
           return Task.CompletedTask;
        }

        public Task CloseAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }

        public T GetProperty<T>() where T : class
        {
            return this.OuterChannel.GetProperty<T>();
        }

        public Task OpenAsync()
        {
            BuildResponderSecurityProtocol<SecurityReplyChannel>();
            return Task.CompletedTask;
        }

       // internal IChannelListener<TChannel> BuildResponderChannelListener<TChannel>(BindingContext context)
        //   where TChannel : class, IChannel
        //Keeping above signature for future reference. Flattening out the Listner and sticking to push model
        internal void BuildResponderSecurityProtocol<TChannel>() 
        {
            SecuritySessionSecurityTokenAuthenticator authenticator = 
                (SecuritySessionSecurityTokenAuthenticator) this.SecurityServiceDispatcher
                .SessionServerSettings.SessionTokenAuthenticator;
             BindingContext issuerBindingContext = authenticator.IssuerBindingContext; 
            SecurityBindingElement bootStrapSecurity = authenticator.BootstrapSecurityBindingElement;

            SecurityCredentialsManager securityCredentials = issuerBindingContext.BindingParameters.Find<SecurityCredentialsManager>();
            if (securityCredentials == null)
            {
                securityCredentials = ServiceCredentials.CreateDefaultCredentials();
            }
            bootStrapSecurity.ReaderQuotas 
                = issuerBindingContext.GetInnerProperty<XmlDictionaryReaderQuotas>();
            if (bootStrapSecurity.ReaderQuotas == null)
            {
                bootStrapSecurity.ReaderQuotas = XmlDictionaryReaderQuotas.Max;
                //throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.EncodingBindingElementDoesNotHandleReaderQuotas)));
            }

            TransportBindingElement transportBindingElement = issuerBindingContext.RemainingBindingElements.Find<TransportBindingElement>();
            if (transportBindingElement != null)
                bootStrapSecurity.MaxReceivedMessageSize 
                    = transportBindingElement.MaxReceivedMessageSize;

            SecurityProtocolFactory bootstrapSecurityProtocolFactory = bootStrapSecurity.
                CreateSecurityProtocolFactory<TChannel>(issuerBindingContext.Clone(), 
                securityCredentials, true, issuerBindingContext.Clone());
            //TODO : Message security
            //if (bootstrapSecurityProtocolFactory is MessageSecurityProtocolFactory)
            //{
            //    MessageSecurityProtocolFactory soapBindingFactory = (MessageSecurityProtocolFactory)bootstrapSecurityProtocolFactory;
            //    soapBindingFactory.ApplyConfidentiality = soapBindingFactory.ApplyIntegrity
            //    = soapBindingFactory.RequireConfidentiality = soapBindingFactory.RequireIntegrity = true;

            //    soapBindingFactory.ProtectionRequirements.IncomingSignatureParts.ChannelParts.IsBodyIncluded = true;
            //    soapBindingFactory.ProtectionRequirements.OutgoingSignatureParts.ChannelParts.IsBodyIncluded = true;

            //    MessagePartSpecification bodyPart = new MessagePartSpecification(true);
            //    soapBindingFactory.ProtectionRequirements.OutgoingSignatureParts.AddParts(bodyPart, this.IssueResponseAction);
            //    soapBindingFactory.ProtectionRequirements.OutgoingEncryptionParts.AddParts(bodyPart, this.IssueResponseAction);
            //    soapBindingFactory.ProtectionRequirements.OutgoingSignatureParts.AddParts(bodyPart, this.RenewResponseAction);
            //    soapBindingFactory.ProtectionRequirements.OutgoingEncryptionParts.AddParts(bodyPart, this.RenewResponseAction);

            //    soapBindingFactory.ProtectionRequirements.IncomingSignatureParts.AddParts(bodyPart, this.IssueAction);
            //    soapBindingFactory.ProtectionRequirements.IncomingEncryptionParts.AddParts(bodyPart, this.IssueAction);
            //    soapBindingFactory.ProtectionRequirements.IncomingSignatureParts.AddParts(bodyPart, this.RenewAction);
            //    soapBindingFactory.ProtectionRequirements.IncomingEncryptionParts.AddParts(bodyPart, this.RenewAction);
            //}

            //TODO renew part
            //SupportingTokenParameters renewSupportingTokenParameters = new SupportingTokenParameters();
            //SecurityContextSecurityTokenParameters sctParameters = new SecurityContextSecurityTokenParameters();
            //if (bootStrapSecurity.EndpointSupportingTokenParameters.Endorsing.Count > 0)
            //{
            //    SecureConversationSecurityTokenParameters scParametersTemp = bootStrapSecurity.EndpointSupportingTokenParameters.Endorsing[0] as SecureConversationSecurityTokenParameters;
            //    sctParameters.RequireDerivedKeys = scParametersTemp.RequireDerivedKeys;
            //}
            //renewSupportingTokenParameters.Endorsing.Add(sctParameters);
           // bootstrapSecurityProtocolFactory.SecurityBindingElement.OperationSupportingTokenParameters.Add(this.RenewAction.Value, renewSupportingTokenParameters);
            // bootstrapSecurityProtocolFactory.SecurityTokenManager = new SessionRenewSecurityTokenManager(bootstrapSecurityProtocolFactory.SecurityTokenManager, this.sessionTokenAuthenticator, (SecurityTokenResolver)this.IssuedTokenCache);
            //SecurityChannelListener<TChannel> securityChannelListener = new SecurityChannelListener<TChannel>(
            //    this.bootstrapSecurityBindingElement, this.IssuerBindingContext);
            //securityChannelListener.SecurityProtocolFactory = bootstrapSecurityProtocolFactory;
            //securityChannelListener.SendUnsecuredFaults = !SecurityUtils.IsCompositeDuplexBinding(context);

            //ChannelBuilder channelBuilder = new ChannelBuilder(context, true);
            //securityChannelListener.InitializeListener(channelBuilder);
            //this.shouldMatchRstWithEndpointFilter = SecurityUtils.ShouldMatchRstWithEndpointFilter(this.bootstrapSecurityBindingElement);
            //return securityChannelListener;
            bootstrapSecurityProtocolFactory.OpenAsync(TimeSpan.Zero);
            base.SecurityProtocol = bootstrapSecurityProtocolFactory.CreateSecurityProtocol(
                    null,
                    null,null,
                    true, TimeSpan.Zero);

        }

        public Task OpenAsync(CancellationToken token)
        {
            return Task.CompletedTask;
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
                SecurityProtocolCorrelationState correlationState = this.VerifyIncomingMessage(ref message, timeoutHelper.RemainingTime(), null);
               //TODO check with MS
                if(message.Headers.RelatesTo == null && message.Headers.MessageId !=null)
                {
                    message.Headers.RelatesTo = message.Headers.MessageId;
                }
                return new SecurityRequestContext(message, requestContext, this.SecurityProtocol, correlationState, ServiceDefaults.SendTimeout, ServiceDefaults.CloseTimeout);
            }
            catch (Exception e)
            {
                securityException = e;
                SendFaultIfRequired(securityException, requestContext, timeoutHelper.RemainingTime());
                throw securityException;
            }

        }

        void SendFaultIfRequired(Exception e, RequestContext innerContext, TimeSpan timeout)
        {
            if (!sendUnsecuredFaults)
            {
                return;
            }
            MessageFault fault = SecurityUtils.CreateSecurityMessageFault(e, this.SecurityProtocol.SecurityProtocolFactory.StandardsManager);
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
#pragma warning suppress 56500 // covered by FxCOP
            catch (Exception ex)
            {
                if (Fx.IsFatal(ex))
                {
                    throw;
                }

                // eat up exceptions
            }
            finally
            {
                faultMessage.Close();
                innerContext.Abort();
            }
        }
    }

    sealed class SecurityRequestContext : RequestContextBase
    {
        readonly RequestContext innerContext;
        readonly SecurityProtocol securityProtocol;
        readonly SecurityProtocolCorrelationState correlationState;

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
            this.innerContext.Abort();
        }

        protected override Task OnCloseAsync(CancellationToken token)
        {
            return this.innerContext.CloseAsync(token);
        }

        protected override Task OnReplyAsync(Message message, CancellationToken token)
        {
            if (message != null)
            {
             Task<Message> appLiedMessage =  this.securityProtocol.SecureOutgoingMessageAsync(message, token);
                return this.innerContext.ReplyAsync(appLiedMessage.GetAwaiter().GetResult(),token);
            }else
            {
              return  Task.CompletedTask;
            }
        }

        
    }
}
