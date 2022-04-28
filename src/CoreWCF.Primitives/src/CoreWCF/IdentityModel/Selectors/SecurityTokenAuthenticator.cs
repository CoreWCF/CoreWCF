// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
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

        public ReadOnlyCollection<IAuthorizationPolicy> ValidateToken(SecurityToken token)
        {
            // We don't call this internally, so we won't have problems with sync over async causing performance issues
            var validationResult = ValidateTokenAsync(token);
            return validationResult.IsCompleted
                ? validationResult.Result
                : validationResult.AsTask().GetAwaiter().GetResult();
        }

        public async ValueTask<ReadOnlyCollection<IAuthorizationPolicy>> ValidateTokenAsync(SecurityToken token)
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

        [Obsolete("Implementers should override ValidateTokenCoreAsync.")]
        protected virtual ReadOnlyCollection<IAuthorizationPolicy> ValidateTokenCore(SecurityToken token) => throw new NotImplementedException(SR.SynchronousTokenValidationIsDeprecated);
        
        protected virtual ValueTask<ReadOnlyCollection<IAuthorizationPolicy>> ValidateTokenCoreAsync(SecurityToken token)
        {
            // Default to calling sync implementation to support existing derived types which haven't overridden this method
#pragma warning disable CS0618 // Type or member is obsolete
            return new ValueTask<ReadOnlyCollection<IAuthorizationPolicy>>(ValidateTokenCore(token));
#pragma warning restore CS0618 // Type or member is obsolete
        }
    }
}
