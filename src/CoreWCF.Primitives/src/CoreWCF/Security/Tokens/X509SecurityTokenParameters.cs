// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.Text;
using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;

namespace CoreWCF.Security.Tokens
{
    public class X509SecurityTokenParameters : SecurityTokenParameters
    {
        internal const X509KeyIdentifierClauseType defaultX509ReferenceStyle = X509KeyIdentifierClauseType.Any;
        private X509KeyIdentifierClauseType _x509ReferenceStyle;

        protected X509SecurityTokenParameters(X509SecurityTokenParameters other)
            : base(other)
        {
            _x509ReferenceStyle = other._x509ReferenceStyle;
        }

        public X509SecurityTokenParameters()
            : this(defaultX509ReferenceStyle, defaultInclusionMode)
        {
            // empty
        }

        public X509SecurityTokenParameters(X509KeyIdentifierClauseType x509ReferenceStyle)
            : this(x509ReferenceStyle, defaultInclusionMode)
        {
            // empty
        }

        public X509SecurityTokenParameters(X509KeyIdentifierClauseType x509ReferenceStyle, SecurityTokenInclusionMode inclusionMode)
            : this(x509ReferenceStyle, inclusionMode, defaultRequireDerivedKeys)
        {
        }

        internal X509SecurityTokenParameters(X509KeyIdentifierClauseType x509ReferenceStyle, SecurityTokenInclusionMode inclusionMode,
            bool requireDerivedKeys)
            : base()
        {
            X509ReferenceStyle = x509ReferenceStyle;
            InclusionMode = inclusionMode;
            RequireDerivedKeys = requireDerivedKeys;
        }

        protected internal override bool HasAsymmetricKey { get { return true; } }

        public X509KeyIdentifierClauseType X509ReferenceStyle
        {
            get
            {
                return _x509ReferenceStyle;
            }
            set
            {
                X509SecurityTokenReferenceStyleHelper.Validate(value);
                _x509ReferenceStyle = value;
            }
        }

        protected internal override bool SupportsClientAuthentication { get { return true; } }
        protected internal override bool SupportsServerAuthentication { get { return true; } }
        protected internal override bool SupportsClientWindowsIdentity { get { return true; } }

        protected override SecurityTokenParameters CloneCore()
        {
            return new X509SecurityTokenParameters(this);
        }

        protected internal override SecurityKeyIdentifierClause CreateKeyIdentifierClause(SecurityToken token, SecurityTokenReferenceStyle referenceStyle)
        {
            SecurityKeyIdentifierClause result = null;

            switch (_x509ReferenceStyle)
            {
                default:
                case X509KeyIdentifierClauseType.Any:
                    if (referenceStyle == SecurityTokenReferenceStyle.External)
                    {
                        if (token is X509SecurityToken x509Token)
                        {
                            if (X509SubjectKeyIdentifierClause.TryCreateFrom(x509Token.Certificate, out X509SubjectKeyIdentifierClause x509KeyIdentifierClause))
                            {
                                result = x509KeyIdentifierClause;
                            }
                        }
                        else
                        {
                            if (token is X509WindowsSecurityToken windowsX509Token)
                            {
                                if (X509SubjectKeyIdentifierClause.TryCreateFrom(windowsX509Token.Certificate, out X509SubjectKeyIdentifierClause x509KeyIdentifierClause))
                                {
                                    result = x509KeyIdentifierClause;
                                }
                            }
                        }

                        if (result == null)
                        {
                            result = token.CreateKeyIdentifierClause<X509IssuerSerialKeyIdentifierClause>();
                        }

                        if (result == null)
                        {
                            result = token.CreateKeyIdentifierClause<X509ThumbprintKeyIdentifierClause>();
                        }
                    }
                    else
                    {
                        result = token.CreateKeyIdentifierClause<LocalIdKeyIdentifierClause>();
                    }

                    break;
                case X509KeyIdentifierClauseType.Thumbprint:
                    result = CreateKeyIdentifierClause<X509ThumbprintKeyIdentifierClause, LocalIdKeyIdentifierClause>(token, referenceStyle);
                    break;
                case X509KeyIdentifierClauseType.SubjectKeyIdentifier:
                    result = CreateKeyIdentifierClause<X509SubjectKeyIdentifierClause, LocalIdKeyIdentifierClause>(token, referenceStyle);
                    break;
                case X509KeyIdentifierClauseType.IssuerSerial:
                    result = CreateKeyIdentifierClause<X509IssuerSerialKeyIdentifierClause, LocalIdKeyIdentifierClause>(token, referenceStyle);
                    break;
                case X509KeyIdentifierClauseType.RawDataKeyIdentifier:
                    result = CreateKeyIdentifierClause<X509RawDataKeyIdentifierClause, LocalIdKeyIdentifierClause>(token, referenceStyle);
                    break;
            }

            return result;
        }

        protected internal override void InitializeSecurityTokenRequirement(SecurityTokenRequirement requirement)
        {
            requirement.TokenType = SecurityTokenTypes.X509Certificate;
            requirement.RequireCryptographicToken = true;
            requirement.KeyType = SecurityKeyType.AsymmetricKey;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(base.ToString());

            sb.Append(string.Format(CultureInfo.InvariantCulture, "X509ReferenceStyle: {0}", _x509ReferenceStyle.ToString()));

            return sb.ToString();
        }
    }
}
