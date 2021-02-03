// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.Text;
using CoreWCF.Channels;
using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;

namespace CoreWCF.Security.Tokens
{
    public class SecureConversationSecurityTokenParameters : SecurityTokenParameters
    {
        internal const bool defaultRequireCancellation = true;
        internal const bool defaultCanRenewSession = true;
        private BindingContext _issuerBindingContext;

        protected SecureConversationSecurityTokenParameters(SecureConversationSecurityTokenParameters other)
            : base(other)
        {
            RequireCancellation = other.RequireCancellation;
            CanRenewSession = other.CanRenewSession;
            if (other.BootstrapSecurityBindingElement != null)
            {
                BootstrapSecurityBindingElement = (SecurityBindingElement)other.BootstrapSecurityBindingElement.Clone();
            }

            if (other.BootstrapProtectionRequirements != null)
            {
                BootstrapProtectionRequirements = new ChannelProtectionRequirements(other.BootstrapProtectionRequirements);
            }

            if (other._issuerBindingContext != null)
            {
                _issuerBindingContext = other._issuerBindingContext.Clone();
            }
        }

        public SecureConversationSecurityTokenParameters()
            : this(null, defaultRequireCancellation, null)
        {
            // empty
        }

        public SecureConversationSecurityTokenParameters(SecurityBindingElement bootstrapSecurityBindingElement)
            : this(bootstrapSecurityBindingElement, defaultRequireCancellation, null)
        {
            // empty
        }

        public SecureConversationSecurityTokenParameters(SecurityBindingElement bootstrapSecurityBindingElement, bool requireCancellation)
            : this(bootstrapSecurityBindingElement, requireCancellation, true)
        {
            // empty
        }

        public SecureConversationSecurityTokenParameters(SecurityBindingElement bootstrapSecurityBindingElement, bool requireCancellation, bool canRenewSession)
            : this(bootstrapSecurityBindingElement, requireCancellation, canRenewSession, null)
        {
            // empty
        }

        public SecureConversationSecurityTokenParameters(SecurityBindingElement bootstrapSecurityBindingElement, bool requireCancellation, ChannelProtectionRequirements bootstrapProtectionRequirements)
            : this(bootstrapSecurityBindingElement, requireCancellation, defaultCanRenewSession, null)
        {
            // empty
        }

        public SecureConversationSecurityTokenParameters(SecurityBindingElement bootstrapSecurityBindingElement, bool requireCancellation, bool canRenewSession, ChannelProtectionRequirements bootstrapProtectionRequirements)
            : base()
        {
            BootstrapSecurityBindingElement = bootstrapSecurityBindingElement;
            CanRenewSession = canRenewSession;
            if (bootstrapProtectionRequirements != null)
            {
                BootstrapProtectionRequirements = new ChannelProtectionRequirements(bootstrapProtectionRequirements);
            }
            else
            {
                BootstrapProtectionRequirements = new ChannelProtectionRequirements();
                BootstrapProtectionRequirements.IncomingEncryptionParts.AddParts(new MessagePartSpecification(true));
                BootstrapProtectionRequirements.IncomingSignatureParts.AddParts(new MessagePartSpecification(true));
                BootstrapProtectionRequirements.OutgoingEncryptionParts.AddParts(new MessagePartSpecification(true));
                BootstrapProtectionRequirements.OutgoingSignatureParts.AddParts(new MessagePartSpecification(true));
            }
            RequireCancellation = requireCancellation;
        }

        protected internal override bool HasAsymmetricKey => false;

        public SecurityBindingElement BootstrapSecurityBindingElement { get; set; }

        public ChannelProtectionRequirements BootstrapProtectionRequirements { get; }

        internal BindingContext IssuerBindingContext
        {
            get
            {
                return _issuerBindingContext;
            }
            set
            {
                if (value != null)
                {
                    value = value.Clone();
                }
                _issuerBindingContext = value;
            }
        }

        private ISecurityCapabilities BootstrapSecurityCapabilities => BootstrapSecurityBindingElement.GetIndividualProperty<ISecurityCapabilities>();

        public bool RequireCancellation { get; set; }

        public bool CanRenewSession { get; set; } = defaultCanRenewSession;

        protected internal override bool SupportsClientAuthentication => BootstrapSecurityCapabilities == null ? false : BootstrapSecurityCapabilities.SupportsClientAuthentication;

        protected internal override bool SupportsServerAuthentication => BootstrapSecurityCapabilities == null ? false : BootstrapSecurityCapabilities.SupportsServerAuthentication;

        protected internal override bool SupportsClientWindowsIdentity => BootstrapSecurityCapabilities == null ? false : BootstrapSecurityCapabilities.SupportsClientWindowsIdentity;

        protected override SecurityTokenParameters CloneCore()
        {
            return new SecureConversationSecurityTokenParameters(this);
        }

        protected internal override SecurityKeyIdentifierClause CreateKeyIdentifierClause(SecurityToken token, SecurityTokenReferenceStyle referenceStyle)
        {
            if (token is GenericXmlSecurityToken)
            {
                return CreateGenericXmlTokenKeyIdentifierClause(token, referenceStyle);
            }
            else
            {
                return CreateKeyIdentifierClause<SecurityContextKeyIdentifierClause, LocalIdKeyIdentifierClause>(token, referenceStyle);
            }
        }

        protected internal override void InitializeSecurityTokenRequirement(SecurityTokenRequirement requirement)
        {
            requirement.TokenType = ServiceModelSecurityTokenTypes.SecureConversation;
            requirement.KeyType = SecurityKeyType.SymmetricKey;
            requirement.RequireCryptographicToken = true;
            requirement.Properties[ServiceModelSecurityTokenRequirement.SupportSecurityContextCancellationProperty] = RequireCancellation;
            requirement.Properties[ServiceModelSecurityTokenRequirement.SecureConversationSecurityBindingElementProperty] = BootstrapSecurityBindingElement;
            requirement.Properties[ServiceModelSecurityTokenRequirement.IssuerBindingContextProperty] = IssuerBindingContext.Clone();
            requirement.Properties[ServiceModelSecurityTokenRequirement.IssuedSecurityTokenParametersProperty] = Clone();
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(base.ToString());

            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "RequireCancellation: {0}", RequireCancellation.ToString()));
            if (BootstrapSecurityBindingElement == null)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "BootstrapSecurityBindingElement: null"));
            }
            else
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "BootstrapSecurityBindingElement:"));
                sb.AppendLine("  " + BootstrapSecurityBindingElement.ToString().Trim().Replace("\n", "\n  "));
            }

            return sb.ToString().Trim();
        }
    }
}
