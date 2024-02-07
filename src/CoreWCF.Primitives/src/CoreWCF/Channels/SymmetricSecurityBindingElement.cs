// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.Net.Security;
using System.Text;
using CoreWCF.Configuration;
using CoreWCF.Description;
using CoreWCF.Dispatcher;
using CoreWCF.Security;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Channels
{
    public sealed class SymmetricSecurityBindingElement : SecurityBindingElement //, IPolicyExportExtension
    {
        private MessageProtectionOrder _messageProtectionOrder;

        private SymmetricSecurityBindingElement(SymmetricSecurityBindingElement elementToBeCloned)
            : base(elementToBeCloned)
        {
            _messageProtectionOrder = elementToBeCloned._messageProtectionOrder;
            if (elementToBeCloned.ProtectionTokenParameters != null)
            {
                ProtectionTokenParameters = (SecurityTokenParameters)elementToBeCloned.ProtectionTokenParameters.Clone();
            }

            RequireSignatureConfirmation = elementToBeCloned.RequireSignatureConfirmation;
        }

        public SymmetricSecurityBindingElement()
            : this((SecurityTokenParameters)null)
        {
            // empty
        }

        public SymmetricSecurityBindingElement(SecurityTokenParameters protectionTokenParameters)
            : base()
        {
            _messageProtectionOrder = DefaultMessageProtectionOrder;
            RequireSignatureConfirmation = DefaultRequireSignatureConfirmation;
            ProtectionTokenParameters = protectionTokenParameters;
        }

        public bool RequireSignatureConfirmation { get; set; }

        public MessageProtectionOrder MessageProtectionOrder
        {
            get
            {
                return _messageProtectionOrder;
            }
            set
            {
                if (!MessageProtectionOrderHelper.IsDefined(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                }

                _messageProtectionOrder = value;
            }
        }

        public SecurityTokenParameters ProtectionTokenParameters { get; set; }

        internal override ISecurityCapabilities GetIndividualISecurityCapabilities()
        {
            bool supportsServerAuthentication = false;
            GetSupportingTokensCapabilities(out bool supportsClientAuthentication, out bool supportsClientWindowsIdentity);
            if (ProtectionTokenParameters != null)
            {
                supportsClientAuthentication = supportsClientAuthentication || ProtectionTokenParameters.SupportsClientAuthentication;
                supportsClientWindowsIdentity = supportsClientWindowsIdentity || ProtectionTokenParameters.SupportsClientWindowsIdentity;

                if (ProtectionTokenParameters.HasAsymmetricKey)
                {
                    supportsServerAuthentication = ProtectionTokenParameters.SupportsClientAuthentication;
                }
                else
                {
                    supportsServerAuthentication = ProtectionTokenParameters.SupportsServerAuthentication;
                }
            }

            return new SecurityCapabilities(supportsClientAuthentication, supportsServerAuthentication, supportsClientWindowsIdentity,
                ProtectionLevel.EncryptAndSign, ProtectionLevel.EncryptAndSign);
        }

        internal override bool SessionMode
        {
            get
            {
                if (ProtectionTokenParameters is SecureConversationSecurityTokenParameters secureConversationTokenParameters)
                {
                    return secureConversationTokenParameters.RequireCancellation;
                }
                else
                {
                    return false;
                }
            }
        }

        internal override bool SupportsDuplex
        {
            get { return SessionMode; }
        }

        internal override bool SupportsRequestReply
        {
            get { return true; }
        }

        public override void SetKeyDerivation(bool requireDerivedKeys)
        {
            base.SetKeyDerivation(requireDerivedKeys);
            if (ProtectionTokenParameters != null)
            {
                ProtectionTokenParameters.RequireDerivedKeys = requireDerivedKeys;
            }
        }

        public override T GetProperty<T>(BindingContext context)
        {
            if (context == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(context));
            }

            if (typeof(T) == typeof(ChannelProtectionRequirements))
            {
                AddressingVersion addressing = MessageVersion.Default.Addressing;
                MessageEncodingBindingElement encoding = context.Binding.Elements.Find<MessageEncodingBindingElement>();
                if (encoding != null)
                {
                    addressing = encoding.MessageVersion.Addressing;
                }
                ChannelProtectionRequirements myRequirements = GetProtectionRequirements(addressing, ProtectionLevel.EncryptAndSign);
                myRequirements.Add(context.GetInnerProperty<ChannelProtectionRequirements>() ?? new ChannelProtectionRequirements());
                return (T)(object)myRequirements;
            }
            else
            {
                return base.GetProperty<T>(context);
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(base.ToString());

            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "MessageProtectionOrder: {0}", _messageProtectionOrder.ToString()));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "RequireSignatureConfirmation: {0}", RequireSignatureConfirmation.ToString()));
            sb.Append("ProtectionTokenParameters: ");
            if (ProtectionTokenParameters != null)
            {
                sb.AppendLine(ProtectionTokenParameters.ToString().Trim().Replace("\n", "\n  "));
            }
            else
            {
                sb.AppendLine("null");
            }

            return sb.ToString().Trim();
        }

        public override BindingElement Clone()
        {
            return new SymmetricSecurityBindingElement(this);
        }

        internal override SecurityProtocolFactory CreateSecurityProtocolFactory<TChannel>(BindingContext context, SecurityCredentialsManager credentialsManager, bool isForService, BindingContext issuerBindingContext)
        {
            if (context == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("context");
            if (credentialsManager == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("credentialsManager");

            if (ProtectionTokenParameters == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SymmetricSecurityBindingElementNeedsProtectionTokenParameters, ToString())));
            }

            SymmetricSecurityProtocolFactory protocolFactory = new SymmetricSecurityProtocolFactory();
            if (isForService)
            {
               // base.ApplyAuditBehaviorSettings(context, protocolFactory);
            }
            protocolFactory.SecurityTokenParameters = (SecurityTokenParameters)ProtectionTokenParameters.Clone();
            SetIssuerBindingContextIfRequired(protocolFactory.SecurityTokenParameters, issuerBindingContext);
            protocolFactory.ApplyConfidentiality = true;
            protocolFactory.RequireConfidentiality = true;
            protocolFactory.ApplyIntegrity = true;
            protocolFactory.RequireIntegrity = true;
           // protocolFactory.IdentityVerifier = this.LocalClientSettings.IdentityVerifier;
            protocolFactory.DoRequestSignatureConfirmation = RequireSignatureConfirmation;
            protocolFactory.MessageProtectionOrder = MessageProtectionOrder;
            protocolFactory.ProtectionRequirements.Add(SecurityBindingElement.ComputeProtectionRequirements(this, context.BindingParameters, context.Binding.Elements, isForService));
            base.ConfigureProtocolFactory(protocolFactory, credentialsManager, isForService, issuerBindingContext, context.Binding);

            return protocolFactory;
        }

        internal override bool RequiresChannelDemuxer() => base.RequiresChannelDemuxer() || RequiresChannelDemuxer(ProtectionTokenParameters);

        protected override IServiceDispatcher BuildServiceDispatcherCore<TChannel>(BindingContext context, IServiceDispatcher serviceDispatcher)
        {
            SecurityServiceDispatcher securityServiceDispatcher = new SecurityServiceDispatcher(context, serviceDispatcher);
            SecurityCredentialsManager credentialsManager = serviceDispatcher.Host.Description.Behaviors.Find<SecurityCredentialsManager>();
            if (credentialsManager == null)
                credentialsManager = ServiceCredentials.CreateDefaultCredentials();

            // This adds the demuxer element to the context. We add a demuxer element only if the binding is configured to do
            // secure conversation or negotiation

            bool requireDemuxer = RequiresChannelDemuxer();
            ChannelBuilder channelBuilder = new ChannelBuilder(context, requireDemuxer);
            if (requireDemuxer)
            {
                ApplyPropertiesOnDemuxer(channelBuilder, context);
            }

            BindingContext issuerBindingContext = context.Clone();

            if (ProtectionTokenParameters is SecureConversationSecurityTokenParameters)
            {
                SecureConversationSecurityTokenParameters scParameters = (SecureConversationSecurityTokenParameters)ProtectionTokenParameters;
                if (scParameters.BootstrapSecurityBindingElement == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SecureConversationSecurityTokenParametersRequireBootstrapBinding)));

                BindingContext scIssuerBindingContext = issuerBindingContext.Clone();
                scIssuerBindingContext.BindingParameters.Remove<ChannelProtectionRequirements>();
                scIssuerBindingContext.BindingParameters.Add(scParameters.BootstrapProtectionRequirements);
                scIssuerBindingContext.BindingParameters.Add(credentialsManager);
                IMessageFilterTable<EndpointAddress> endpointFilterTable = context.BindingParameters.Find<IMessageFilterTable<EndpointAddress>>();

                AddDemuxerForSecureConversation(channelBuilder, scIssuerBindingContext);

                if (scParameters.RequireCancellation)
                {
                    SessionSymmetricMessageSecurityProtocolFactory sessionFactory = new SessionSymmetricMessageSecurityProtocolFactory();
                   // base.ApplyAuditBehaviorSettings(context, sessionFactory);
                    sessionFactory.SecurityTokenParameters = scParameters.Clone();
                    ((SecureConversationSecurityTokenParameters)sessionFactory.SecurityTokenParameters).IssuerBindingContext = scIssuerBindingContext;
                    sessionFactory.ApplyConfidentiality = true;
                    sessionFactory.RequireConfidentiality = true;
                    sessionFactory.ApplyIntegrity = true;
                    sessionFactory.RequireIntegrity = true;
                    ///sessionFactory.IdentityVerifier = this.LocalClientSettings.IdentityVerifier;
                    sessionFactory.DoRequestSignatureConfirmation = RequireSignatureConfirmation;
                    sessionFactory.MessageProtectionOrder = MessageProtectionOrder;
                   // sessionFactory.IdentityVerifier = this.LocalClientSettings.IdentityVerifier;
                    sessionFactory.ProtectionRequirements.Add(SecurityBindingElement.ComputeProtectionRequirements(this, context.BindingParameters, context.Binding.Elements, true));
                    base.ConfigureProtocolFactory(sessionFactory, credentialsManager, true, issuerBindingContext, context.Binding);

                    securityServiceDispatcher.SessionMode = true;
                    securityServiceDispatcher.SessionServerSettings.InactivityTimeout = LocalServiceSettings.InactivityTimeout;
                    securityServiceDispatcher.SessionServerSettings.KeyRolloverInterval = LocalServiceSettings.SessionKeyRolloverInterval;
                    securityServiceDispatcher.SessionServerSettings.MaximumPendingSessions = LocalServiceSettings.MaxPendingSessions;
                    securityServiceDispatcher.SessionServerSettings.MaximumKeyRenewalInterval = LocalServiceSettings.SessionKeyRenewalInterval;
                    securityServiceDispatcher.SessionServerSettings.TolerateTransportFailures = LocalServiceSettings.ReconnectTransportOnFailure;
                    securityServiceDispatcher.SessionServerSettings.CanRenewSession = scParameters.CanRenewSession;
                    securityServiceDispatcher.SessionServerSettings.IssuedSecurityTokenParameters = scParameters.Clone();
                    ((SecureConversationSecurityTokenParameters)securityServiceDispatcher.SessionServerSettings.IssuedSecurityTokenParameters).IssuerBindingContext = scIssuerBindingContext;
                    securityServiceDispatcher.SessionServerSettings.SecurityStandardsManager = sessionFactory.StandardsManager;
                    securityServiceDispatcher.SessionServerSettings.SessionProtocolFactory = sessionFactory;
                    securityServiceDispatcher.SessionServerSettings.SessionProtocolFactory.EndpointFilterTable = endpointFilterTable;

                    // pass in the error handler for handling unknown security sessions - dont do this if the underlying channel is duplex since sending 
                    // back faults in response to badly secured requests over duplex can result in DoS.
                    if (context.BindingParameters != null && context.BindingParameters.Find<IChannelDemuxFailureHandler>() == null
                        && !IsUnderlyingDispatcherDuplex<TChannel>(context))
                    {
                        context.BindingParameters.Add(new SecuritySessionServerSettings.SecuritySessionDemuxFailureHandler(sessionFactory.StandardsManager));
                    }
                }
                else
                {
                    SymmetricSecurityProtocolFactory protocolFactory = new SymmetricSecurityProtocolFactory();
                   // base.ApplyAuditBehaviorSettings(context, protocolFactory);
                    protocolFactory.SecurityTokenParameters = scParameters.Clone();
                    ((SecureConversationSecurityTokenParameters)protocolFactory.SecurityTokenParameters).IssuerBindingContext = scIssuerBindingContext;
                    protocolFactory.ApplyConfidentiality = true;
                    protocolFactory.RequireConfidentiality = true;
                    protocolFactory.ApplyIntegrity = true;
                    protocolFactory.RequireIntegrity = true;
                   // protocolFactory.IdentityVerifier = this.LocalClientSettings.IdentityVerifier;
                    protocolFactory.DoRequestSignatureConfirmation = RequireSignatureConfirmation;
                    protocolFactory.MessageProtectionOrder = MessageProtectionOrder;
                    protocolFactory.ProtectionRequirements.Add(SecurityBindingElement.ComputeProtectionRequirements(this, context.BindingParameters, context.Binding.Elements, true));
                    protocolFactory.EndpointFilterTable = endpointFilterTable;
                    base.ConfigureProtocolFactory(protocolFactory, credentialsManager, true, issuerBindingContext, context.Binding);

                    securityServiceDispatcher.SecurityProtocolFactory = protocolFactory;
                }

            }
            else
            {
                SecurityProtocolFactory protocolFactory = CreateSecurityProtocolFactory<TChannel>(context, credentialsManager, true, issuerBindingContext);
                securityServiceDispatcher.SecurityProtocolFactory = protocolFactory;
            }

            securityServiceDispatcher.InitializeSecurityDispatcher(channelBuilder, typeof(TChannel));
            channelBuilder.BuildServiceDispatcher<TChannel>(context, securityServiceDispatcher);
            return securityServiceDispatcher;
        }
    }
}
