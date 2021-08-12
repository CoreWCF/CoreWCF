﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
        internal const string DefaultAlgorithmSuiteString = "Default";
        internal static readonly SecurityAlgorithmSuite s_defaultDefaultAlgorithmSuite = SecurityAlgorithmSuite.Default;
        internal const bool DefaultIncludeTimestamp = true;
        internal const bool DefaultAllowInsecureTransport = false;
        internal const MessageProtectionOrder DefaultMessageProtectionOrder = MessageProtectionOrder.SignBeforeEncryptAndEncryptSignature;
        internal const bool DefaultRequireSignatureConfirmation = false;
        internal const bool DefaultEnableUnsecuredResponse = false;
        internal const bool DefaultProtectTokens = false;
        private SecurityAlgorithmSuite _defaultAlgorithmSuite;
        private SecurityKeyEntropyMode _keyEntropyMode;
        private readonly Dictionary<string, SupportingTokenParameters> _operationSupportingTokenParameters;
        private readonly Dictionary<string, SupportingTokenParameters> _optionalOperationSupportingTokenParameters;
        private MessageSecurityVersion _messageSecurityVersion;
        private SecurityHeaderLayout _securityHeaderLayout;

        internal SecurityBindingElement()
            : base()
        {
            _messageSecurityVersion = MessageSecurityVersion.Default;
            _keyEntropyMode = SecurityKeyEntropyMode.CombinedEntropy; // AcceleratedTokenProvider.defaultKeyEntropyMode;
            IncludeTimestamp = DefaultIncludeTimestamp;
            _defaultAlgorithmSuite = s_defaultDefaultAlgorithmSuite;
            LocalServiceSettings = new LocalServiceSecuritySettings();
            EndpointSupportingTokenParameters = new SupportingTokenParameters();
            OptionalEndpointSupportingTokenParameters = new SupportingTokenParameters();
            _operationSupportingTokenParameters = new Dictionary<string, SupportingTokenParameters>();
            _optionalOperationSupportingTokenParameters = new Dictionary<string, SupportingTokenParameters>();
            _securityHeaderLayout = SecurityHeaderLayout.Strict; // SecurityProtocolFactory.defaultSecurityHeaderLayout;
            AllowInsecureTransport = DefaultAllowInsecureTransport;
            EnableUnsecuredResponse = DefaultEnableUnsecuredResponse;
            ProtectTokens = DefaultProtectTokens;
        }

        internal SecurityBindingElement(SecurityBindingElement elementToBeCloned)
            : base(elementToBeCloned)
        {
            if (elementToBeCloned == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(elementToBeCloned));
            }

            _defaultAlgorithmSuite = elementToBeCloned._defaultAlgorithmSuite;
            IncludeTimestamp = elementToBeCloned.IncludeTimestamp;
            _keyEntropyMode = elementToBeCloned._keyEntropyMode;
            _messageSecurityVersion = elementToBeCloned._messageSecurityVersion;
            _securityHeaderLayout = elementToBeCloned._securityHeaderLayout;
            EndpointSupportingTokenParameters = (SupportingTokenParameters)elementToBeCloned.EndpointSupportingTokenParameters.Clone();
            OptionalEndpointSupportingTokenParameters = (SupportingTokenParameters)elementToBeCloned.OptionalEndpointSupportingTokenParameters.Clone();
            _operationSupportingTokenParameters = new Dictionary<string, SupportingTokenParameters>();
            foreach (string key in elementToBeCloned._operationSupportingTokenParameters.Keys)
            {
                _operationSupportingTokenParameters[key] = (SupportingTokenParameters)elementToBeCloned._operationSupportingTokenParameters[key].Clone();
            }
            _optionalOperationSupportingTokenParameters = new Dictionary<string, SupportingTokenParameters>();
            foreach (string key in elementToBeCloned._optionalOperationSupportingTokenParameters.Keys)
            {
                _optionalOperationSupportingTokenParameters[key] = (SupportingTokenParameters)elementToBeCloned._optionalOperationSupportingTokenParameters[key].Clone();
            }
            LocalServiceSettings = (LocalServiceSecuritySettings)elementToBeCloned.LocalServiceSettings.Clone();
            // this.internalDuplexBindingElement = elementToBeCloned.internalDuplexBindingElement;
            MaxReceivedMessageSize = elementToBeCloned.MaxReceivedMessageSize;
            ReaderQuotas = elementToBeCloned.ReaderQuotas;
            DoNotEmitTrust = elementToBeCloned.DoNotEmitTrust;
            AllowInsecureTransport = elementToBeCloned.AllowInsecureTransport;
            EnableUnsecuredResponse = elementToBeCloned.EnableUnsecuredResponse;
            SupportsExtendedProtectionPolicy = elementToBeCloned.SupportsExtendedProtectionPolicy;
            ProtectTokens = elementToBeCloned.ProtectTokens;
        }

        internal bool SupportsExtendedProtectionPolicy { get; set; }

        public SupportingTokenParameters EndpointSupportingTokenParameters { get; }

        public SupportingTokenParameters OptionalEndpointSupportingTokenParameters { get; }

        public IDictionary<string, SupportingTokenParameters> OperationSupportingTokenParameters
        {
            get
            {
                return _operationSupportingTokenParameters;
            }
        }

        public IDictionary<string, SupportingTokenParameters> OptionalOperationSupportingTokenParameters
        {
            get
            {
                return _optionalOperationSupportingTokenParameters;
            }
        }

        public SecurityHeaderLayout SecurityHeaderLayout
        {
            get
            {
                return _securityHeaderLayout;
            }
            set
            {
                if (!SecurityHeaderLayoutHelper.IsDefined(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                }

                _securityHeaderLayout = value;
            }
        }

        public MessageSecurityVersion MessageSecurityVersion
        {
            get
            {
                return _messageSecurityVersion;
            }
            set
            {
                _messageSecurityVersion = value ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(value)));
            }
        }

        public bool EnableUnsecuredResponse { get; set; }

        public bool IncludeTimestamp { get; set; }

        public bool AllowInsecureTransport { get; set; }

        public SecurityAlgorithmSuite DefaultAlgorithmSuite
        {
            get
            {
                return _defaultAlgorithmSuite;
            }
            set
            {
                _defaultAlgorithmSuite = value ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(value)));
            }
        }

        public bool ProtectTokens { get; set; } = DefaultProtectTokens;

        public LocalServiceSecuritySettings LocalServiceSettings { get; }

        public SecurityKeyEntropyMode KeyEntropyMode
        {
            get
            {
                return _keyEntropyMode;
            }
            set
            {
                if (!SecurityKeyEntropyModeHelper.IsDefined(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                }
                _keyEntropyMode = value;
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

        internal long MaxReceivedMessageSize { get; set; } = TransportDefaults.MaxReceivedMessageSize;

        internal bool DoNotEmitTrust { get; set; } = false;

        internal XmlDictionaryReaderQuotas ReaderQuotas { get; set; }

        private void GetSupportingTokensCapabilities(ICollection<SecurityTokenParameters> parameters, out bool supportsClientAuth, out bool supportsWindowsIdentity)
        {
            supportsClientAuth = false;
            supportsWindowsIdentity = false;
            foreach (SecurityTokenParameters p in parameters)
            {
                if (p.SupportsClientAuthentication)
                {
                    supportsClientAuth = true;
                }

                if (p.SupportsClientWindowsIdentity)
                {
                    supportsWindowsIdentity = true;
                }
            }
        }

        private void GetSupportingTokensCapabilities(SupportingTokenParameters requirements, out bool supportsClientAuth, out bool supportsWindowsIdentity)
        {
            supportsClientAuth = false;
            supportsWindowsIdentity = false;
            GetSupportingTokensCapabilities(requirements.Endorsing, out bool tmpSupportsClientAuth, out bool tmpSupportsWindowsIdentity);
            supportsClientAuth = supportsClientAuth || tmpSupportsClientAuth;
            supportsWindowsIdentity = supportsWindowsIdentity || tmpSupportsWindowsIdentity;

            GetSupportingTokensCapabilities(requirements.SignedEndorsing, out tmpSupportsClientAuth, out tmpSupportsWindowsIdentity);
            supportsClientAuth = supportsClientAuth || tmpSupportsClientAuth;
            supportsWindowsIdentity = supportsWindowsIdentity || tmpSupportsWindowsIdentity;

            GetSupportingTokensCapabilities(requirements.SignedEncrypted, out tmpSupportsClientAuth, out tmpSupportsWindowsIdentity);
            supportsClientAuth = supportsClientAuth || tmpSupportsClientAuth;
            supportsWindowsIdentity = supportsWindowsIdentity || tmpSupportsWindowsIdentity;
        }

        internal void GetSupportingTokensCapabilities(out bool supportsClientAuth, out bool supportsWindowsIdentity)
        {
            GetSupportingTokensCapabilities(EndpointSupportingTokenParameters, out supportsClientAuth, out supportsWindowsIdentity);
        }

        internal void AddDemuxerForSecureConversation(ChannelBuilder builder, BindingContext secureConversationBindingContext)
        {
            //new way
            secureConversationBindingContext.BindingParameters.Add(builder);
        }

            // SecureConversation needs a demuxer below security to 1) demux between the security sessions and 2) demux the SCT issue and renewal messages
            // to the authenticator
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

        private static BindingContext CreateIssuerBindingContextForNegotiation(BindingContext issuerBindingContext)
        {
            TransportBindingElement transport = issuerBindingContext.RemainingBindingElements.Find<TransportBindingElement>();
            if (transport == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.TransportBindingElementNotFound)));
            }
            //ChannelDemuxerBindingElement demuxer = null;
            //// pick the demuxer above transport (i.e. the last demuxer in the array)
            //for (int i = 0; i < issuerBindingContext.RemainingBindingElements.Count; ++i)
            //{
            //    if (issuerBindingContext.RemainingBindingElements[i] is ChannelDemuxerBindingElement)
            //    {
            //        demuxer = (ChannelDemuxerBindingElement)issuerBindingContext.RemainingBindingElements[i];
            //    }
            //}
            //if (demuxer == null)
            //{
            //    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.GetString(SR.ChannelDemuxerBindingElementNotFound)));
            //}
            BindingElementCollection negotiationBindingElements = new BindingElementCollection
            {
                //negotiationBindingElements.Add(demuxer.Clone());
                transport.Clone()
            };
            CustomBinding binding = new CustomBinding(negotiationBindingElements)
            {
                OpenTimeout = issuerBindingContext.Binding.OpenTimeout,
                CloseTimeout = issuerBindingContext.Binding.CloseTimeout,
                SendTimeout = issuerBindingContext.Binding.SendTimeout,
                ReceiveTimeout = issuerBindingContext.Binding.ReceiveTimeout
            };
            if (issuerBindingContext.ListenUriBaseAddress != null)
            {
                return new BindingContext(binding, new BindingParameterCollection(issuerBindingContext.BindingParameters), issuerBindingContext.ListenUriBaseAddress,
                    issuerBindingContext.ListenUriRelativeAddress);//, issuerBindingContext.ListenUriMode);
            }
            else
            {
                return new BindingContext(binding, new BindingParameterCollection(issuerBindingContext.BindingParameters));
            }
        }

        /*
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

        internal bool IsUnderlyingDispatcherDuplex<TChannel>(BindingContext context)
        {
            return ((typeof(TChannel) == typeof(IDuplexSessionChannel)) && context.CanBuildNextServiceDispatcher<IDuplexChannel>()
                && !context.CanBuildNextServiceDispatcher<IDuplexSessionChannel>());
        }

        internal void ConfigureProtocolFactory(SecurityProtocolFactory factory, SecurityCredentialsManager credentialsManager, bool isForService, BindingContext issuerBindingContext, Binding binding)
        {
            if (factory == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(factory)));
            }

            if (credentialsManager == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(credentialsManager)));
            }

            factory.AddTimestamp = IncludeTimestamp;
            factory.IncomingAlgorithmSuite = DefaultAlgorithmSuite;
            factory.OutgoingAlgorithmSuite = DefaultAlgorithmSuite;
            factory.SecurityHeaderLayout = SecurityHeaderLayout;
            factory.TimestampValidityDuration = LocalServiceSettings.TimestampValidityDuration;
            factory.DetectReplays = LocalServiceSettings.DetectReplays;
            factory.MaxCachedNonces = LocalServiceSettings.ReplayCacheSize;
            factory.MaxClockSkew = LocalServiceSettings.MaxClockSkew;
            factory.ReplayWindow = LocalServiceSettings.ReplayWindow;

            if (LocalServiceSettings.DetectReplays)
            {
                factory.NonceCache = LocalServiceSettings.NonceCache;
            }
            factory.SecurityBindingElement = (SecurityBindingElement)Clone();
            factory.SecurityBindingElement.SetIssuerBindingContextIfRequired(issuerBindingContext);
            factory.SecurityTokenManager = credentialsManager.CreateSecurityTokenManager();
            SecurityTokenSerializer tokenSerializer = factory.SecurityTokenManager.CreateSecurityTokenSerializer(_messageSecurityVersion.SecurityTokenVersion);
            factory.StandardsManager = new SecurityStandardsManager(_messageSecurityVersion, tokenSerializer);
        }

        internal abstract SecurityProtocolFactory CreateSecurityProtocolFactory<TChannel>(BindingContext context, SecurityCredentialsManager credentialsManager,
        bool isForService, BindingContext issuanceBindingContext);

        public override IServiceDispatcher BuildServiceDispatcher<TChannel>(BindingContext context, IServiceDispatcher innerDispatcher)
        {
            if (context == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(context));
            }

            if (!CanBuildServiceDispatcher<TChannel>(context))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.Format(SR.ChannelTypeNotSupported, typeof(TChannel)), "TChannel"));
            }

            ReaderQuotas = context.GetInnerProperty<XmlDictionaryReaderQuotas>();
            if (ReaderQuotas == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.EncodingBindingElementDoesNotHandleReaderQuotas)));
            }

            TransportBindingElement transportBindingElement = null;
            if (context.RemainingBindingElements != null)
            {
                transportBindingElement = context.RemainingBindingElements.Find<TransportBindingElement>();
            }

            if (transportBindingElement != null)
            {
                MaxReceivedMessageSize = transportBindingElement.MaxReceivedMessageSize;
            }

            return BuildServiceDispatcherCore<TChannel>(context, innerDispatcher);
        }
        protected abstract IServiceDispatcher BuildServiceDispatcherCore<TChannel>(BindingContext context, IServiceDispatcher serviceDispatcher)
            where TChannel : class, IChannel;
        public override bool CanBuildServiceDispatcher<TChannel>(BindingContext context)
        {
            if (context == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(context));
            }

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
                (SupportsDuplex && (typeof(TChannel) == typeof(IDuplexChannel) || typeof(TChannel) == typeof(IDuplexSessionChannel))) ||
                (SupportsRequestReply && (typeof(TChannel) == typeof(IReplyChannel) || typeof(TChannel) == typeof(IReplySessionChannel)));
        }


        public virtual void SetKeyDerivation(bool requireDerivedKeys)
        {
            EndpointSupportingTokenParameters.SetKeyDerivation(requireDerivedKeys);
            OptionalEndpointSupportingTokenParameters.SetKeyDerivation(requireDerivedKeys);
            foreach (SupportingTokenParameters t in OperationSupportingTokenParameters.Values)
            {
                t.SetKeyDerivation(requireDerivedKeys);
            }

            foreach (SupportingTokenParameters t in OptionalOperationSupportingTokenParameters.Values)
            {
                t.SetKeyDerivation(requireDerivedKeys);
            }
        }

        internal ChannelProtectionRequirements GetProtectionRequirements(AddressingVersion addressing, ProtectionLevel defaultProtectionLevel)
        {
            if (addressing == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(addressing));
            }

            ChannelProtectionRequirements result = new ChannelProtectionRequirements();
            ProtectionLevel supportedRequestProtectionLevel = GetIndividualProperty<ISecurityCapabilities>().SupportedRequestProtectionLevel;
            ProtectionLevel supportedResponseProtectionLevel = GetIndividualProperty<ISecurityCapabilities>().SupportedResponseProtectionLevel;

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
            if (context == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(context));
            }
            if (typeof(T) == typeof(ISecurityCapabilities))
            {
                return (T)(object)GetSecurityCapabilities(context);
            }
            else
            {
                return context.GetInnerProperty<T>();
            }
        }

        internal abstract ISecurityCapabilities GetIndividualISecurityCapabilities();

        private ISecurityCapabilities GetSecurityCapabilities(BindingContext context)
        {
            ISecurityCapabilities thisSecurityCapability = GetIndividualISecurityCapabilities();
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

        private void SetIssuerBindingContextIfRequired(BindingContext issuerBindingContext)
        {
            SetIssuerBindingContextIfRequired(EndpointSupportingTokenParameters, issuerBindingContext);
            SetIssuerBindingContextIfRequired(OptionalEndpointSupportingTokenParameters, issuerBindingContext);
            foreach (SupportingTokenParameters parameters in OperationSupportingTokenParameters.Values)
            {
                SetIssuerBindingContextIfRequired(parameters, issuerBindingContext);
            }
            foreach (SupportingTokenParameters parameters in OptionalOperationSupportingTokenParameters.Values)
            {
                SetIssuerBindingContextIfRequired(parameters, issuerBindingContext);
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

        private static void SetIssuerBindingContextIfRequired(SupportingTokenParameters supportingParameters, BindingContext issuerBindingContext)
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
        public static TransportSecurityBindingElement CreateUserNameOverTransportBindingElement()
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
        public static SecurityBindingElement CreateSecureConversationBindingElement(SecurityBindingElement bootstrapSecurity)
        {
            return CreateSecureConversationBindingElement(bootstrapSecurity, SecureConversationSecurityTokenParameters.defaultRequireCancellation, null);
        }

        public static SecurityBindingElement CreateSecureConversationBindingElement(SecurityBindingElement bootstrapSecurity, bool requireCancellation)
        {
            return CreateSecureConversationBindingElement(bootstrapSecurity, requireCancellation, null);
        }

        // If any changes are made to this method, please make sure that they are
        // reflected in the corresponding IsCertificateOverTransportBinding() method.
        public static TransportSecurityBindingElement CreateCertificateOverTransportBindingElement()
        {
            return CreateCertificateOverTransportBindingElement(MessageSecurityVersion.Default);
        }

        // If any changes are made to this method, please make sure that they are
        // reflected in the corresponding IsCertificateOverTransportBinding() method.
        public static TransportSecurityBindingElement CreateCertificateOverTransportBindingElement(MessageSecurityVersion version)
        {
            if (version == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(version));
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
            {
                return false;
            }

            if (!(sbe is TransportSecurityBindingElement))
            {
                return false;
            }

            SupportingTokenParameters parameters = sbe.EndpointSupportingTokenParameters;
            if (parameters.Signed.Count != 0 || parameters.SignedEncrypted.Count != 0 || parameters.Endorsing.Count != 1 || parameters.SignedEndorsing.Count != 0)
            {
                return false;
            }

            if (!(parameters.Endorsing[0] is X509SecurityTokenParameters x509Parameters))
            {
                return false;
            }

            if (x509Parameters.InclusionMode != SecurityTokenInclusionMode.AlwaysToRecipient)
            {
                return false;
            }

            return x509Parameters.X509ReferenceStyle == X509KeyIdentifierClauseType.Any || x509Parameters.X509ReferenceStyle == X509KeyIdentifierClauseType.Thumbprint;
        }


        // If any changes are made to this method, please make sure that they are
        // reflected in the corresponding IsSecureConversationBinding() method.
        public static SecurityBindingElement CreateSecureConversationBindingElement(SecurityBindingElement bootstrapSecurity, bool requireCancellation, ChannelProtectionRequirements bootstrapProtectionRequirements)
        {
            if (bootstrapSecurity == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("bootstrapBinding");
            }

            SecurityBindingElement result;

            if (bootstrapSecurity is TransportSecurityBindingElement)
            {
                // there is no need to do replay detection or key derivation for transport bindings
                TransportSecurityBindingElement primary = new TransportSecurityBindingElement();
                SecureConversationSecurityTokenParameters scParameters = new SecureConversationSecurityTokenParameters(
                        bootstrapSecurity,
                        requireCancellation,
                        bootstrapProtectionRequirements)
                {
                    RequireDerivedKeys = false
                };
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
                        bootstrapProtectionRequirements))
                {
                    // there is no need for signature confirmation on the steady state binding
                    RequireSignatureConfirmation = false
                };
                result = primary;
            }
            return result;
        }

        // If any changes are made to this method, please make sure that they are
        // reflected in the corresponding IsSspiNegotiationOverTransportBinding() method.
        public static TransportSecurityBindingElement CreateSspiNegotiationOverTransportBindingElement(bool requireCancellation)
        {
            TransportSecurityBindingElement result = new TransportSecurityBindingElement();
            SspiSecurityTokenParameters sspiParameters = new SspiSecurityTokenParameters(requireCancellation)
            {
                RequireDerivedKeys = false
            };
            result.EndpointSupportingTokenParameters.Endorsing.Add(
                sspiParameters);
            result.IncludeTimestamp = true;
           // result.LocalClientSettings.DetectReplays = false;
            result.LocalServiceSettings.DetectReplays = false;
            result.SupportsExtendedProtectionPolicy = true;

            return result;
        }

        //TODO other security mode
    }
}
