// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Description;
using System.Xml;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Serialization;
using CoreWCF;
using CoreWCF.Security;
using CoreWCF.Security.Tokens;
using System.Net.Security;
using System.Text;
using CoreWCF.Configuration;

namespace CoreWCF.Channels
{
    public sealed class AsymmetricSecurityBindingElement : SecurityBindingElement, IPolicyExportExtension
    {
        public override BindingElement Clone() => throw new NotImplementedException();
        public void ExportPolicy(MetadataExporter exporter, PolicyConversionContext context) => throw new NotImplementedException();

        //        internal const bool defaultAllowSerializedSigningTokenOnReply = false;
        //        private bool allowSerializedSigningTokenOnReply;
        //        private SecurityTokenParameters initiatorTokenParameters;
        //        private MessageProtectionOrder messageProtectionOrder;
        //        private SecurityTokenParameters recipientTokenParameters;
        //        private bool requireSignatureConfirmation;
        //        private bool isCertificateSignatureBinding;

        //        private AsymmetricSecurityBindingElement(AsymmetricSecurityBindingElement elementToBeCloned)
        //            : base(elementToBeCloned)
        //        {
        //            if (elementToBeCloned.initiatorTokenParameters != null)
        //                initiatorTokenParameters = (SecurityTokenParameters)elementToBeCloned.initiatorTokenParameters.Clone();
        //            messageProtectionOrder = elementToBeCloned.messageProtectionOrder;
        //            if (elementToBeCloned.recipientTokenParameters != null)
        //                recipientTokenParameters = (SecurityTokenParameters)elementToBeCloned.recipientTokenParameters.Clone();
        //            requireSignatureConfirmation = elementToBeCloned.requireSignatureConfirmation;
        //            allowSerializedSigningTokenOnReply = elementToBeCloned.allowSerializedSigningTokenOnReply;
        //            isCertificateSignatureBinding = elementToBeCloned.isCertificateSignatureBinding;
        //        }

        //        public AsymmetricSecurityBindingElement()
        //            : this(null, null)
        //        {
        //            // empty
        //        }

        //        public AsymmetricSecurityBindingElement(SecurityTokenParameters recipientTokenParameters)
        //            : this(recipientTokenParameters, null)
        //        {
        //            // empty
        //        }

        //        public AsymmetricSecurityBindingElement(SecurityTokenParameters recipientTokenParameters, SecurityTokenParameters initiatorTokenParameters)
        //            : this(recipientTokenParameters, initiatorTokenParameters, AsymmetricSecurityBindingElement.defaultAllowSerializedSigningTokenOnReply)
        //        {
        //            // empty
        //        }

        //        internal AsymmetricSecurityBindingElement(
        //            SecurityTokenParameters recipientTokenParameters,
        //            SecurityTokenParameters initiatorTokenParameters,
        //            bool allowSerializedSigningTokenOnReply)
        //            : base()
        //        {
        //            messageProtectionOrder = SecurityBindingElement.defaultMessageProtectionOrder;
        //            requireSignatureConfirmation = SecurityBindingElement.defaultRequireSignatureConfirmation;
        //            this.initiatorTokenParameters = initiatorTokenParameters;
        //            this.recipientTokenParameters = recipientTokenParameters;
        //            this.allowSerializedSigningTokenOnReply = allowSerializedSigningTokenOnReply;
        //            isCertificateSignatureBinding = false;
        //        }

        //        public bool AllowSerializedSigningTokenOnReply
        //        {
        //            get
        //            {
        //                return allowSerializedSigningTokenOnReply;
        //            }
        //            set
        //            {
        //                allowSerializedSigningTokenOnReply = value;
        //            }
        //        }

        //        public SecurityTokenParameters InitiatorTokenParameters
        //        {
        //            get
        //            {
        //                return initiatorTokenParameters;
        //            }
        //            set
        //            {
        //                initiatorTokenParameters = value;
        //            }
        //        }

        //        public MessageProtectionOrder MessageProtectionOrder
        //        {
        //            get
        //            {
        //                return messageProtectionOrder;
        //            }
        //            set
        //            {
        //                if (!MessageProtectionOrderHelper.IsDefined(value))
        //                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value"));
        //                messageProtectionOrder = value;
        //            }
        //        }

        //        public SecurityTokenParameters RecipientTokenParameters
        //        {
        //            get
        //            {
        //                return recipientTokenParameters;
        //            }
        //            set
        //            {
        //                recipientTokenParameters = value;
        //            }
        //        }

        //        public bool RequireSignatureConfirmation
        //        {
        //            get
        //            {
        //                return requireSignatureConfirmation;
        //            }
        //            set
        //            {
        //                requireSignatureConfirmation = value;
        //            }
        //        }

        //        internal override ISecurityCapabilities GetIndividualISecurityCapabilities()
        //        {
        //            ProtectionLevel requestProtectionLevel = ProtectionLevel.EncryptAndSign;
        //            ProtectionLevel responseProtectionLevel = ProtectionLevel.EncryptAndSign;
        //            bool supportsServerAuthentication = false;

        //            if (IsCertificateSignatureBinding)
        //            {
        //                requestProtectionLevel = ProtectionLevel.Sign;
        //                responseProtectionLevel = ProtectionLevel.None;
        //            }
        //            else if (RecipientTokenParameters != null)
        //            {
        //                supportsServerAuthentication = RecipientTokenParameters.SupportsServerAuthentication;
        //            }

        //            bool supportsClientAuthentication;
        //            bool supportsClientWindowsIdentity;
        //            GetSupportingTokensCapabilities(out supportsClientAuthentication, out supportsClientWindowsIdentity);
        //            if (InitiatorTokenParameters != null)
        //            {
        //                supportsClientAuthentication = supportsClientAuthentication || InitiatorTokenParameters.SupportsClientAuthentication;
        //                supportsClientWindowsIdentity = supportsClientWindowsIdentity || InitiatorTokenParameters.SupportsClientWindowsIdentity;
        //            }

        //            return new SecurityCapabilities(supportsClientAuthentication, supportsServerAuthentication, supportsClientWindowsIdentity,
        //                requestProtectionLevel, responseProtectionLevel);
        //        }

        //        internal override bool SupportsDuplex
        //        {
        //            get { return !isCertificateSignatureBinding; }
        //        }

        //        internal override bool SupportsRequestReply
        //        {
        //            get
        //            {
        //                return !isCertificateSignatureBinding;
        //            }
        //        }

        //        internal bool IsCertificateSignatureBinding
        //        {
        //            get { return isCertificateSignatureBinding; }
        //            set { isCertificateSignatureBinding = value; }
        //        }

        //        public override void SetKeyDerivation(bool requireDerivedKeys)
        //        {
        //            base.SetKeyDerivation(requireDerivedKeys);
        //            if (initiatorTokenParameters != null)
        //                initiatorTokenParameters.RequireDerivedKeys = requireDerivedKeys;
        //            if (recipientTokenParameters != null)
        //                recipientTokenParameters.RequireDerivedKeys = requireDerivedKeys;
        //        }

        //        internal override bool IsSetKeyDerivation(bool requireDerivedKeys)
        //        {
        //            if (!base.IsSetKeyDerivation(requireDerivedKeys))
        //                return false;
        //            if (initiatorTokenParameters != null && initiatorTokenParameters.RequireDerivedKeys != requireDerivedKeys)
        //                return false;
        //            if (recipientTokenParameters != null && recipientTokenParameters.RequireDerivedKeys != requireDerivedKeys)
        //                return false;
        //            return true;
        //        }

        //        private bool HasProtectionRequirements(ScopedMessagePartSpecification scopedParts)
        //        {
        //            foreach (string action in scopedParts.Actions)
        //            {
        //                MessagePartSpecification parts;
        //                if (scopedParts.TryGetParts(action, out parts))
        //                {
        //                    if (!parts.IsEmpty())
        //                    {
        //                        return true;
        //                    }
        //                }
        //            }
        //            return false;
        //        }

        //        internal override SecurityProtocolFactory CreateSecurityProtocolFactory<TChannel>(BindingContext context, SecurityCredentialsManager credentialsManager, bool isForService, BindingContext issuerBindingContext)
        //        {
        //            if (context == null)
        //                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("context");
        //            if (credentialsManager == null)
        //                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("credentialsManager");

        //            if (InitiatorTokenParameters == null)
        //                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.AsymmetricSecurityBindingElementNeedsInitiatorTokenParameters, ToString())));
        //            if (RecipientTokenParameters == null)
        //                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.AsymmetricSecurityBindingElementNeedsRecipientTokenParameters, ToString())));

        //            bool isDuplexSecurity = !isCertificateSignatureBinding && (typeof(IDuplexChannel) == typeof(TChannel) || typeof(IDuplexSessionChannel) == typeof(TChannel));

        //            SecurityProtocolFactory protocolFactory;

        //            AsymmetricSecurityProtocolFactory forward = new AsymmetricSecurityProtocolFactory();
        //            forward.ProtectionRequirements.Add(SecurityBindingElement.ComputeProtectionRequirements(this, context.BindingParameters, context.Binding.Elements, isForService));
        //            forward.RequireConfidentiality = this.HasProtectionRequirements(forward.ProtectionRequirements.IncomingEncryptionParts);
        //            forward.RequireIntegrity = this.HasProtectionRequirements(forward.ProtectionRequirements.IncomingSignatureParts);
        //            if (isCertificateSignatureBinding)
        //            {
        //                if (isForService)
        //                {
        //                    forward.ApplyIntegrity = forward.ApplyConfidentiality = false;
        //                }
        //                else
        //                {
        //                    forward.ApplyConfidentiality = forward.RequireIntegrity = false;
        //                }
        //            }
        //            else
        //            {
        //                forward.ApplyIntegrity = this.HasProtectionRequirements(forward.ProtectionRequirements.OutgoingSignatureParts);
        //                forward.ApplyConfidentiality = this.HasProtectionRequirements(forward.ProtectionRequirements.OutgoingEncryptionParts);
        //            }
        //            if (isForService)
        //            {
        //                //base.ApplyAuditBehaviorSettings(context, forward);
        //                if (forward.RequireConfidentiality || (!isCertificateSignatureBinding && forward.ApplyIntegrity))
        //                {
        //                    forward.AsymmetricTokenParameters = (SecurityTokenParameters)RecipientTokenParameters.Clone();
        //                }
        //                else
        //                {
        //                    forward.AsymmetricTokenParameters = null;
        //                }
        //                forward.CryptoTokenParameters = InitiatorTokenParameters.Clone();
        //                SetIssuerBindingContextIfRequired(forward.CryptoTokenParameters, issuerBindingContext);
        //            }
        //            else
        //            {
        //               /* if (forward.ApplyConfidentiality || (!isCertificateSignatureBinding && forward.RequireIntegrity))
        //                {
        //                    forward.AsymmetricTokenParameters = (SecurityTokenParameters)RecipientTokenParameters.Clone();
        //                }
        //                else
        //                {
        //                    forward.AsymmetricTokenParameters = null;
        //                }
        //                forward.CryptoTokenParameters = InitiatorTokenParameters.Clone();
        //                SetIssuerBindingContextIfRequired(forward.CryptoTokenParameters, issuerBindingContext);*/
        //            }
        //            if (isDuplexSecurity)
        //            {
        //                if (isForService)
        //                {
        //                    forward.ApplyConfidentiality = forward.ApplyIntegrity = false;
        //                }
        //                else
        //                {
        //                    forward.RequireIntegrity = forward.RequireConfidentiality = false;
        //                }
        //            }
        //            else
        //            {
        //                if (!isForService)
        //                {
        //                    forward.AllowSerializedSigningTokenOnReply = AllowSerializedSigningTokenOnReply;
        //                }
        //            }

        //            //forward.IdentityVerifier = this.LocalClientSettings.IdentityVerifier;
        //            forward.DoRequestSignatureConfirmation = RequireSignatureConfirmation;
        //            forward.MessageProtectionOrder = MessageProtectionOrder;
        //            base.ConfigureProtocolFactory(forward, credentialsManager, isForService, issuerBindingContext, context.Binding);
        //            if (!forward.RequireIntegrity)
        //                forward.DetectReplays = false;

        //            if (isDuplexSecurity)
        //            {
        //                AsymmetricSecurityProtocolFactory reverse = new AsymmetricSecurityProtocolFactory();
        //                if (isForService)
        //                {
        //                    reverse.AsymmetricTokenParameters = InitiatorTokenParameters.Clone();
        //                    reverse.AsymmetricTokenParameters.ReferenceStyle = SecurityTokenReferenceStyle.External;
        //                    reverse.AsymmetricTokenParameters.InclusionMode = SecurityTokenInclusionMode.Never;
        //                    reverse.CryptoTokenParameters = (SecurityTokenParameters)RecipientTokenParameters.Clone();
        //                    reverse.CryptoTokenParameters.ReferenceStyle = SecurityTokenReferenceStyle.Internal;
        //                    reverse.CryptoTokenParameters.InclusionMode = SecurityTokenInclusionMode.AlwaysToRecipient;
        //                    reverse.IdentityVerifier = null;
        //                }
        //                else
        //                {
        //                    reverse.AsymmetricTokenParameters = InitiatorTokenParameters.Clone();
        //                    reverse.AsymmetricTokenParameters.ReferenceStyle = SecurityTokenReferenceStyle.External;
        //                    reverse.AsymmetricTokenParameters.InclusionMode = SecurityTokenInclusionMode.Never;
        //                    reverse.CryptoTokenParameters = (SecurityTokenParameters)RecipientTokenParameters.Clone();
        //                    reverse.CryptoTokenParameters.ReferenceStyle = SecurityTokenReferenceStyle.Internal;
        //                    reverse.CryptoTokenParameters.InclusionMode = SecurityTokenInclusionMode.AlwaysToRecipient;
        //                   // reverse.IdentityVerifier = this.LocalClientSettings.IdentityVerifier;
        //                }
        //                reverse.DoRequestSignatureConfirmation = RequireSignatureConfirmation;
        //                reverse.MessageProtectionOrder = MessageProtectionOrder;
        //                reverse.ProtectionRequirements.Add(SecurityBindingElement.ComputeProtectionRequirements(this, context.BindingParameters, context.Binding.Elements, isForService));
        //                if (isForService)
        //                {
        //                    reverse.ApplyConfidentiality = this.HasProtectionRequirements(reverse.ProtectionRequirements.OutgoingEncryptionParts);
        //                    reverse.ApplyIntegrity = true;
        //                    reverse.RequireIntegrity = reverse.RequireConfidentiality = false;
        //                }
        //                else
        //                {
        //                    reverse.RequireConfidentiality = this.HasProtectionRequirements(reverse.ProtectionRequirements.IncomingEncryptionParts);
        //                    reverse.RequireIntegrity = true;
        //                    reverse.ApplyIntegrity = reverse.ApplyConfidentiality = false;
        //                }
        //                base.ConfigureProtocolFactory(reverse, credentialsManager, !isForService, issuerBindingContext, context.Binding);
        //                if (!reverse.RequireIntegrity)
        //                    reverse.DetectReplays = false;

        //                // setup reverse here
        //                reverse.IsDuplexReply = true;

        //                DuplexSecurityProtocolFactory duplex = new DuplexSecurityProtocolFactory();
        //                duplex.ForwardProtocolFactory = forward;
        //                duplex.ReverseProtocolFactory = reverse;
        //                protocolFactory = duplex;
        //            }
        //            else
        //            {
        //                protocolFactory = forward;
        //            }

        //            return protocolFactory;
        //        }

        //        internal override bool RequiresChannelDemuxer()
        //        {
        //            return (base.RequiresChannelDemuxer() || RequiresChannelDemuxer(InitiatorTokenParameters));
        //        }

        //        protected override IChannelFactory<TChannel> BuildChannelFactoryCore<TChannel>(BindingContext context)
        //        {
        //            ISecurityCapabilities securityCapabilities = GetProperty<ISecurityCapabilities>(context);
        //            bool requireDemuxer = RequiresChannelDemuxer();
        //            ChannelBuilder channelBuilder = new ChannelBuilder(context, requireDemuxer);
        //            if (requireDemuxer)
        //            {
        //                ApplyPropertiesOnDemuxer(channelBuilder, context);
        //            }

        //            BindingContext issuerBindingContext = context.Clone();
        //            SecurityCredentialsManager credentialsManager = context.BindingParameters.Find<SecurityCredentialsManager>();
        //            if (credentialsManager == null)
        //            {
        //                credentialsManager = ClientCredentials.CreateDefaultCredentials();
        //            }

        //            SecurityProtocolFactory protocolFactory =
        //                CreateSecurityProtocolFactory<TChannel>(context, credentialsManager, false, issuerBindingContext);

        //            return new SecurityChannelFactory<TChannel>(securityCapabilities, context, channelBuilder, protocolFactory);
        //        }

        //        protected override IChannelListener<TChannel> BuildChannelListenerCore<TChannel>(BindingContext context)
        //        {
        //            bool requireDemuxer = RequiresChannelDemuxer();
        //            ChannelBuilder channelBuilder = new ChannelBuilder(context, requireDemuxer);
        //            if (requireDemuxer)
        //            {
        //                ApplyPropertiesOnDemuxer(channelBuilder, context);
        //            }
        //            BindingContext issuerBindingContext = context.Clone();

        //            SecurityChannelListener<TChannel> channelListener = new SecurityChannelListener<TChannel>(this, context);
        //            SecurityCredentialsManager credentialsManager = context.BindingParameters.Find<SecurityCredentialsManager>();
        //            if (credentialsManager == null)
        //                credentialsManager = ServiceCredentials.CreateDefaultCredentials();

        //            SecurityProtocolFactory protocolFactory = CreateSecurityProtocolFactory<TChannel>(context, credentialsManager, true, issuerBindingContext);
        //            channelListener.SecurityProtocolFactory = protocolFactory;
        //            channelListener.InitializeListener(channelBuilder);

        //            return channelListener;
        //        }

        //        public override T GetProperty<T>(BindingContext context)
        //        {
        //            if (context == null)
        //                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("context");

        //            if (typeof(T) == typeof(ChannelProtectionRequirements))
        //            {
        //                AddressingVersion addressing = MessageVersion.Default.Addressing;
        //#pragma warning suppress 56506
        //                MessageEncodingBindingElement encoding = context.Binding.Elements.Find<MessageEncodingBindingElement>();
        //                if (encoding != null)
        //                {
        //                    addressing = encoding.MessageVersion.Addressing;
        //                }

        //                ChannelProtectionRequirements myRequirements = base.GetProtectionRequirements(addressing, GetIndividualProperty<ISecurityCapabilities>().SupportedRequestProtectionLevel);
        //                myRequirements.Add(context.GetInnerProperty<ChannelProtectionRequirements>() ?? new ChannelProtectionRequirements());
        //                return (T)(object)myRequirements;
        //            }
        //            else
        //            {
        //                return base.GetProperty<T>(context);
        //            }
        //        }

        //        public override string ToString()
        //        {
        //            StringBuilder sb = new StringBuilder();
        //            sb.AppendLine(base.ToString());

        //            sb.AppendLine(String.Format(CultureInfo.InvariantCulture, "MessageProtectionOrder: {0}", messageProtectionOrder.ToString()));
        //            sb.AppendLine(String.Format(CultureInfo.InvariantCulture, "RequireSignatureConfirmation: {0}", requireSignatureConfirmation.ToString()));
        //            sb.Append("InitiatorTokenParameters: ");
        //            if (initiatorTokenParameters != null)
        //                sb.AppendLine(initiatorTokenParameters.ToString().Trim().Replace("\n", "\n  "));
        //            else
        //                sb.AppendLine("null");
        //            sb.Append("RecipientTokenParameters: ");
        //            if (recipientTokenParameters != null)
        //                sb.AppendLine(recipientTokenParameters.ToString().Trim().Replace("\n", "\n  "));
        //            else
        //                sb.AppendLine("null");

        //            return sb.ToString().Trim();
        //        }

        //        public override BindingElement Clone()
        //        {
        //            return new AsymmetricSecurityBindingElement(this);
        //        }

        //        void IPolicyExportExtension.ExportPolicy(MetadataExporter exporter, PolicyConversionContext context)
        //        {
        //            SecurityBindingElement.ExportPolicy(exporter, context);
        //        }

        protected override IServiceDispatcher BuildServiceDispatcherCore<TChannel>(BindingContext context, IServiceDispatcher serviceDispatcher) => throw new NotImplementedException();
        internal override SecurityProtocolFactory CreateSecurityProtocolFactory<TChannel>(BindingContext context, SecurityCredentialsManager credentialsManager, bool isForService, BindingContext issuanceBindingContext) => throw new NotImplementedException();
        internal override ISecurityCapabilities GetIndividualISecurityCapabilities() => throw new NotImplementedException();
    }
}
