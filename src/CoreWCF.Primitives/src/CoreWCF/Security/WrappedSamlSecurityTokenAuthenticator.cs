// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;

namespace CoreWCF.Security
{
    /// <summary>
    /// Authenticator that wraps both SAML 1.1 and SAML 2.0 WrapperSecurityTokenAuthenticators.
    /// </summary>
    internal class WrappedSamlSecurityTokenAuthenticator : SecurityTokenAuthenticator
    {
        private WrappedSaml11SecurityTokenAuthenticator _wrappedSaml11SecurityTokenAuthenticator;
        private WrappedSaml2SecurityTokenAuthenticator _wrappedSaml2SecurityTokenAuthenticator;

        public WrappedSamlSecurityTokenAuthenticator(WrappedSaml11SecurityTokenAuthenticator wrappedSaml11SecurityTokenAuthenticator, WrappedSaml2SecurityTokenAuthenticator wrappedSaml2SecurityTokenAuthenticator)
        {
            if (wrappedSaml11SecurityTokenAuthenticator == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(wrappedSaml11SecurityTokenAuthenticator));
            }

            if (wrappedSaml2SecurityTokenAuthenticator == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(wrappedSaml2SecurityTokenAuthenticator));
            }

            _wrappedSaml11SecurityTokenAuthenticator = wrappedSaml11SecurityTokenAuthenticator;
            _wrappedSaml2SecurityTokenAuthenticator = wrappedSaml2SecurityTokenAuthenticator;
        }

        protected override bool CanValidateTokenCore(SecurityToken token)
        {
            return (_wrappedSaml11SecurityTokenAuthenticator.CanValidateToken(token) || _wrappedSaml2SecurityTokenAuthenticator.CanValidateToken(token));
        }

        protected override ValueTask<ReadOnlyCollection<IAuthorizationPolicy>> ValidateTokenCoreAsync(SecurityToken token)
        {
            if (_wrappedSaml11SecurityTokenAuthenticator.CanValidateToken(token))
            {
                return _wrappedSaml11SecurityTokenAuthenticator.ValidateTokenAsync(token);
            }
            else if (_wrappedSaml2SecurityTokenAuthenticator.CanValidateToken(token))
            {
                return _wrappedSaml2SecurityTokenAuthenticator.ValidateTokenAsync(token);
            }
            else
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.Format(SR.ID4101, token.GetType().ToString())));
            }
        }
    }
}
