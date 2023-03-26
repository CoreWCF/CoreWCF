// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.IdentityModel.Tokens;

namespace CoreWCF.IdentityModel.Selectors
{
    public abstract class SecurityTokenProvider
    {
        protected SecurityTokenProvider() { }

        public virtual bool SupportsTokenRenewal
        {
            get { return false; }
        }

        public virtual bool SupportsTokenCancellation
        {
            get { return false; }
        }

        public SecurityToken GetToken(TimeSpan timeout)
        {
            SecurityToken token = GetTokenCore(timeout);
            if (token == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityTokenException(SR.Format(SR.TokenProviderUnableToGetToken, this)));
            }
            return token;
        }

        public Task<SecurityToken> GetTokenAsync(CancellationToken token)
        {
            return GetTokenCoreAsync(token);
        }

        public SecurityToken RenewToken(TimeSpan timeout, SecurityToken tokenToBeRenewed)
        {
            if (tokenToBeRenewed == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(tokenToBeRenewed));
            }
            SecurityToken token = RenewTokenCore(timeout, tokenToBeRenewed);
            if (token == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityTokenException(SR.Format(SR.TokenProviderUnableToRenewToken, this)));
            }
            return token;
        }

        public async Task<SecurityToken> RenewTokenAsync(SecurityToken tokenToBeRenewed, TimeSpan timeout)
        {
            if (tokenToBeRenewed == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(tokenToBeRenewed));
            }
            SecurityToken token = await RenewTokenCoreAsync(tokenToBeRenewed, timeout);
            if (token == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityTokenException(SR.Format(SR.TokenProviderUnableToRenewToken, this)));
            }
            return token;
        }

        public void CancelToken(TimeSpan timeout, SecurityToken token)
        {
            if (token == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(token));
            }
            CancelTokenCore(timeout, token);
        }

        public Task CancelTokenAsync(SecurityToken token, TimeSpan timeout)
        {
            if (token == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(token));
            }
            return CancelTokenCoreAsync(token, timeout);
        }

        // protected methods
        protected abstract SecurityToken GetTokenCore(TimeSpan timeout);

        protected virtual SecurityToken RenewTokenCore(TimeSpan timeout, SecurityToken tokenToBeRenewed)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.TokenRenewalNotSupported, this)));
        }

        protected virtual void CancelTokenCore(TimeSpan timeout, SecurityToken token)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.TokenCancellationNotSupported, this)));
        }

        protected virtual Task<SecurityToken> GetTokenCoreAsync(CancellationToken cancellationToken)
        {
            SecurityToken token = GetToken(Timeout.InfiniteTimeSpan);
            return Task.FromResult(token);
        }

        protected virtual Task<SecurityToken> RenewTokenCoreAsync(SecurityToken tokenToBeRenewed, TimeSpan timeout)
        {
            SecurityToken token = RenewTokenCore(timeout, tokenToBeRenewed);
            return Task.FromResult(token);
        }

        protected virtual Task CancelTokenCoreAsync(SecurityToken token, TimeSpan timeout)
        {
#pragma warning disable VSTHRD103 // Requires to deprecate synchronous public api
            CancelToken(timeout, token);
#pragma warning restore VSTHRD103
            return Task.CompletedTask;
        }
    }
}

