// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Security.Tokens
{
    using System;
    using System.Globalization;
    using System.Text;
    using CoreWCF;
    using CoreWCF.IdentityModel;
    using CoreWCF.IdentityModel.Selectors;
    using CoreWCF.IdentityModel.Tokens;
    using CoreWCF.Security;

    public abstract class SecurityTokenParameters
    {
        internal const SecurityTokenInclusionMode defaultInclusionMode = SecurityTokenInclusionMode.AlwaysToRecipient;
        internal const SecurityTokenReferenceStyle defaultReferenceStyle = SecurityTokenReferenceStyle.Internal;
        internal const bool defaultRequireDerivedKeys = true;
        private SecurityTokenInclusionMode _inclusionMode = defaultInclusionMode;
        private SecurityTokenReferenceStyle _referenceStyle = defaultReferenceStyle;

        protected SecurityTokenParameters(SecurityTokenParameters other)
        {
            if (other == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("other");
            }

            RequireDerivedKeys = other.RequireDerivedKeys;
            _inclusionMode = other._inclusionMode;
            _referenceStyle = other._referenceStyle;
        }

        protected SecurityTokenParameters()
        {
            // empty
        }

        internal protected abstract bool HasAsymmetricKey { get; }

        public SecurityTokenInclusionMode InclusionMode
        {
            get
            {
                return _inclusionMode;
            }
            set
            {
                SecurityTokenInclusionModeHelper.Validate(value);
                _inclusionMode = value;
            }
        }

        public SecurityTokenReferenceStyle ReferenceStyle
        {
            get
            {
                return _referenceStyle;
            }
            set
            {
                TokenReferenceStyleHelper.Validate(value);
                _referenceStyle = value;
            }
        }

        public bool RequireDerivedKeys { get; set; } = defaultRequireDerivedKeys;

        internal protected abstract bool SupportsClientAuthentication { get; }
        internal protected abstract bool SupportsServerAuthentication { get; }
        internal protected abstract bool SupportsClientWindowsIdentity { get; }

        public SecurityTokenParameters Clone()
        {
            SecurityTokenParameters result = CloneCore();

            if (result == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SecurityTokenParametersCloneInvalidResult, GetType().ToString())));
            }

            return result;
        }

        protected abstract SecurityTokenParameters CloneCore();

        internal protected abstract SecurityKeyIdentifierClause CreateKeyIdentifierClause(SecurityToken token, SecurityTokenReferenceStyle referenceStyle);

        internal protected abstract void InitializeSecurityTokenRequirement(SecurityTokenRequirement requirement);

        internal SecurityKeyIdentifierClause CreateKeyIdentifierClause<TExternalClause, TInternalClause>(SecurityToken token, SecurityTokenReferenceStyle referenceStyle)
            where TExternalClause : SecurityKeyIdentifierClause
            where TInternalClause : SecurityKeyIdentifierClause
        {
            if (token == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("token");
            }

            SecurityKeyIdentifierClause result;

            switch (referenceStyle)
            {
                default:
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(
                        SR.Format(SR.TokenDoesNotSupportKeyIdentifierClauseCreation, token.GetType().Name, referenceStyle)));
                case SecurityTokenReferenceStyle.External:
                    result = token.CreateKeyIdentifierClause<TExternalClause>();
                    break;
                case SecurityTokenReferenceStyle.Internal:
                    result = token.CreateKeyIdentifierClause<TInternalClause>();
                    break;
            }

            return result;
        }

        internal SecurityKeyIdentifierClause CreateGenericXmlTokenKeyIdentifierClause(SecurityToken token, SecurityTokenReferenceStyle referenceStyle)
        {
            GenericXmlSecurityToken xmlToken = token as GenericXmlSecurityToken;
            if (xmlToken != null)
            {
                if (referenceStyle == SecurityTokenReferenceStyle.Internal && xmlToken.InternalTokenReference != null)
                {
                    return xmlToken.InternalTokenReference;
                }

                if (referenceStyle == SecurityTokenReferenceStyle.External && xmlToken.ExternalTokenReference != null)
                {
                    return xmlToken.ExternalTokenReference;
                }
            }

            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.UnableToCreateTokenReference)));
        }

        internal protected virtual bool MatchesKeyIdentifierClause(SecurityToken token, SecurityKeyIdentifierClause keyIdentifierClause, SecurityTokenReferenceStyle referenceStyle)
        {
            if (token == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("token");
            }

            if (token is GenericXmlSecurityToken)
            {
                return MatchesGenericXmlTokenKeyIdentifierClause(token, keyIdentifierClause, referenceStyle);
            }

            bool result;

            switch (referenceStyle)
            {
                default:
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(
                        SR.Format(SR.TokenDoesNotSupportKeyIdentifierClauseCreation, token.GetType().Name, referenceStyle)));
                case SecurityTokenReferenceStyle.External:
                    if (keyIdentifierClause is LocalIdKeyIdentifierClause)
                    {
                        result = false;
                    }
                    else
                    {
                        result = token.MatchesKeyIdentifierClause(keyIdentifierClause);
                    }

                    break;
                case SecurityTokenReferenceStyle.Internal:
                    result = token.MatchesKeyIdentifierClause(keyIdentifierClause);
                    break;
            }

            return result;
        }

        internal bool MatchesGenericXmlTokenKeyIdentifierClause(SecurityToken token, SecurityKeyIdentifierClause keyIdentifierClause, SecurityTokenReferenceStyle referenceStyle)
        {
            if (token == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("token");
            }

            bool result;

            GenericXmlSecurityToken xmlToken = token as GenericXmlSecurityToken;

            if (xmlToken == null)
            {
                result = false;
            }
            else if (referenceStyle == SecurityTokenReferenceStyle.External && xmlToken.ExternalTokenReference != null)
            {
                result = xmlToken.ExternalTokenReference.Matches(keyIdentifierClause);
            }
            else if (referenceStyle == SecurityTokenReferenceStyle.Internal)
            {
                result = xmlToken.MatchesKeyIdentifierClause(keyIdentifierClause);
            }
            else
            {
                result = false;
            }

            return result;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine(String.Format(CultureInfo.InvariantCulture, "{0}:", GetType().ToString()));
            sb.AppendLine(String.Format(CultureInfo.InvariantCulture, "InclusionMode: {0}", _inclusionMode.ToString()));
            sb.AppendLine(String.Format(CultureInfo.InvariantCulture, "ReferenceStyle: {0}", _referenceStyle.ToString()));
            sb.Append(String.Format(CultureInfo.InvariantCulture, "RequireDerivedKeys: {0}", RequireDerivedKeys.ToString()));

            return sb.ToString();
        }
    }
}
