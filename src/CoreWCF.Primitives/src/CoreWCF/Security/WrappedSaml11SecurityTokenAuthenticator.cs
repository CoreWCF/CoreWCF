// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using System.Security.Claims;
using System;
using System.Threading.Tasks;

namespace CoreWCF.Security
{
    /// <summary>
    /// Wraps a Samll1SecurityTokenHandler. Delegates the token authentication call to
    /// this wrapped tokenAuthenticator.  Wraps the returned ClaimsIdentities into
    /// an IAuthorizationPolicy.
    /// </summary>
    internal class WrappedSaml11SecurityTokenAuthenticator : SamlSecurityTokenAuthenticator
    {
        private readonly SamlSecurityTokenHandler _wrappedSaml11SecurityTokenHandler;
        private readonly ExceptionMapper _exceptionMapper;

        /// <summary>
        /// Initializes an instance of <see cref="WrappedSaml11SecurityTokenAuthenticator"/>
        /// </summary>
        /// <param name="saml11SecurityTokenHandler">The Saml11SecurityTokenHandler to wrap.</param>
        /// <param name="exceptionMapper">Converts token validation exceptions to SOAP faults.</param>
        public WrappedSaml11SecurityTokenAuthenticator(
            SamlSecurityTokenHandler saml11SecurityTokenHandler, 
            ExceptionMapper exceptionMapper)
            : base(new List<SecurityTokenAuthenticator>())
        {
            _wrappedSaml11SecurityTokenHandler = saml11SecurityTokenHandler ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(saml11SecurityTokenHandler));
            _exceptionMapper = exceptionMapper ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(exceptionMapper));
        }

        /// <summary>
        /// Validates the token using the wrapped token handler and generates IAuthorizationPolicy
        /// wrapping the returned ClaimsIdentities.
        /// </summary>
        /// <param name="token">Token to be validated.</param>
        /// <returns>Read-only collection of IAuthorizationPolicy</returns>
        protected override ValueTask<ReadOnlyCollection<IAuthorizationPolicy>> ValidateTokenCoreAsync(SecurityToken token)
        {
            IEnumerable<ClaimsIdentity> identities = null;
            try
            {
                identities = _wrappedSaml11SecurityTokenHandler.ValidateToken(token);
            }
            catch (Exception ex)
            {
                if (!_exceptionMapper.HandleSecurityTokenProcessingException(ex))
                {
                    throw;
                }
            }

            List<IAuthorizationPolicy> policies = new List<IAuthorizationPolicy>(1);
            policies.Add(new AuthorizationPolicy(identities));
            return new ValueTask<ReadOnlyCollection<IAuthorizationPolicy>>(policies.AsReadOnly());
        }
    }
}
