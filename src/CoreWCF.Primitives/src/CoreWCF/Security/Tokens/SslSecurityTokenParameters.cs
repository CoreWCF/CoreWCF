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
    public class SslSecurityTokenParameters : SecurityTokenParameters
    {
        internal const bool defaultRequireClientCertificate = false;
        internal const bool defaultRequireCancellation = false;
        private BindingContext _issuerBindingContext;

        protected SslSecurityTokenParameters(SslSecurityTokenParameters other)
            : base(other)
        {
            RequireClientCertificate = other.RequireClientCertificate;
            RequireCancellation = other.RequireCancellation;
            if (other._issuerBindingContext != null)
            {
                _issuerBindingContext = other._issuerBindingContext.Clone();
            }
        }

        public SslSecurityTokenParameters()
            : this(defaultRequireClientCertificate)
        {
            // empty
        }

        public SslSecurityTokenParameters(bool requireClientCertificate)
            : this(requireClientCertificate, defaultRequireCancellation)
        {
            // empty
        }

        public SslSecurityTokenParameters(bool requireClientCertificate, bool requireCancellation)
            : base()
        {
            RequireClientCertificate = requireClientCertificate;
            RequireCancellation = requireCancellation;
        }

        protected internal override bool HasAsymmetricKey { get { return false; } }

        public bool RequireCancellation { get; set; } = defaultRequireCancellation;

        public bool RequireClientCertificate { get; set; }

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

        protected internal override bool SupportsClientAuthentication { get { return RequireClientCertificate; } }
        protected internal override bool SupportsServerAuthentication { get { return true; } }
        protected internal override bool SupportsClientWindowsIdentity { get { return RequireClientCertificate; } }

        protected override SecurityTokenParameters CloneCore()
        {
            return new SslSecurityTokenParameters(this);
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
            requirement.TokenType = (RequireClientCertificate) ? ServiceModelSecurityTokenTypes.MutualSslnego : ServiceModelSecurityTokenTypes.AnonymousSslnego;
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

            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "RequireCancellation: {0}", RequireCancellation.ToString()));
            sb.Append(string.Format(CultureInfo.InvariantCulture, "RequireClientCertificate: {0}", RequireClientCertificate.ToString()));

            return sb.ToString();
        }
    }
}
