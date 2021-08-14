// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Tokens;

namespace CoreWCF.IdentityModel.Selectors
{
    public abstract class SecurityTokenAuthenticator
    {
        protected SecurityTokenAuthenticator() { }

        public bool CanValidateToken(SecurityToken token)
        {
            if (token == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(token));
            }
            return CanValidateTokenCore(token);
        }

        public async Task<ReadOnlyCollection<IAuthorizationPolicy>> ValidateTokenAsync(SecurityToken token)
        {
            if (token == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(token));
            }
            if (!CanValidateToken(token))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityTokenValidationException(SR.Format(SR.CannotValidateSecurityTokenType, this, token.GetType())));
            }

            ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies = await ValidateTokenCoreAsync(token);
            if (authorizationPolicies == null)
            {
                string errorMsg = SR.Format(SR.CannotValidateSecurityTokenType, this, token.GetType());
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityTokenValidationException(errorMsg));
            }

            return authorizationPolicies;
        }

        protected abstract bool CanValidateTokenCore(SecurityToken token);
        protected abstract Task<ReadOnlyCollection<IAuthorizationPolicy>> ValidateTokenCoreAsync(SecurityToken token);
    }
}
