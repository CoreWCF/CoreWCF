using CoreWCF.IdentityModel.Tokens;
using CoreWCF;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("tokenToBeRenewed");
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
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("tokenToBeRenewed");
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
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("token");
            }
            CancelTokenCore(timeout, token);
        }

        public Task CancelTokenAsync(SecurityToken token, TimeSpan timeout)
        {
            if (token == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("token");
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
            CancelToken(timeout, token);
            return Task.CompletedTask;
        }
    }
}

