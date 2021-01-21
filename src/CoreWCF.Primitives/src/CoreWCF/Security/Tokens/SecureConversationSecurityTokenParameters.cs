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
        private SecurityBindingElement bootstrapSecurityBindingElement;
        private readonly ChannelProtectionRequirements bootstrapProtectionRequirements;
        private bool requireCancellation;
        private bool canRenewSession = defaultCanRenewSession;
        private BindingContext issuerBindingContext;

        protected SecureConversationSecurityTokenParameters(SecureConversationSecurityTokenParameters other)
            : base(other)
        {
            requireCancellation = other.requireCancellation;
            canRenewSession = other.canRenewSession;
            if (other.bootstrapSecurityBindingElement != null)
            {
                bootstrapSecurityBindingElement = (SecurityBindingElement)other.bootstrapSecurityBindingElement.Clone();
            }

            if (other.bootstrapProtectionRequirements != null)
            {
                bootstrapProtectionRequirements = new ChannelProtectionRequirements(other.bootstrapProtectionRequirements);
            }

            if (other.issuerBindingContext != null)
            {
                issuerBindingContext = other.issuerBindingContext.Clone();
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
            this.bootstrapSecurityBindingElement = bootstrapSecurityBindingElement;
            this.canRenewSession = canRenewSession;
            if (bootstrapProtectionRequirements != null)
            {
                this.bootstrapProtectionRequirements = new ChannelProtectionRequirements(bootstrapProtectionRequirements);
            }
            else
            {
                this.bootstrapProtectionRequirements = new ChannelProtectionRequirements();
                this.bootstrapProtectionRequirements.IncomingEncryptionParts.AddParts(new MessagePartSpecification(true));
                this.bootstrapProtectionRequirements.IncomingSignatureParts.AddParts(new MessagePartSpecification(true));
                this.bootstrapProtectionRequirements.OutgoingEncryptionParts.AddParts(new MessagePartSpecification(true));
                this.bootstrapProtectionRequirements.OutgoingSignatureParts.AddParts(new MessagePartSpecification(true));
            }
            this.requireCancellation = requireCancellation;
        }

        internal protected override bool HasAsymmetricKey => false;

        public SecurityBindingElement BootstrapSecurityBindingElement
        {
            get
            {
                return bootstrapSecurityBindingElement;
            }
            set
            {
                bootstrapSecurityBindingElement = value;
            }
        }

        public ChannelProtectionRequirements BootstrapProtectionRequirements => bootstrapProtectionRequirements;

        internal BindingContext IssuerBindingContext
        {
            get
            {
                return issuerBindingContext;
            }
            set
            {
                if (value != null)
                {
                    value = value.Clone();
                }
                issuerBindingContext = value;
            }
        }

        private ISecurityCapabilities BootstrapSecurityCapabilities => bootstrapSecurityBindingElement.GetIndividualProperty<ISecurityCapabilities>();

        public bool RequireCancellation
        {
            get
            {
                return requireCancellation;
            }
            set
            {
                requireCancellation = value;
            }
        }

        public bool CanRenewSession
        {
            get
            {
                return canRenewSession;
            }
            set
            {
                canRenewSession = value;
            }
        }

        internal protected override bool SupportsClientAuthentication => BootstrapSecurityCapabilities == null ? false : BootstrapSecurityCapabilities.SupportsClientAuthentication;

        internal protected override bool SupportsServerAuthentication => BootstrapSecurityCapabilities == null ? false : BootstrapSecurityCapabilities.SupportsServerAuthentication;

        internal protected override bool SupportsClientWindowsIdentity => BootstrapSecurityCapabilities == null ? false : BootstrapSecurityCapabilities.SupportsClientWindowsIdentity;

        protected override SecurityTokenParameters CloneCore()
        {
            return new SecureConversationSecurityTokenParameters(this);
        }

        internal protected override SecurityKeyIdentifierClause CreateKeyIdentifierClause(SecurityToken token, SecurityTokenReferenceStyle referenceStyle)
        {
            if (token is GenericXmlSecurityToken)
            {
                return base.CreateGenericXmlTokenKeyIdentifierClause(token, referenceStyle);
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

            sb.AppendLine(String.Format(CultureInfo.InvariantCulture, "RequireCancellation: {0}", requireCancellation.ToString()));
            if (bootstrapSecurityBindingElement == null)
            {
                sb.AppendLine(String.Format(CultureInfo.InvariantCulture, "BootstrapSecurityBindingElement: null"));
            }
            else
            {
                sb.AppendLine(String.Format(CultureInfo.InvariantCulture, "BootstrapSecurityBindingElement:"));
                sb.AppendLine("  " + BootstrapSecurityBindingElement.ToString().Trim().Replace("\n", "\n  "));
            }

            return sb.ToString().Trim();
        }
    }
}
