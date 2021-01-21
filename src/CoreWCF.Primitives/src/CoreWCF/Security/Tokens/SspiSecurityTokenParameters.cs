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
    public class SspiSecurityTokenParameters : SecurityTokenParameters
    {
        internal const bool defaultRequireCancellation = false;
        private BindingContext issuerBindingContext;

        protected SspiSecurityTokenParameters(SspiSecurityTokenParameters other)
            : base(other)
        {
            RequireCancellation = other.RequireCancellation;
            if (other.issuerBindingContext != null)
            {
                issuerBindingContext = other.issuerBindingContext.Clone();
            }
        }


        public SspiSecurityTokenParameters()
            : this(defaultRequireCancellation)
        {
            // empty
        }

        public SspiSecurityTokenParameters(bool requireCancellation)
            : base()
        {
            RequireCancellation = requireCancellation;
        }

        internal protected override bool HasAsymmetricKey { get { return false; } }

        public bool RequireCancellation { get; set; } = defaultRequireCancellation;

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

        internal protected override bool SupportsClientAuthentication { get { return true; } }
        internal protected override bool SupportsServerAuthentication { get { return true; } }
        internal protected override bool SupportsClientWindowsIdentity { get { return true; } }

        protected override SecurityTokenParameters CloneCore()
        {
            return new SspiSecurityTokenParameters(this);
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
            requirement.TokenType = ServiceModelSecurityTokenTypes.Spnego;
            requirement.RequireCryptographicToken = true;
            requirement.KeyType = SecurityKeyType.SymmetricKey;
            requirement.Properties[ServiceModelSecurityTokenRequirement.SupportSecurityContextCancellationProperty] = RequireCancellation;
            if (IssuerBindingContext != null)
            {
                requirement.Properties[ServiceModelSecurityTokenRequirement.IssuerBindingContextProperty] = IssuerBindingContext.Clone();
            }
            requirement.Properties[ServiceModelSecurityTokenRequirement.IssuedSecurityTokenParametersProperty] = Clone();
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(base.ToString());

            sb.Append(String.Format(CultureInfo.InvariantCulture, "RequireCancellation: {0}", RequireCancellation.ToString()));

            return sb.ToString();
        }
    }
}
