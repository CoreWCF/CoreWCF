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
    internal class SecurityTokenAuthenticatorAdapter : SecurityTokenAuthenticator
    {
        private SecurityTokenHandler _securityTokenHandler;
        private ExceptionMapper _exceptionMapper;

        public SecurityTokenAuthenticatorAdapter(SecurityTokenHandler securityTokenHandler, ExceptionMapper exceptionMapper)
        {
            if (securityTokenHandler == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(securityTokenHandler));
            }

            if (exceptionMapper == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(exceptionMapper));
            }

            _securityTokenHandler = securityTokenHandler;
            _exceptionMapper = exceptionMapper;
        }

        protected override bool CanValidateTokenCore(SecurityToken token)
        {
            if (token == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(token));
            }

            return ((token.GetType() == _securityTokenHandler.TokenType) && (_securityTokenHandler.CanValidateToken));
        }

        protected override ValueTask<ReadOnlyCollection<IAuthorizationPolicy>> ValidateTokenCoreAsync(SecurityToken token)
        {
            IEnumerable<ClaimsIdentity> subjectCollection = null;

            try
            {
                subjectCollection = _securityTokenHandler.ValidateToken(token);
            }
            catch (Exception ex)
            {
                if (!_exceptionMapper.HandleSecurityTokenProcessingException(ex))
                {
                    throw;
                }
            }

            List<IAuthorizationPolicy> policies = new List<IAuthorizationPolicy>(1);
            policies.Add(new AuthorizationPolicy(subjectCollection));
            return new ValueTask<ReadOnlyCollection<IAuthorizationPolicy>>(policies.AsReadOnly());
        }
    }
}
