using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Xml;
using CoreWCF.Configuration;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.Security;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Channels
{
    public abstract class SecurityBindingElement : BindingElement
    {
        internal const string defaultAlgorithmSuiteString = "Default";
        internal static readonly SecurityAlgorithmSuite defaultDefaultAlgorithmSuite = SecurityAlgorithmSuite.Default;
        internal const bool defaultIncludeTimestamp = true;
        internal const bool defaultAllowInsecureTransport = false;
        internal const MessageProtectionOrder defaultMessageProtectionOrder = MessageProtectionOrder.SignBeforeEncryptAndEncryptSignature;
        internal const bool defaultRequireSignatureConfirmation = false;
        internal const bool defaultEnableUnsecuredResponse = false;
        internal const bool defaultProtectTokens = false;

        SecurityAlgorithmSuite defaultAlgorithmSuite;
        SupportingTokenParameters endpointSupportingTokenParameters;
        SupportingTokenParameters optionalEndpointSupportingTokenParameters;
        bool includeTimestamp;
        SecurityKeyEntropyMode keyEntropyMode;
        Dictionary<string, SupportingTokenParameters> operationSupportingTokenParameters;
        Dictionary<string, SupportingTokenParameters> optionalOperationSupportingTokenParameters;
        LocalServiceSecuritySettings localServiceSettings;
        MessageSecurityVersion messageSecurityVersion;
        SecurityHeaderLayout securityHeaderLayout;
        // InternalDuplexBindingElement internalDuplexBindingElement;
        long maxReceivedMessageSize = TransportDefaults.MaxReceivedMessageSize;
        XmlDictionaryReaderQuotas readerQuotas;
        bool doNotEmitTrust = false; // true if user create a basic http standard binding, the custombinding equivalent will not set this flag 
        bool supportsExtendedProtectionPolicy;
        bool allowInsecureTransport;
        bool enableUnsecuredResponse;
        bool protectTokens = defaultProtectTokens;

        internal SecurityBindingElement()
            : base()
        {
            this.messageSecurityVersion = MessageSecurityVersion.Default;
            this.keyEntropyMode = SecurityKeyEntropyMode.CombinedEntropy; // AcceleratedTokenProvider.defaultKeyEntropyMode;
            this.includeTimestamp = defaultIncludeTimestamp;
            this.defaultAlgorithmSuite = defaultDefaultAlgorithmSuite;
            this.localServiceSettings = new LocalServiceSecuritySettings();
            this.endpointSupportingTokenParameters = new SupportingTokenParameters();
            this.optionalEndpointSupportingTokenParameters = new SupportingTokenParameters();
            this.operationSupportingTokenParameters = new Dictionary<string, SupportingTokenParameters>();
            this.optionalOperationSupportingTokenParameters = new Dictionary<string, SupportingTokenParameters>();
            this.securityHeaderLayout = SecurityHeaderLayout.Strict; // SecurityProtocolFactory.defaultSecurityHeaderLayout;
            this.allowInsecureTransport = defaultAllowInsecureTransport;
            this.enableUnsecuredResponse = defaultEnableUnsecuredResponse;
            this.protectTokens = defaultProtectTokens;
        }

        internal SecurityBindingElement(SecurityBindingElement elementToBeCloned)
            : base(elementToBeCloned)
        {
            if (elementToBeCloned == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("elementToBeCloned");

            this.defaultAlgorithmSuite = elementToBeCloned.defaultAlgorithmSuite;
            this.includeTimestamp = elementToBeCloned.includeTimestamp;
            this.keyEntropyMode = elementToBeCloned.keyEntropyMode;
            this.messageSecurityVersion = elementToBeCloned.messageSecurityVersion;
            this.securityHeaderLayout = elementToBeCloned.securityHeaderLayout;
            this.endpointSupportingTokenParameters = (SupportingTokenParameters)elementToBeCloned.endpointSupportingTokenParameters.Clone();
            this.optionalEndpointSupportingTokenParameters = (SupportingTokenParameters)elementToBeCloned.optionalEndpointSupportingTokenParameters.Clone();
            this.operationSupportingTokenParameters = new Dictionary<string, SupportingTokenParameters>();
            foreach (string key in elementToBeCloned.operationSupportingTokenParameters.Keys)
            {
                this.operationSupportingTokenParameters[key] = (SupportingTokenParameters)elementToBeCloned.operationSupportingTokenParameters[key].Clone();
            }
            this.optionalOperationSupportingTokenParameters = new Dictionary<string, SupportingTokenParameters>();
            foreach (string key in elementToBeCloned.optionalOperationSupportingTokenParameters.Keys)
            {
                this.optionalOperationSupportingTokenParameters[key] = (SupportingTokenParameters)elementToBeCloned.optionalOperationSupportingTokenParameters[key].Clone();
            }
            this.localServiceSettings = (LocalServiceSecuritySettings)elementToBeCloned.localServiceSettings.Clone();
            // this.internalDuplexBindingElement = elementToBeCloned.internalDuplexBindingElement;
            this.maxReceivedMessageSize = elementToBeCloned.maxReceivedMessageSize;
            this.readerQuotas = elementToBeCloned.readerQuotas;
            this.doNotEmitTrust = elementToBeCloned.doNotEmitTrust;
            this.allowInsecureTransport = elementToBeCloned.allowInsecureTransport;
            this.enableUnsecuredResponse = elementToBeCloned.enableUnsecuredResponse;
            this.supportsExtendedProtectionPolicy = elementToBeCloned.supportsExtendedProtectionPolicy;
            this.protectTokens = elementToBeCloned.protectTokens;
        }

        internal bool SupportsExtendedProtectionPolicy
        {
            get { return this.supportsExtendedProtectionPolicy; }
            set { this.supportsExtendedProtectionPolicy = value; }
        }

        public SupportingTokenParameters EndpointSupportingTokenParameters
        {
            get
            {
                return this.endpointSupportingTokenParameters;
            }
        }

        public SupportingTokenParameters OptionalEndpointSupportingTokenParameters
        {
            get
            {
                return this.optionalEndpointSupportingTokenParameters;
            }
        }

        public IDictionary<string, SupportingTokenParameters> OperationSupportingTokenParameters
        {
            get
            {
                return this.operationSupportingTokenParameters;
            }
        }

        public IDictionary<string, SupportingTokenParameters> OptionalOperationSupportingTokenParameters
        {
            get
            {
                return this.optionalOperationSupportingTokenParameters;
            }
        }

        public SecurityHeaderLayout SecurityHeaderLayout
        {
            get
            {
                return this.securityHeaderLayout;
            }
            set
            {
                if (!SecurityHeaderLayoutHelper.IsDefined(value))
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value"));

                this.securityHeaderLayout = value;
            }
        }

        public MessageSecurityVersion MessageSecurityVersion
        {
            get
            {
                return this.messageSecurityVersion;
            }
            set
            {
                if (value == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException("value"));
                this.messageSecurityVersion = value;
            }
        }

        public bool EnableUnsecuredResponse
        {
            get
            {
                return this.enableUnsecuredResponse;
            }
            set
            {
                this.enableUnsecuredResponse = value;
            }
        }

        public bool IncludeTimestamp
        {
            get
            {
                return this.includeTimestamp;
            }
            set
            {
                this.includeTimestamp = value;
            }
        }

        public bool AllowInsecureTransport
        {
            get
            {
                return this.allowInsecureTransport;
            }
            set
            {
                this.allowInsecureTransport = value;
            }
        }

        public SecurityAlgorithmSuite DefaultAlgorithmSuite
        {
            get
            {
                return this.defaultAlgorithmSuite;
            }
            set
            {
                if (value == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException("value"));
                this.defaultAlgorithmSuite = value;
            }
        }

        public bool ProtectTokens
        {
            get
            {
                return this.protectTokens;
            }
            set
            {
                this.protectTokens = value;
            }
        }

        public LocalServiceSecuritySettings LocalServiceSettings
        {
            get
            {
                return this.localServiceSettings;
            }
        }

        public SecurityKeyEntropyMode KeyEntropyMode
        {
            get
            {
                return this.keyEntropyMode;
            }
            set
            {
                if (!SecurityKeyEntropyModeHelper.IsDefined(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                }
                this.keyEntropyMode = value;
            }
        }

        internal virtual bool SessionMode
        {
            get { return false; }
        }

        internal virtual bool SupportsDuplex
        {
            get { return false; }
        }

        internal virtual bool SupportsRequestReply
        {
            get { return false; }
        }

        internal long MaxReceivedMessageSize
        {
            get { return this.maxReceivedMessageSize; }
            set { this.maxReceivedMessageSize = value; }
        }

        internal bool DoNotEmitTrust
        {
            get { return this.doNotEmitTrust; }
            set { this.doNotEmitTrust = value; }
        }

        internal XmlDictionaryReaderQuotas ReaderQuotas
        {
            get { return this.readerQuotas; }
            set { this.readerQuotas = value; }
        }

        void GetSupportingTokensCapabilities(ICollection<SecurityTokenParameters> parameters, out bool supportsClientAuth, out bool supportsWindowsIdentity)
        {
            supportsClientAuth = false;
            supportsWindowsIdentity = false;
            foreach (SecurityTokenParameters p in parameters)
            {
                if (p.SupportsClientAuthentication)
                    supportsClientAuth = true;
                if (p.SupportsClientWindowsIdentity)
                    supportsWindowsIdentity = true;
            }
        }

        void GetSupportingTokensCapabilities(SupportingTokenParameters requirements, out bool supportsClientAuth, out bool supportsWindowsIdentity)
        {
            supportsClientAuth = false;
            supportsWindowsIdentity = false;
            bool tmpSupportsClientAuth;
            bool tmpSupportsWindowsIdentity;
            this.GetSupportingTokensCapabilities(requirements.Endorsing, out tmpSupportsClientAuth, out tmpSupportsWindowsIdentity);
            supportsClientAuth = supportsClientAuth || tmpSupportsClientAuth;
            supportsWindowsIdentity = supportsWindowsIdentity || tmpSupportsWindowsIdentity;

            this.GetSupportingTokensCapabilities(requirements.SignedEndorsing, out tmpSupportsClientAuth, out tmpSupportsWindowsIdentity);
            supportsClientAuth = supportsClientAuth || tmpSupportsClientAuth;
            supportsWindowsIdentity = supportsWindowsIdentity || tmpSupportsWindowsIdentity;

            this.GetSupportingTokensCapabilities(requirements.SignedEncrypted, out tmpSupportsClientAuth, out tmpSupportsWindowsIdentity);
            supportsClientAuth = supportsClientAuth || tmpSupportsClientAuth;
            supportsWindowsIdentity = supportsWindowsIdentity || tmpSupportsWindowsIdentity;
        }

        internal void GetSupportingTokensCapabilities(out bool supportsClientAuth, out bool supportsWindowsIdentity)
        {
            this.GetSupportingTokensCapabilities(this.EndpointSupportingTokenParameters, out supportsClientAuth, out supportsWindowsIdentity);
        }

        // SecureConversation needs a demuxer below security to 1) demux between the security sessions and 2) demux the SCT issue and renewal messages
        // to the authenticator
        internal void AddDemuxerForSecureConversation(ChannelBuilder builder, BindingContext secureConversationBindingContext)
        {
            // add a demuxer element  right below security unless there's a demuxer already present below and the only 
            // binding elements between security and the demuxer are "ancillary" binding elements like message encoding element and
            // stream-security upgrade element. We could always add the channel demuxer below security but not doing so in the ancillary
            // binding elements case improves perf
            //int numChannelDemuxersBelowSecurity = 0;
            //bool doesBindingHaveShapeChangingElements = false;
            //for (int i = 0; i < builder.Binding.Elements.Count; ++i)
            //{
            //    if ((builder.Binding.Elements[i] is MessageEncodingBindingElement) || (builder.Binding.Elements[i] is StreamUpgradeBindingElement))
            //    {
            //        continue;
            //    }
            //    if (builder.Binding.Elements[i] is ChannelDemuxerBindingElement)
            //    {
            //        ++numChannelDemuxersBelowSecurity;
            //    }
            //    else if (builder.Binding.Elements[i] is TransportBindingElement)
            //    {
            //        break;
            //    }
            //    else
            //    {
            //        doesBindingHaveShapeChangingElements = true;
            //    }
            //}
            //if (numChannelDemuxersBelowSecurity == 1 && !doesBindingHaveShapeChangingElements)
            //{
            //    return;
            //}

            //ChannelDemuxerBindingElement demuxer = new ChannelDemuxerBindingElement(false);
            //demuxer.MaxPendingSessions = this.LocalServiceSettings.MaxPendingSessions;
            //demuxer.PeekTimeout = this.LocalServiceSettings.NegotiationTimeout;

            //builder.Binding.Elements.Insert(0, demuxer);
            //secureConversationBindingContext.RemainingBindingElements.Insert(0, demuxer);
        }

        internal void ApplyPropertiesOnDemuxer(ChannelBuilder builder, BindingContext context)
        {
            /* TODO later
             Collection<ChannelDemuxerBindingElement> demuxerElements = builder.Binding.Elements.FindAll<ChannelDemuxerBindingElement>();
             foreach (ChannelDemuxerBindingElement element in demuxerElements)
             {
                 if (element != null)
                 {
                     element.MaxPendingSessions = this.LocalServiceSettings.MaxPendingSessions;
                     element.PeekTimeout = this.LocalServiceSettings.NegotiationTimeout;
                 }
             }*/
        }

        /*
        static BindingContext CreateIssuerBindingContextForNegotiation(BindingContext issuerBindingContext)
        {
            TransportBindingElement transport = issuerBindingContext.RemainingBindingElements.Find<TransportBindingElement>();
            if (transport == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.GetString(SR.TransportBindingElementNotFound)));
            }
            ChannelDemuxerBindingElement demuxer = null;
            // pick the demuxer above transport (i.e. the last demuxer in the array)
            for (int i = 0; i < issuerBindingContext.RemainingBindingElements.Count; ++i)
            {
                if (issuerBindingContext.RemainingBindingElements[i] is ChannelDemuxerBindingElement)
                {
                    demuxer = (ChannelDemuxerBindingElement)issuerBindingContext.RemainingBindingElements[i];
                }
            }
            if (demuxer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.GetString(SR.ChannelDemuxerBindingElementNotFound)));
            }
            BindingElementCollection negotiationBindingElements = new BindingElementCollection();
            negotiationBindingElements.Add(demuxer.Clone());
            negotiationBindingElements.Add(transport.Clone());
            CustomBinding binding = new CustomBinding(negotiationBindingElements);
            binding.OpenTimeout = issuerBindingContext.Binding.OpenTimeout;
            binding.CloseTimeout = issuerBindingContext.Binding.CloseTimeout;
            binding.SendTimeout = issuerBindingContext.Binding.SendTimeout;
            binding.ReceiveTimeout = issuerBindingContext.Binding.ReceiveTimeout;
            if (issuerBindingContext.ListenUriBaseAddress != null)
            {
                return new BindingContext(binding, new BindingParameterCollection(issuerBindingContext.BindingParameters), issuerBindingContext.ListenUriBaseAddress,
                    issuerBindingContext.ListenUriRelativeAddress, issuerBindingContext.ListenUriMode);
            }
            else
            {
                return new BindingContext(binding, new BindingParameterCollection(issuerBindingContext.BindingParameters));
            }
        }

        protected static void SetIssuerBindingContextIfRequired(SecurityTokenParameters parameters, BindingContext issuerBindingContext)
        {
            if (parameters is SslSecurityTokenParameters)
            {
                ((SslSecurityTokenParameters)parameters).IssuerBindingContext = CreateIssuerBindingContextForNegotiation(issuerBindingContext);
            }
            else if (parameters is SspiSecurityTokenParameters)
            {
                ((SspiSecurityTokenParameters)parameters).IssuerBindingContext = CreateIssuerBindingContextForNegotiation(issuerBindingContext);
            }
        }

        static void SetIssuerBindingContextIfRequired(SupportingTokenParameters supportingParameters, BindingContext issuerBindingContext)
        {
            for (int i = 0; i < supportingParameters.Endorsing.Count; ++i)
            {
                SetIssuerBindingContextIfRequired(supportingParameters.Endorsing[i], issuerBindingContext);
            }
            for (int i = 0; i < supportingParameters.SignedEndorsing.Count; ++i)
            {
                SetIssuerBindingContextIfRequired(supportingParameters.SignedEndorsing[i], issuerBindingContext);
            }
            for (int i = 0; i < supportingParameters.Signed.Count; ++i)
            {
                SetIssuerBindingContextIfRequired(supportingParameters.Signed[i], issuerBindingContext);
            }
            for (int i = 0; i < supportingParameters.SignedEncrypted.Count; ++i)
            {
                SetIssuerBindingContextIfRequired(supportingParameters.SignedEncrypted[i], issuerBindingContext);
            }
        }

        void SetIssuerBindingContextIfRequired(BindingContext issuerBindingContext)
        {
            SetIssuerBindingContextIfRequired(this.EndpointSupportingTokenParameters, issuerBindingContext);
            SetIssuerBindingContextIfRequired(this.OptionalEndpointSupportingTokenParameters, issuerBindingContext);
            foreach (SupportingTokenParameters parameters in this.OperationSupportingTokenParameters.Values)
            {
                SetIssuerBindingContextIfRequired(parameters, issuerBindingContext);
            }
            foreach (SupportingTokenParameters parameters in this.OptionalOperationSupportingTokenParameters.Values)
            {
                SetIssuerBindingContextIfRequired(parameters, issuerBindingContext);
            }
        }*/

        internal bool RequiresChannelDemuxer(SecurityTokenParameters parameters)
        {
            return ((parameters is SecureConversationSecurityTokenParameters)
                    || (parameters is SslSecurityTokenParameters)
                    || (parameters is SspiSecurityTokenParameters));
        }

        internal virtual bool RequiresChannelDemuxer()
        {
            foreach (SecurityTokenParameters parameters in EndpointSupportingTokenParameters.Endorsing)
            {
                if (RequiresChannelDemuxer(parameters))
                {
                    return true;
                }
            }
            foreach (SecurityTokenParameters parameters in EndpointSupportingTokenParameters.SignedEndorsing)
            {
                if (RequiresChannelDemuxer(parameters))
                {
                    return true;
                }
            }
            foreach (SecurityTokenParameters parameters in OptionalEndpointSupportingTokenParameters.Endorsing)
            {
                if (RequiresChannelDemuxer(parameters))
                {
                    return true;
                }
            }
            foreach (SecurityTokenParameters parameters in OptionalEndpointSupportingTokenParameters.SignedEndorsing)
            {
                if (RequiresChannelDemuxer(parameters))
                {
                    return true;
                }
            }
            foreach (SupportingTokenParameters supportingParameters in OperationSupportingTokenParameters.Values)
            {
                foreach (SecurityTokenParameters parameters in supportingParameters.Endorsing)
                {
                    if (RequiresChannelDemuxer(parameters))
                    {
                        return true;
                    }
                }
                foreach (SecurityTokenParameters parameters in supportingParameters.SignedEndorsing)
                {
                    if (RequiresChannelDemuxer(parameters))
                    {
                        return true;
                    }
                }
            }
            foreach (SupportingTokenParameters supportingParameters in OptionalOperationSupportingTokenParameters.Values)
            {
                foreach (SecurityTokenParameters parameters in supportingParameters.Endorsing)
                {
                    if (RequiresChannelDemuxer(parameters))
                    {
                        return true;
                    }
                }
                foreach (SecurityTokenParameters parameters in supportingParameters.SignedEndorsing)
                {
                    if (RequiresChannelDemuxer(parameters))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        //void SetPrivacyNoticeUriIfRequired(SecurityProtocolFactory factory, Binding binding)
        //{
        //    PrivacyNoticeBindingElement privacyElement = binding.CreateBindingElements().Find<PrivacyNoticeBindingElement>();
        //    if (privacyElement != null)
        //    {
        //        factory.PrivacyNoticeUri = privacyElement.Url;
        //        factory.PrivacyNoticeVersion = privacyElement.Version;
        //    }
        //}
        
        internal bool IsUnderlyingDispatcherDuplex<TChannel>(BindingContext context)
        {
            return ((typeof(TChannel) == typeof(IDuplexSessionChannel)) && context.CanBuildNextServiceDispatcher<IDuplexChannel>()
                && !context.CanBuildNextServiceDispatcher<IDuplexSessionChannel>());
        }

        internal void ConfigureProtocolFactory(SecurityProtocolFactory factory, SecurityCredentialsManager credentialsManager, bool isForService, BindingContext issuerBindingContext, Binding binding)
        {
            if (factory == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(factory)));
            if (credentialsManager == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(credentialsManager)));
            factory.AddTimestamp = this.IncludeTimestamp;
            factory.IncomingAlgorithmSuite = this.DefaultAlgorithmSuite;
            factory.OutgoingAlgorithmSuite = this.DefaultAlgorithmSuite;
            factory.SecurityHeaderLayout = this.SecurityHeaderLayout;
            factory.TimestampValidityDuration = this.LocalServiceSettings.TimestampValidityDuration;
            factory.DetectReplays = this.LocalServiceSettings.DetectReplays;
            factory.MaxCachedNonces = this.LocalServiceSettings.ReplayCacheSize;
            factory.MaxClockSkew = this.LocalServiceSettings.MaxClockSkew;
            factory.ReplayWindow = this.LocalServiceSettings.ReplayWindow;

            if (this.LocalServiceSettings.DetectReplays)
            {
                factory.NonceCache = this.LocalServiceSettings.NonceCache;
            }
            factory.SecurityBindingElement = (SecurityBindingElement)this.Clone();
            factory.SecurityBindingElement.SetIssuerBindingContextIfRequired(issuerBindingContext);
            factory.SecurityTokenManager = credentialsManager.CreateSecurityTokenManager();
            SecurityTokenSerializer tokenSerializer = factory.SecurityTokenManager.CreateSecurityTokenSerializer(this.messageSecurityVersion.SecurityTokenVersion);
            factory.StandardsManager = new SecurityStandardsManager(this.messageSecurityVersion, tokenSerializer);
        }

        internal abstract SecurityProtocolFactory CreateSecurityProtocolFactory<TChannel>(BindingContext context, SecurityCredentialsManager credentialsManager,
        bool isForService, BindingContext issuanceBindingContext);

        public override IServiceDispatcher BuildServiceDispatcher<TChannel>(BindingContext context, IServiceDispatcher innerDispatcher)
        {
            if (context == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("context");

            if (!this.CanBuildServiceDispatcher<TChannel>(context))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.Format(SR.ChannelTypeNotSupported, typeof(TChannel)), "TChannel"));
            }

            this.readerQuotas = context.GetInnerProperty<XmlDictionaryReaderQuotas>();
            if (readerQuotas == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.EncodingBindingElementDoesNotHandleReaderQuotas)));
            }

            TransportBindingElement transportBindingElement = null;
            if (context.RemainingBindingElements != null)
                transportBindingElement = context.RemainingBindingElements.Find<TransportBindingElement>();

            if (transportBindingElement != null)
                this.maxReceivedMessageSize = transportBindingElement.MaxReceivedMessageSize;
            return this.BuildServiceDispatcherCore<TChannel>(context, innerDispatcher);
        }
        protected abstract IServiceDispatcher BuildServiceDispatcherCore<TChannel>(BindingContext context, IServiceDispatcher serviceDispatcher)
            where TChannel : class, IChannel;
        public override bool CanBuildServiceDispatcher<TChannel>(BindingContext context)
        {
            if (context == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(context));

            // InternalDuplexBindingElement.AddDuplexListenerSupport(context, ref this.internalDuplexBindingElement);

            //  if (this.SessionMode)
            //  {
            //      return this.CanBuildSessionChannelListener<TChannel>(context);
            //  }

            if (!context.CanBuildNextServiceDispatcher<TChannel>())
            {
                return false;
            }

            return typeof(TChannel) == typeof(IInputChannel) || typeof(TChannel) == typeof(IInputSessionChannel) ||
                (this.SupportsDuplex && (typeof(TChannel) == typeof(IDuplexChannel) || typeof(TChannel) == typeof(IDuplexSessionChannel))) ||
                (this.SupportsRequestReply && (typeof(TChannel) == typeof(IReplyChannel) || typeof(TChannel) == typeof(IReplySessionChannel)));
        }


        public virtual void SetKeyDerivation(bool requireDerivedKeys)
        {
            this.EndpointSupportingTokenParameters.SetKeyDerivation(requireDerivedKeys);
            this.OptionalEndpointSupportingTokenParameters.SetKeyDerivation(requireDerivedKeys);
            foreach (SupportingTokenParameters t in this.OperationSupportingTokenParameters.Values)
                t.SetKeyDerivation(requireDerivedKeys);
            foreach (SupportingTokenParameters t in this.OptionalOperationSupportingTokenParameters.Values)
            {
                t.SetKeyDerivation(requireDerivedKeys);
            }
        }

        internal ChannelProtectionRequirements GetProtectionRequirements(AddressingVersion addressing, ProtectionLevel defaultProtectionLevel)
        {
            if (addressing == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("addressing");

            ChannelProtectionRequirements result = new ChannelProtectionRequirements();
            ProtectionLevel supportedRequestProtectionLevel = this.GetIndividualProperty<ISecurityCapabilities>().SupportedRequestProtectionLevel;
            ProtectionLevel supportedResponseProtectionLevel = this.GetIndividualProperty<ISecurityCapabilities>().SupportedResponseProtectionLevel;

            bool canSupportMoreThanTheDefault =
                (ProtectionLevelHelper.IsStrongerOrEqual(supportedRequestProtectionLevel, defaultProtectionLevel)
                && ProtectionLevelHelper.IsStrongerOrEqual(supportedResponseProtectionLevel, defaultProtectionLevel));
            if (canSupportMoreThanTheDefault)
            {
                MessagePartSpecification signedParts = new MessagePartSpecification();
                MessagePartSpecification encryptedParts = new MessagePartSpecification();
                if (defaultProtectionLevel != ProtectionLevel.None)
                {
                    signedParts.IsBodyIncluded = true;
                    if (defaultProtectionLevel == ProtectionLevel.EncryptAndSign)
                    {
                        encryptedParts.IsBodyIncluded = true;
                    }
                }
                signedParts.MakeReadOnly();
                encryptedParts.MakeReadOnly();
                if (addressing.FaultAction != null)
                {
                    // Addressing faults
                    result.IncomingSignatureParts.AddParts(signedParts, addressing.FaultAction);
                    result.OutgoingSignatureParts.AddParts(signedParts, addressing.FaultAction);
                    result.IncomingEncryptionParts.AddParts(encryptedParts, addressing.FaultAction);
                    result.OutgoingEncryptionParts.AddParts(encryptedParts, addressing.FaultAction);
                }
                if (addressing.DefaultFaultAction != null)
                {
                    // Faults that do not specify a particular action
                    result.IncomingSignatureParts.AddParts(signedParts, addressing.DefaultFaultAction);
                    result.OutgoingSignatureParts.AddParts(signedParts, addressing.DefaultFaultAction);
                    result.IncomingEncryptionParts.AddParts(encryptedParts, addressing.DefaultFaultAction);
                    result.OutgoingEncryptionParts.AddParts(encryptedParts, addressing.DefaultFaultAction);
                }
                // Infrastructure faults
                result.IncomingSignatureParts.AddParts(signedParts, FaultCodeConstants.Actions.NetDispatcher);
                result.OutgoingSignatureParts.AddParts(signedParts, FaultCodeConstants.Actions.NetDispatcher);
                result.IncomingEncryptionParts.AddParts(encryptedParts, FaultCodeConstants.Actions.NetDispatcher);
                result.OutgoingEncryptionParts.AddParts(encryptedParts, FaultCodeConstants.Actions.NetDispatcher);
            }

            return result;
        }

        public override T GetProperty<T>(BindingContext context)
        {
            return context.GetInnerProperty<T>();
        }

        internal abstract ISecurityCapabilities GetIndividualISecurityCapabilities();

        ISecurityCapabilities GetSecurityCapabilities(BindingContext context)
        {
            ISecurityCapabilities thisSecurityCapability = this.GetIndividualISecurityCapabilities();
            ISecurityCapabilities lowerSecurityCapability = context.GetInnerProperty<ISecurityCapabilities>();
            if (lowerSecurityCapability == null)
            {
                return thisSecurityCapability;
            }
            else
            {
                bool supportsClientAuth = thisSecurityCapability.SupportsClientAuthentication;
                bool supportsClientWindowsIdentity = thisSecurityCapability.SupportsClientWindowsIdentity;
                bool supportsServerAuth = thisSecurityCapability.SupportsServerAuthentication || lowerSecurityCapability.SupportsServerAuthentication;
                ProtectionLevel requestProtectionLevel = ProtectionLevelHelper.Max(thisSecurityCapability.SupportedRequestProtectionLevel, lowerSecurityCapability.SupportedRequestProtectionLevel);
                ProtectionLevel responseProtectionLevel = ProtectionLevelHelper.Max(thisSecurityCapability.SupportedResponseProtectionLevel, lowerSecurityCapability.SupportedResponseProtectionLevel);
                return new SecurityCapabilities(supportsClientAuth, supportsServerAuth, supportsClientWindowsIdentity, requestProtectionLevel, responseProtectionLevel);
            }
        }
        void SetIssuerBindingContextIfRequired(BindingContext issuerBindingContext)
        {
            SetIssuerBindingContextIfRequired(this.EndpointSupportingTokenParameters, issuerBindingContext);
            SetIssuerBindingContextIfRequired(this.OptionalEndpointSupportingTokenParameters, issuerBindingContext);
            foreach (SupportingTokenParameters parameters in this.OperationSupportingTokenParameters.Values)
            {
                SetIssuerBindingContextIfRequired(parameters, issuerBindingContext);
            }
            foreach (SupportingTokenParameters parameters in this.OptionalOperationSupportingTokenParameters.Values)
            {
                SetIssuerBindingContextIfRequired(parameters, issuerBindingContext);
            }
        }
       

        protected static void SetIssuerBindingContextIfRequired(SecurityTokenParameters parameters, BindingContext issuerBindingContext)
        {
            if (parameters is SslSecurityTokenParameters)
            {
                throw new NotImplementedException();
                //((SslSecurityTokenParameters)parameters).IssuerBindingContext = CreateIssuerBindingContextForNegotiation(issuerBindingContext);
            }
            else if (parameters is SspiSecurityTokenParameters)
            {
                 throw new NotImplementedException();
                // ((SspiSecurityTokenParameters)parameters).IssuerBindingContext = CreateIssuerBindingContextForNegotiation(issuerBindingContext);
            }
        }

        static void SetIssuerBindingContextIfRequired(SupportingTokenParameters supportingParameters, BindingContext issuerBindingContext)
        {
            for (int i = 0; i < supportingParameters.Endorsing.Count; ++i)
            {
                SetIssuerBindingContextIfRequired(supportingParameters.Endorsing[i], issuerBindingContext);
            }
            for (int i = 0; i < supportingParameters.SignedEndorsing.Count; ++i)
            {
                SetIssuerBindingContextIfRequired(supportingParameters.SignedEndorsing[i], issuerBindingContext);
            }
            for (int i = 0; i < supportingParameters.Signed.Count; ++i)
            {
                SetIssuerBindingContextIfRequired(supportingParameters.Signed[i], issuerBindingContext);
            }
            for (int i = 0; i < supportingParameters.SignedEncrypted.Count; ++i)
            {
                SetIssuerBindingContextIfRequired(supportingParameters.SignedEncrypted[i], issuerBindingContext);
            }
        }

        // If any changes are made to this method, please make sure that they are
        // reflected in the corresponding IsUserNameOverTransportBinding() method.
        static public TransportSecurityBindingElement CreateUserNameOverTransportBindingElement()
        {
            TransportSecurityBindingElement result = new TransportSecurityBindingElement();
            result.EndpointSupportingTokenParameters.SignedEncrypted.Add(
                new UserNameSecurityTokenParameters());
            result.IncludeTimestamp = true;
            //result.LocalClientSettings.DetectReplays = false;
            result.LocalServiceSettings.DetectReplays = false;
            return result;
        }

        // If any changes are made to this method, please make sure that they are
        // reflected in the corresponding IsSecureConversationBinding() method.
        static public SecurityBindingElement CreateSecureConversationBindingElement(SecurityBindingElement bootstrapSecurity)
        {
            return CreateSecureConversationBindingElement(bootstrapSecurity, SecureConversationSecurityTokenParameters.defaultRequireCancellation, null);
        }

        static public SecurityBindingElement CreateSecureConversationBindingElement(SecurityBindingElement bootstrapSecurity, bool requireCancellation)
        {
            return CreateSecureConversationBindingElement(bootstrapSecurity, requireCancellation, null);
        }

        // If any changes are made to this method, please make sure that they are
        // reflected in the corresponding IsCertificateOverTransportBinding() method.
        static public TransportSecurityBindingElement CreateCertificateOverTransportBindingElement()
        {
            return CreateCertificateOverTransportBindingElement(MessageSecurityVersion.Default);
        }

        // If any changes are made to this method, please make sure that they are
        // reflected in the corresponding IsCertificateOverTransportBinding() method.
        static public TransportSecurityBindingElement CreateCertificateOverTransportBindingElement(MessageSecurityVersion version)
        {
            if (version == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("version");
            }
            X509KeyIdentifierClauseType x509ReferenceType;

            if (version.SecurityVersion == SecurityVersion.WSSecurity10)
            {
                x509ReferenceType = X509KeyIdentifierClauseType.Any;
            }
            else
            {
                x509ReferenceType = X509KeyIdentifierClauseType.Thumbprint;
            }

            TransportSecurityBindingElement result = new TransportSecurityBindingElement();
            X509SecurityTokenParameters x509Parameters = new X509SecurityTokenParameters(
                    x509ReferenceType,
                    SecurityTokenInclusionMode.AlwaysToRecipient,
                    false);
            result.EndpointSupportingTokenParameters.Endorsing.Add(
                x509Parameters
                );
            result.IncludeTimestamp = true;
           // result.LocalClientSettings.DetectReplays = false;
            result.LocalServiceSettings.DetectReplays = false;
            result.MessageSecurityVersion = version;

            return result;
        }

        // this method reverses CreateMutualCertificateBindingElement() logic
        internal static bool IsCertificateOverTransportBinding(SecurityBindingElement sbe)
        {
            // do not check local settings: sbe.LocalServiceSettings and sbe.LocalClientSettings
            if (!sbe.IncludeTimestamp)
                return false;

            if (!(sbe is TransportSecurityBindingElement))
                return false;

            SupportingTokenParameters parameters = sbe.EndpointSupportingTokenParameters;
            if (parameters.Signed.Count != 0 || parameters.SignedEncrypted.Count != 0 || parameters.Endorsing.Count != 1 || parameters.SignedEndorsing.Count != 0)
                return false;

            X509SecurityTokenParameters x509Parameters = parameters.Endorsing[0] as X509SecurityTokenParameters;
            if (x509Parameters == null)
                return false;

            if (x509Parameters.InclusionMode != SecurityTokenInclusionMode.AlwaysToRecipient)
                return false;

            return x509Parameters.X509ReferenceStyle == X509KeyIdentifierClauseType.Any || x509Parameters.X509ReferenceStyle == X509KeyIdentifierClauseType.Thumbprint;
        }


        // If any changes are made to this method, please make sure that they are
        // reflected in the corresponding IsSecureConversationBinding() method.
        static public SecurityBindingElement CreateSecureConversationBindingElement(SecurityBindingElement bootstrapSecurity, bool requireCancellation, ChannelProtectionRequirements bootstrapProtectionRequirements)
        {
            if (bootstrapSecurity == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("bootstrapBinding");

            SecurityBindingElement result;

            if (bootstrapSecurity is TransportSecurityBindingElement)
            {
                // there is no need to do replay detection or key derivation for transport bindings
                TransportSecurityBindingElement primary = new TransportSecurityBindingElement();
                SecureConversationSecurityTokenParameters scParameters = new SecureConversationSecurityTokenParameters(
                        bootstrapSecurity,
                        requireCancellation,
                        bootstrapProtectionRequirements);
                scParameters.RequireDerivedKeys = false;
                primary.EndpointSupportingTokenParameters.Endorsing.Add(
                    scParameters);
               // primary.LocalClientSettings.DetectReplays = false;
                primary.LocalServiceSettings.DetectReplays = false;
                primary.IncludeTimestamp = true;
                result = primary;
            }
            else // Symmetric- or AsymmetricSecurityBindingElement
            {
                SymmetricSecurityBindingElement primary = new SymmetricSecurityBindingElement(
                    new SecureConversationSecurityTokenParameters(
                        bootstrapSecurity,
                        requireCancellation,
                        bootstrapProtectionRequirements));
                // there is no need for signature confirmation on the steady state binding
                primary.RequireSignatureConfirmation = false;
                result = primary;
            }
            return result;
        }
        //TODO other security mode
    }
}
